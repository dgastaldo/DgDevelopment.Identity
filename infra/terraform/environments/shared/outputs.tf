output "acs_connection_string" {
  value     = module.communication_services.connection_string
  sensitive = true
}

output "acs_mail_from_domain" {
  value = module.communication_services.mail_from_domain
}
