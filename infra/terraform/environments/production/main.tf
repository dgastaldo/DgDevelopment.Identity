// production: true production tier, per docs/deployment.md's branch/environment table - own
// resource group/cluster, separate from integration.

module "resource_group" {
  source = "../../modules/resource-group"

  name     = "rg-identity-production"
  location = var.location
  tags     = local.tags
}

module "sql" {
  source = "../../modules/sql"

  server_name                  = "sql-identity-production"
  resource_group_name          = module.resource_group.name
  location                     = var.location
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  sku_name                     = "S0"
  tags                         = local.tags
}

module "notification_hub" {
  source = "../../modules/notification-hub"

  namespace_name      = "identity-production-ns"
  hub_name            = "identity-production-hub"
  resource_group_name = module.resource_group.name
  location            = var.location
  sku_name            = "Free"
  fcm_api_key         = var.fcm_api_key
  tags                = local.tags
}

module "aks" {
  source = "../../modules/aks"

  cluster_name        = "aks-identity-production"
  resource_group_name = module.resource_group.name
  location            = var.location
  kubernetes_version  = var.kubernetes_version
  node_count          = 1
  vm_size             = "Standard_B2s"
  tags                = local.tags
}

module "ghcr_pull_secret" {
  source = "../../modules/ghcr-pull-secret"

  namespace     = "default"
  ghcr_username = var.ghcr_username
  ghcr_token    = var.ghcr_token

  depends_on = [module.aks]
}

locals {
  tags = {
    environment = "production"
    managed_by  = "terraform"
  }
}
