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

variable "private_endpoint_subnet_id" {
  type        = string
  description = "Subnet to place the server's Private Endpoint in (the networking module's private_endpoint_subnet_id output). Null falls back to public access + the AllowAzureServices firewall rule - pass this to genuinely close that off instead."
  default     = null
}

variable "private_dns_zone_id" {
  type        = string
  description = "privatelink.database.windows.net zone ID (the networking module's sql_private_dns_zone_id output) - required when private_endpoint_subnet_id is set, so the server's private IP resolves inside the VNet."
  default     = null
}
