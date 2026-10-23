output "instance_name" {
  value       = google_compute_instance.rabbitmq_vm.name
  description = "Compute Engine instance name for RabbitMQ."
}

output "private_ip" {
  value       = google_compute_instance.rabbitmq_vm.network_interface[0].network_ip
  description = "Private IP address of RabbitMQ."
}

output "amqp_port" {
  value       = 5672
  description = "Standard AMQP port."
}

output "amqps_port" {
  value       = 5671
  description = "TLS AMQPS port."
}
