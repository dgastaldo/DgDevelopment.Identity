// personal itself (Server/IdentityPlatform/SQL) stays on the NUC, outside Azure entirely - see
// docs/deployment.md's "Revised: personal deploy model". The only Azure dependency this
// environment has is its own Notification Hub (push MFA) - separate per environment per
// deployment.md's "Shared or per-environment resources?" decision.

module "resource_group" {
  source = "../../modules/resource-group"

  name     = "rg-identity-personal"
  location = var.location
  tags     = local.tags
}

module "notification_hub" {
  source = "../../modules/notification-hub"

  namespace_name      = "identity-personal-ns"
  hub_name            = "identity-personal-hub"
  resource_group_name = module.resource_group.name
  location            = var.location
  sku_name            = "Free"
  fcm_api_key         = var.fcm_api_key
  tags                = local.tags
}

locals {
  tags = {
    environment = "personal"
    managed_by  = "terraform"
  }
}
