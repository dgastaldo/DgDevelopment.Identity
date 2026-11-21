output "connection_string" {
  description = "Maps to Parameters__IdentityDb in app config."
  value       = "Server=tcp:${azurerm_mssql_server.this.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.identity_db.name};User ID=${var.administrator_login};Password=${var.administrator_login_password};Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
  sensitive   = true
}

output "server_fqdn" {
  value = azurerm_mssql_server.this.fully_qualified_domain_name
}
