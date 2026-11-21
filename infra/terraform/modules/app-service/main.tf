resource "azurerm_service_plan" "this" {
  name                = "${var.name_prefix}-plan"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = var.sku_name
  tags                = var.tags
}

// Two sites, one plan - Server + IdentityPlatform (App Service hosts public websites only, no
// arbitrary sidecar containers, per deployment.md's "Aspire-native deployment model" note).
// GHCR has no native Azure AD/managed-identity federation (unlike ACR) - App Service pulls from it
// with a plain username/PAT registry credential, same GHCR PAT the AKS module's imagePullSecret
// uses. DOCKER_REGISTRY_SERVER_* app settings are App Service's documented mechanism for a
// non-ACR container registry.
resource "azurerm_linux_web_app" "server" {
  name                = "${var.name_prefix}-server"
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.this.id
  tags                = var.tags

  site_config {
    application_stack {
      docker_image_name   = var.server_image
      docker_registry_url = "https://ghcr.io"
    }
  }

  app_settings = merge(var.server_app_settings, {
    "WEBSITES_PORT"                   = "8080"
    "DOCKER_REGISTRY_SERVER_URL"      = "https://ghcr.io"
    "DOCKER_REGISTRY_SERVER_USERNAME" = var.ghcr_username
    "DOCKER_REGISTRY_SERVER_PASSWORD" = var.ghcr_token
  })

  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_linux_web_app" "identity_platform" {
  name                = "${var.name_prefix}-identity-platform"
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.this.id
  tags                = var.tags

  site_config {
    application_stack {
      docker_image_name   = var.identity_platform_image
      docker_registry_url = "https://ghcr.io"
    }
  }

  app_settings = merge(var.identity_platform_app_settings, {
    "WEBSITES_PORT"                   = "8080"
    "DOCKER_REGISTRY_SERVER_URL"      = "https://ghcr.io"
    "DOCKER_REGISTRY_SERVER_USERNAME" = var.ghcr_username
    "DOCKER_REGISTRY_SERVER_PASSWORD" = var.ghcr_token
  })

  identity {
    type = "SystemAssigned"
  }
}
