output "connection_string" {
  value     = azurerm_communication_service.this.primary_connection_string
  sensitive = true
}

output "mail_from_domain" {
  value = azurerm_email_communication_service_domain.this.mail_from_sender_domain
}
