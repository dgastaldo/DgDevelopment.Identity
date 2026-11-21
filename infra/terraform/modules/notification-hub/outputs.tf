output "connection_string" {
  description = "Listen+Send SAS connection string - maps to Azure:NotificationHub:ConnectionString in app config."
  value       = azurerm_notification_hub_authorization_rule.listen_send.primary_connection_string
  sensitive   = true
}

output "namespace_name" {
  value = azurerm_notification_hub_namespace.this.name
}

output "hub_name" {
  description = "Maps to Azure:NotificationHub:Name in app config."
  value       = azurerm_notification_hub.this.name
}
