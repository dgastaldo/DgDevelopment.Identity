terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.30"
    }
  }

  backend "azurerm" {
    resource_group_name  = "dgdevelopment-identity-tfstate"
    storage_account_name = "dgdevidentitytfstate"
    container_name       = "tfstate"
    key                  = "integration.tfstate"
    use_oidc             = true
  }
}

provider "azurerm" {
  features {}
  use_oidc        = true
  subscription_id = var.subscription_id
}

// Configured from the AKS module's own kube_config output, so no separate `az aks get-credentials`
// step is needed between provisioning the cluster and creating the GHCR imagePullSecret in it.
provider "kubernetes" {
  host                   = module.aks.kube_config_host
  client_certificate     = base64decode(module.aks.kube_config_client_certificate)
  client_key             = base64decode(module.aks.kube_config_client_key)
  cluster_ca_certificate = base64decode(module.aks.kube_config_ca_certificate)
}
