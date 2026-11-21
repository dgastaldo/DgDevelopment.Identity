output "cluster_name" {
  value = module.aks.cluster_name
}

output "sql_connection_string" {
  value     = module.sql.connection_string
  sensitive = true
}

output "notification_hub_connection_string" {
  value     = module.notification_hub.connection_string
  sensitive = true
}

output "ghcr_pull_secret_name" {
  value = module.ghcr_pull_secret.secret_name
}
