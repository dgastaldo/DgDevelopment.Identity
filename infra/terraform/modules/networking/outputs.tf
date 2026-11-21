output "virtual_network_id" {
  value = azurerm_virtual_network.this.id
}

output "private_endpoint_subnet_id" {
  value = azurerm_subnet.private_endpoints.id
}

output "compute_subnet_id" {
  value = azurerm_subnet.compute.id
}

output "sql_private_dns_zone_id" {
  value = azurerm_private_dns_zone.sql.id
}
