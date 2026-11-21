output "secret_name" {
  value = kubernetes_secret.ghcr.metadata[0].name
}
