// NOTE: azurerm_notification_hub's gcm_credential block only exposes the legacy FCM "server key"
// (api_key) - deployment.md's checklist calls for FCM v1 (service-account) credentials instead,
// which this resource may not support yet. Verify against the current azurerm provider docs before
// applying; if FCM v1 isn't supported by the provider, the legacy server key (still issuable from
// Firebase console under Cloud Messaging API (Legacy), if enabled) is the fallback, or the hub's FCM
// credential update may need an azapi_resource / az cli step instead of azurerm_notification_hub.

resource "azurerm_notification_hub_namespace" "this" {
  name                = var.namespace_name
  resource_group_name = var.resource_group_name
  location            = var.location
  namespace_type      = "NotificationHub"
  sku_name            = var.sku_name
  tags                = var.tags
}

resource "azurerm_notification_hub" "this" {
  name                = var.hub_name
  namespace_name      = azurerm_notification_hub_namespace.this.name
  resource_group_name = var.resource_group_name
  location            = var.location

  dynamic "apns_credential" {
    for_each = var.apns_credential == null ? [] : [var.apns_credential]
    content {
      application_mode = apns_credential.value.application_mode
      bundle_id         = apns_credential.value.bundle_id
      key_id            = apns_credential.value.key_id
      team_id           = apns_credential.value.team_id
      token             = apns_credential.value.token
    }
  }

  dynamic "gcm_credential" {
    for_each = var.fcm_api_key == null ? [] : [var.fcm_api_key]
    content {
      api_key = gcm_credential.value
    }
  }
}

// Dedicated Listen+Send policy, per deployment.md's own recommendation over the namespace's
// full-access default rule.
resource "azurerm_notification_hub_authorization_rule" "listen_send" {
  name                  = "ListenSend"
  notification_hub_name = azurerm_notification_hub.this.name
  namespace_name        = azurerm_notification_hub_namespace.this.name
  resource_group_name   = var.resource_group_name

  listen = true
  send   = true
  manage = false
}
