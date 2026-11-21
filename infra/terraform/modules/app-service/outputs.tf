output "server_hostname" {
  value = azurerm_linux_web_app.server.default_hostname
}

output "identity_platform_hostname" {
  value = azurerm_linux_web_app.identity_platform.default_hostname
}
