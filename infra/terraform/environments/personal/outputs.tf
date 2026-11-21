output "notification_hub_connection_string" {
  value     = module.notification_hub.connection_string
  sensitive = true
}

output "notification_hub_name" {
  value = module.notification_hub.hub_name
}
