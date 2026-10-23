output "instance_name" {
  value       = google_sql_database_instance.instance.name
  description = "Name of the Cloud SQL instance."
}

output "private_ip_address" {
  value       = google_sql_database_instance.instance.private_ip_address
  description = "Private IP address of the Cloud SQL instance."
}

output "database_names" {
  value = {
    for k, v in google_sql_database.databases : k => v.name
  }
  description = "Map of service key to database name."
}

output "service_users" {
  value = {
    for k, v in google_sql_user.users : k => v.name
  }
  description = "Map of service key to SQL user name."
}

output "connection_strings" {
  value = {
    for k, v in var.services : k => "Server=${google_sql_database_instance.instance.private_ip_address};Database=${v};User Id=${k}_user;Password=${random_password.service_passwords[k].result};TrustServerCertificate=True;Max Pool Size=50;"
  }
  sensitive   = true
  description = "Per-service SQL Server connection strings with private IP and dedicated credentials."
}
