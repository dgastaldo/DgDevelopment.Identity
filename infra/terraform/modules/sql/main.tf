resource "azurerm_mssql_server" "this" {
  name                         = var.server_name
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.administrator_login
  administrator_login_password = var.administrator_login_password
  minimum_tls_version          = "1.2"
  tags                         = var.tags
}

resource "azurerm_mssql_database" "identity_db" {
  name           = "IdentityDb"
  server_id      = azurerm_mssql_server.this.id
  sku_name       = var.sku_name
  max_size_gb    = var.max_size_gb
  zone_redundant = false
  tags           = var.tags
}

// Lets Azure services (the AKS cluster / App Service running this environment's app) reach the
// server without enumerating individual outbound IPs - narrower, IP-scoped rules can replace this
// once the compute environment's egress IPs are known/stable.
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.this.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
