variable "namespace" {
  type    = string
  default = "default"
}

variable "ghcr_username" {
  type = string
}

variable "ghcr_token" {
  type      = string
  sensitive = true
}
