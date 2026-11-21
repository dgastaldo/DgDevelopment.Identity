variable "cluster_name" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "kubernetes_version" {
  type        = string
  description = "Pin explicitly rather than leaving it to the provider's default, so an upgrade is a deliberate plan/apply, not a surprise on the next unrelated change."
}

variable "node_count" {
  type    = number
  default = 1
}

variable "vm_size" {
  type    = string
  default = "Standard_B2s"
}

variable "tags" {
  type    = map(string)
  default = {}
}
