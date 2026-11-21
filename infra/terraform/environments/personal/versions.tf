terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  # State: dedicated Storage Account (see infra/terraform/README.md's bootstrap section for how
  # this storage account itself gets created - chicken-and-egg, so it's provisioned by hand once,
  # outside Terraform, same as the OIDC app registration below).
  backend "azurerm" {
    resource_group_name  = "dgdevelopment-identity-tfstate"
    storage_account_name = "dgdevidentitytfstate" # must be globally unique - adjust if taken
    container_name       = "tfstate"
    key                  = "personal.tfstate"
    use_oidc             = true
  }
}

provider "azurerm" {
  features {}
  use_oidc        = true
  subscription_id = var.subscription_id
}
