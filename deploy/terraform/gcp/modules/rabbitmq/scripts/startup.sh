#!/usr/bin/env bash
set -euxo pipefail

echo "=== Initializing RabbitMQ Installation on Ubuntu ==="

# 1. Mount persistent disk to /var/lib/rabbitmq
DISK_DEVICE="/dev/disk/by-id/google-rabbitmq-data"
MOUNT_DIR="/var/lib/rabbitmq"

mkdir -p "$MOUNT_DIR"

if [ -b "$DISK_DEVICE" ]; then
    if ! blkid "$DISK_DEVICE" | grep -q 'TYPE="ext4"'; then
        echo "Formatting $DISK_DEVICE to ext4..."
        mkfs.ext4 -m 0 -E lazy_itable_init=0,lazy_journal_init=0 "$DISK_DEVICE"
    fi
    if ! mountpoint -q "$MOUNT_DIR"; then
        mount -o discard,defaults "$DISK_DEVICE" "$MOUNT_DIR"
        echo "$DISK_DEVICE $MOUNT_DIR ext4 discard,defaults,nofail 0 2" >> /etc/fstab
    fi
fi

# 2. Install prerequisites & RabbitMQ from Ubuntu standard repositories
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y rabbitmq-server ssl-cert openssl

# 3. RabbitMQ Configuration
mkdir -p /etc/rabbitmq
cat <<EOF > /etc/rabbitmq/rabbitmq.conf
listeners.tcp.default = 5672
management.tcp.port = 15672
EOF

chown -R rabbitmq:rabbitmq "$MOUNT_DIR" /etc/rabbitmq || true
systemctl daemon-reload
systemctl enable rabbitmq-server
systemctl restart rabbitmq-server

# Wait for rabbitmq-server to be active
for i in {1..30}; do
    if rabbitmqctl status >/dev/null 2>&1; then
        echo "RabbitMQ server is responding."
        break
    fi
    sleep 2
done

rabbitmq-plugins enable rabbitmq_management || true

# 4. Provision Admin Users & Service VHosts
ADMIN_PASS="${RABBITMQ_ADMIN_PASSWORD}"

rabbitmqctl add_user "admin" "$ADMIN_PASS" || rabbitmqctl change_password "admin" "$ADMIN_PASS"
rabbitmqctl set_user_tags "admin" administrator
rabbitmqctl set_permissions -p / "admin" ".*" ".*" ".*"

rabbitmqctl add_user "shopsphere_admin" "$ADMIN_PASS" || rabbitmqctl change_password "shopsphere_admin" "$ADMIN_PASS"
rabbitmqctl set_user_tags "shopsphere_admin" administrator
rabbitmqctl set_permissions -p / "shopsphere_admin" ".*" ".*" ".*"

rabbitmqctl delete_user guest || true

for VHOST in orders inventory notifications catalog payment shipping; do
    rabbitmqctl add_vhost "/$VHOST" || true
    rabbitmqctl set_permissions -p "/$VHOST" "admin" ".*" ".*" ".*"
    rabbitmqctl set_permissions -p "/$VHOST" "shopsphere_admin" ".*" ".*" ".*"
done

echo "=== RabbitMQ Initialization Complete ==="
