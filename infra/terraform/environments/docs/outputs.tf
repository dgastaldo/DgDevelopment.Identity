output "server_hostname" {
  value = module.app_service.server_hostname
}

output "identity_platform_hostname" {
  value = module.app_service.identity_platform_hostname
}

output "sql_connection_string" {
  value     = module.sql.connection_string
  sensitive = true
}

output "notification_hub_connection_string" {
  value     = module.notification_hub.connection_string
  sensitive = true
}
