// No ACR here: images are built once and pushed to GHCR (same registry personal/docs use), not
// duplicated into an Azure-native registry - AKS pulls from GHCR via an imagePullSecret (a
// kubernetes_secret resource created from var.ghcr_username/var.ghcr_token), same credential shape
// as the app-service module.
resource "azurerm_kubernetes_cluster" "this" {
  name                = var.cluster_name
  resource_group_name = var.resource_group_name
  location            = var.location
  dns_prefix          = var.cluster_name
  kubernetes_version  = var.kubernetes_version
  tags                = var.tags

  default_node_pool {
    name       = "default"
    node_count = var.node_count
    vm_size    = var.vm_size
  }

  identity {
    type = "SystemAssigned"
  }
}
