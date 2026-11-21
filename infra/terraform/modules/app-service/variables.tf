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
