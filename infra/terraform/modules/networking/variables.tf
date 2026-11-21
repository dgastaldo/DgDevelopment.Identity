variable "name_prefix" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "address_space" {
  type    = string
  default = "10.10.0.0/16"
}

variable "private_endpoint_subnet_prefix" {
  type    = string
  default = "10.10.1.0/24"
}

variable "compute_subnet_prefix" {
  type    = string
  default = "10.10.2.0/24"
}

variable "compute_subnet_delegation" {
  type = object({
    service_name = string
    actions      = list(string)
  })
  description = "Set for App Service VNet Integration (docs): { service_name = \"Microsoft.Web/serverFarms\", actions = [\"Microsoft.Network/virtualNetworks/subnets/action\"] }. Leave null for AKS (integration/production), which doesn't delegate its subnet."
  default     = null
}

variable "tags" {
  type    = map(string)
  default = {}
}
