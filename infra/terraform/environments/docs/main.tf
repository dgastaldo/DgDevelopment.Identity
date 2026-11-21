// docs is genuinely public (hosts the IDP's own API docs for third-party integrators, not just a
// stage name - see docs/deployment.md). App Service, per the branch/environment table there.

module "resource_group" {
  source = "../../modules/resource-group"

  name     = "rg-identity-docs"
  location = var.location
  tags     = local.tags
}

// Private networking: SQL gets a Private Endpoint (no public access at all), reachable only from
// the compute subnet App Service's VNet Integration attaches to.
module "networking" {
  source = "../../modules/networking"

  name_prefix         = "identity-docs"
  resource_group_name = module.resource_group.name
  location            = var.location
  tags                = local.tags

  compute_subnet_delegation = {
    service_name = "Microsoft.Web/serverFarms"
    actions      = ["Microsoft.Network/virtualNetworks/subnets/action"]
  }
}

module "sql" {
  source = "../../modules/sql"

  server_name                  = "sql-identity-docs"
  resource_group_name          = module.resource_group.name
  location                     = var.location
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  sku_name                     = "Basic"
  tags                         = local.tags
  private_endpoint_subnet_id   = module.networking.private_endpoint_subnet_id
  private_dns_zone_id          = module.networking.sql_private_dns_zone_id
}

module "notification_hub" {
  source = "../../modules/notification-hub"

  namespace_name      = "identity-docs-ns"
  hub_name            = "identity-docs-hub"
  resource_group_name = module.resource_group.name
  location            = var.location
  sku_name            = "Free"
  fcm_api_key         = var.fcm_api_key
  tags                = local.tags
}

module "app_service" {
  source = "../../modules/app-service"

  name_prefix                    = "identity-docs"
  resource_group_name            = module.resource_group.name
  location                       = var.location
  sku_name                       = "B1"
  server_image                   = "ghcr.io/dgastaldo/dgdevelopment-identity-server:docs-latest"
  identity_platform_image        = "ghcr.io/dgastaldo/dgdevelopment-identity-platform:docs-latest"
  ghcr_username                  = var.ghcr_username
  ghcr_token                     = var.ghcr_token
  identity_platform_app_settings = {}
  tags                           = local.tags
  vnet_integration_subnet_id     = module.networking.compute_subnet_id
  server_app_settings = {
    "Parameters__IdentityDb" = module.sql.connection_string
  }
}

locals {
  tags = {
    environment = "docs"
    managed_by  = "terraform"
  }
}
