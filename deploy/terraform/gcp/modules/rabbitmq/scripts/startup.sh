#!/usr/bin/env bash
set -euo pipefail

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

# 2. Install prerequisites & RabbitMQ signing keys
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y curl gnupg apt-transport-https ssl-cert openssl

# Team RabbitMQ repository
curl -1sLf "https://keys.openpgp.org/vks/v1/by-fingerprint/0A9AF2115F4687BD29803A206B73A36E6026DFCA" | gpg --dearmor > /usr/share/keyrings/com.rabbitmq.team.gpg
curl -1sLf "https://github.com/rabbitmq/signing-keys/releases/download/3.0/cloudsmith.rabbitmq-erlang.E495BB49CC4BBE5B.key" | gpg --dearmor > /usr/share/keyrings/rabbitmq.E495BB49CC4BBE5B.gpg
curl -1sLf "https://github.com/rabbitmq/signing-keys/releases/download/3.0/cloudsmith.rabbitmq-server.9F4587F2262083E3.key" | gpg --dearmor > /usr/share/keyrings/rabbitmq.9F4587F2262083E3.gpg

DISTRO=$(lsb_release -cs)

cat <<EOF > /etc/apt/sources.list.d/rabbitmq.list
deb [signed-by=/usr/share/keyrings/rabbitmq.E495BB49CC4BBE5B.gpg] https://ppa1.novemberain.com/rabbitmq/rabbitmq-erlang/ubuntu/ $DISTRO main
deb-src [signed-by=/usr/share/keyrings/rabbitmq.E495BB49CC4BBE5B.gpg] https://ppa1.novemberain.com/rabbitmq/rabbitmq-erlang/ubuntu/ $DISTRO main
deb [signed-by=/usr/share/keyrings/rabbitmq.9F4587F2262083E3.gpg] https://ppa1.novemberain.com/rabbitmq/rabbitmq-server/ubuntu/ $DISTRO main
deb-src [signed-by=/usr/share/keyrings/rabbitmq.9F4587F2262083E3.gpg] https://ppa1.novemberain.com/rabbitmq/rabbitmq-server/ubuntu/ $DISTRO main
EOF

apt-get update -y
apt-get install -y erlang-base erlang-asn1 erlang-crypto erlang-eldap erlang-inets erlang-mnesia erlang-os-mon erlang-parsetools erlang-public-key erlang-ssl erlang-syntax-tools erlang-tools erlang-xmerl rabbitmq-server

# 3. Generate self-signed TLS certificates for AMQPS
CERT_DIR="/etc/rabbitmq/ssl"
mkdir -p "$CERT_DIR"
if [ ! -f "$CERT_DIR/server.key" ]; then
    openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
        -keyout "$CERT_DIR/server.key" \
        -out "$CERT_DIR/server.crt" \
        -subj "/CN=rabbitmq.shopsphere.internal"
fi
chown -R rabbitmq:rabbitmq "$CERT_DIR"
chmod 600 "$CERT_DIR/server.key"

# 4. RabbitMQ Configuration
cat <<EOF > /etc/rabbitmq/rabbitmq.conf
listeners.tcp.default = 5672
listeners.ssl.default = 5671
ssl_options.cacertfile = $CERT_DIR/server.crt
ssl_options.certfile   = $CERT_DIR/server.crt
ssl_options.keyfile    = $CERT_DIR/server.key
ssl_options.verify     = verify_none
ssl_options.fail_if_no_peer_cert = false
management.tcp.port = 15672
EOF

chown -R rabbitmq:rabbitmq "$MOUNT_DIR"
systemctl enable rabbitmq-server
systemctl restart rabbitmq-server

rabbitmq-plugins enable rabbitmq_management

# 5. Provision Admin User & Service VHosts
ADMIN_USER="shopsphere_admin"
ADMIN_PASS="${RABBITMQ_ADMIN_PASSWORD}"

rabbitmqctl add_user "$ADMIN_USER" "$ADMIN_PASS" || rabbitmqctl change_password "$ADMIN_USER" "$ADMIN_PASS"
rabbitmqctl set_user_tags "$ADMIN_USER" administrator
rabbitmqctl set_permissions -p / "$ADMIN_USER" ".*" ".*" ".*"
rabbitmqctl delete_user guest || true

# Provision per-service vhosts and users
for VHOST in orders inventory notifications catalog payment shipping; do
    rabbitmqctl add_vhost "/$VHOST" || true
    rabbitmqctl set_permissions -p "/$VHOST" "$ADMIN_USER" ".*" ".*" ".*"
done

echo "=== RabbitMQ Initialization Complete ==="
