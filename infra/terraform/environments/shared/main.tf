// One ACS resource shared by develop (ephemeral) and personal - costs real money per email sent,
// so deployment.md's "Shared or per-environment resources?" decision keeps it to a single bill;
// develop/personal stay distinguishable by sender address/display name, not separate resources.

module "resource_group" {
  source = "../../modules/resource-group"

  name     = "rg-identity-shared"
  location = var.location
  tags     = local.tags
}

module "communication_services" {
  source = "../../modules/communication-services"

  name                = "identity-acs"
  email_service_name  = "identity-acs-email"
  resource_group_name = module.resource_group.name
  tags                = local.tags
}

locals {
  tags = {
    environment = "shared"
    managed_by  = "terraform"
  }
}
