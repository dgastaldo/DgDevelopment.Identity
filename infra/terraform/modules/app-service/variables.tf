variable "name_prefix" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "sku_name" {
  type        = string
  description = "e.g. \"B1\" for docs' low-traffic public doc site."
  default     = "B1"
}

variable "server_image" {
  type        = string
  description = "e.g. \"dgastaldo/dgdevelopment-identity-server:docs-latest\""
}

variable "identity_platform_image" {
  type = string
}

variable "ghcr_username" {
  type = string
}

variable "ghcr_token" {
  type      = string
  sensitive = true
}

variable "server_app_settings" {
  type    = map(string)
  default = {}
}

variable "identity_platform_app_settings" {
  type    = map(string)
  default = {}
}

variable "tags" {
  type    = map(string)
  default = {}
}

variable "vnet_integration_subnet_id" {
  type        = string
  description = "The networking module's delegated compute_subnet_id - required for this site to reach a Private-Endpoint-only SQL server. Null keeps the site without VNet Integration (can only reach a publicly-accessible SQL server)."
  default     = null
}
