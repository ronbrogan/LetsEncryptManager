output "identity_principal_id" {
  description = "Object/principal ID of the user-assigned identity (use to grant additional access manually if needed)."
  value       = azurerm_user_assigned_identity.job.principal_id
}

output "identity_client_id" {
  description = "Client ID of the user-assigned identity (passed to the container as AZURE_CLIENT_ID)."
  value       = azurerm_user_assigned_identity.job.client_id
}

output "job_name" {
  description = "Name of the Container Apps Job. Trigger an on-demand run with: az containerapp job start -n <job> -g <rg>."
  value       = azurerm_container_app_job.renew.name
}

output "container_app_environment_id" {
  description = "Resource ID of the Container Apps environment."
  value       = azurerm_container_app_environment.env.id
}

output "image" {
  description = "Container image the job runs."
  value       = var.image
}
