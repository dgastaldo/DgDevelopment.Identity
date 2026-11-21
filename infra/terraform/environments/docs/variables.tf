variable "subscription_id" {
  type = string
}

variable "location" {
  type    = string
  default = "westeurope"
}

variable "sql_admin_login" {
  type    = string
  default = "identityadmin"
}

variable "sql_admin_password" {
  type      = string
  sensitive = true
}

variable "ghcr_username" {
  type = string
}

variable "ghcr_token" {
  type      = string
  sensitive = true
}

variable "fcm_api_key" {
  type      = string
  default   = null
  sensitive = true
}
