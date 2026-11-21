variable "subscription_id" {
  type = string
}

variable "location" {
  type    = string
  default = "westeurope"
}

variable "fcm_api_key" {
  type      = string
  default   = null
  sensitive = true
}
