variable "namespace_name" {
  type        = string
  description = "Notification Hub Namespace name (globally unique)."
}

variable "hub_name" {
  type        = string
  description = "Notification Hub name within the namespace."
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "sku_name" {
  type        = string
  description = "Free or Basic - dev/personal traffic doesn't need more."
  default     = "Free"
}

variable "fcm_api_key" {
  type        = string
  description = "FCM server key / v1 credential for Android push. Null leaves Android push unconfigured (notifier no-ops). Pass via TF_VAR_fcm_api_key, never committed."
  default     = null
  sensitive   = true
}

variable "apns_credential" {
  type = object({
    application_mode = string
    bundle_id         = string
    key_id            = string
    team_id           = string
    token             = string
  })
  description = "APNs (iOS) credential. Null leaves iOS push unconfigured - not attempted yet per CONTEXT.md (no Mac/Apple Developer account)."
  default     = null
  sensitive   = true
}

variable "tags" {
  type    = map(string)
  default = {}
}
