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
    // The networking module's compute_subnet_id - lets nodes reach a Private-Endpoint-only SQL
    // server. Null keeps the node pool on AKS's own default network (kubenet).
    vnet_subnet_id = var.vnet_subnet_id
  }

  // Azure CNI is required when the node pool uses a custom VNet subnet (vnet_subnet_id set) -
  // kubenet, the default, can't attach to one.
  dynamic "network_profile" {
    for_each = { for idx, s in [var.vnet_subnet_id] : idx => s if s != null }
    content {
      network_plugin = "azure"
    }
  }

  identity {
    type = "SystemAssigned"
  }
}
