variable "server_name" {
  type        = string
  description = "Logical SQL server name (globally unique)."
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "administrator_login" {
  type = string
}

variable "administrator_login_password" {
  type        = string
  sensitive   = true
  description = "Pass via TF_VAR_sql_admin_password, never committed. Consider migrating to Azure AD-only auth later."
}

variable "sku_name" {
  type        = string
  description = "e.g. \"Basic\", \"S0\", \"GP_S_Gen5_1\" (serverless). Start cheap, size up per environment."
  default     = "Basic"
}

variable "max_size_gb" {
  type    = number
  default = 2
}

variable "tags" {
  type    = map(string)
  default = {}
}
