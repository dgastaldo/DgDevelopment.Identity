// One VNet per environment: a dedicated subnet for Private Endpoints (SQL), plus one "compute"
// subnet the environment's App Service (VNet Integration) or AKS node pool uses to reach it
// privately - this is what actually closes the SonarCloud S6329 finding on the sql module,
// instead of just accepting the public-access risk.

resource "azurerm_virtual_network" "this" {
  name                = "${var.name_prefix}-vnet"
  resource_group_name = var.resource_group_name
  location            = var.location
  address_space       = [var.address_space]
  tags                = var.tags
}

resource "azurerm_subnet" "private_endpoints" {
  name                              = "private-endpoints"
  resource_group_name               = var.resource_group_name
  virtual_network_name              = azurerm_virtual_network.this.name
  address_prefixes                  = [var.private_endpoint_subnet_prefix]
  private_endpoint_network_policies = "Enabled"
}

// Delegated to Microsoft.Web/serverFarms for App Service VNet Integration (docs); left
// undelegated for AKS (integration/production), which attaches a subnet to its node pool
// directly without a delegation.
resource "azurerm_subnet" "compute" {
  name                 = "compute"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [var.compute_subnet_prefix]

  dynamic "delegation" {
    for_each = { for idx, d in [var.compute_subnet_delegation] : idx => d if d != null }
    content {
      name = "compute-delegation"
      service_delegation {
        name    = delegation.value.service_name
        actions = delegation.value.actions
      }
    }
  }
}

resource "azurerm_private_dns_zone" "sql" {
  name                = "privatelink.database.windows.net"
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "sql" {
  name                  = "${var.name_prefix}-sql-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.sql.name
  virtual_network_id    = azurerm_virtual_network.this.id
}
