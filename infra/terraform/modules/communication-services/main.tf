resource "azurerm_communication_service" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  data_location       = var.data_location
  tags                = var.tags
}

// Free, Azure-managed subdomain by default (no DNS verification needed) - see deployment.md's ACS
// checklist. Pass a custom domain resource ID instead once a verified custom domain is wanted.
resource "azurerm_email_communication_service" "this" {
  name                = var.email_service_name
  resource_group_name = var.resource_group_name
  data_location       = var.data_location
  tags                = var.tags
}

resource "azurerm_email_communication_service_domain" "this" {
  email_service_id  = azurerm_email_communication_service.this.id
  domain_management = var.custom_domain_management_type == null ? "AzureManaged" : var.custom_domain_management_type
}

resource "azurerm_communication_service_email_domain_association" "this" {
  communication_service_id = azurerm_communication_service.this.id
  email_service_domain_id  = azurerm_email_communication_service_domain.this.id
}
