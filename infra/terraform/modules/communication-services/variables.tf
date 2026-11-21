variable "name" {
  type = string
}

variable "email_service_name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "data_location" {
  type        = string
  description = "ACS data residency region, e.g. \"Europe\"."
  default     = "Europe"
}

variable "custom_domain_management_type" {
  type        = string
  description = "Null uses the free AzureManaged subdomain. Set to \"CustomerManaged\" once a verified custom domain (TXT/DKIM/SPF at your registrar) is wanted - see deployment.md's ACS checklist."
  default     = null
}

variable "tags" {
  type    = map(string)
  default = {}
}
