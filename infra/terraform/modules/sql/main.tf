resource "azurerm_mssql_server" "this" {
  name                         = var.server_name
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.administrator_login
  administrator_login_password = var.administrator_login_password
  minimum_tls_version          = "1.2"
  tags                         = var.tags

  // Genuinely closed, not just accepted: with a Private Endpoint wired (private_endpoint_subnet_id
  // set), the server has no public network path at all - reachable only from inside the VNet.
  public_network_access_enabled = var.private_endpoint_subnet_id == null

  // Not used for authentication today (SQL login/password above) - declared so a future move to
  // Azure AD-only auth, or to a managed-identity-based connection, has an identity to attach to.
  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_mssql_database" "identity_db" {
  name           = "IdentityDb"
  server_id      = azurerm_mssql_server.this.id
  sku_name       = var.sku_name
  max_size_gb    = var.max_size_gb
  zone_redundant = false
  tags           = var.tags
}

// Only relevant when public access is on (private_endpoint_subnet_id is null) - kept as a
// fallback for any future environment that doesn't wire private networking, not used by
// docs/integration/production once they pass private_endpoint_subnet_id.
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  count = var.private_endpoint_subnet_id == null ? 1 : 0

  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.this.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_private_endpoint" "sql" {
  count = var.private_endpoint_subnet_id != null ? 1 : 0

  name                = "${var.server_name}-pe"
  resource_group_name = var.resource_group_name
  location            = var.location
  subnet_id           = var.private_endpoint_subnet_id
  tags                = var.tags

  private_service_connection {
    name                           = "${var.server_name}-psc"
    private_connection_resource_id = azurerm_mssql_server.this.id
    subresource_names              = ["sqlServer"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [var.private_dns_zone_id]
  }
}
