# ---------------------------------------------------------------------------
# Existing resources (looked up, not created)
# ---------------------------------------------------------------------------

data "azurerm_resource_group" "rg" {
  name = var.resource_group_name
}

data "azurerm_app_configuration" "appconfig" {
  name                = var.app_config_name
  resource_group_name = var.app_config_resource_group_name
}

data "azurerm_key_vault" "kv" {
  name                = var.key_vault_name
  resource_group_name = var.key_vault_resource_group_name
}

# ---------------------------------------------------------------------------
# Managed identity used by the job (DefaultAzureCredential -> ManagedIdentity)
# ---------------------------------------------------------------------------

resource "azurerm_user_assigned_identity" "job" {
  name                = var.identity_name
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = var.location
  tags                = var.tags
}

# ---------------------------------------------------------------------------
# Role assignments granting the identity the access the CLI needs
# ---------------------------------------------------------------------------

# Read App Configuration (key/values + Key Vault references resolved by the store).
resource "azurerm_role_assignment" "appconfig_data_reader" {
  scope                = data.azurerm_app_configuration.appconfig.id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = azurerm_user_assigned_identity.job.principal_id
}

# Key Vault access for the identity.
#
# This vault uses the *access policy* permission model (enableRbacAuthorization = false),
# so RBAC role assignments are ignored — access must be granted via an access policy.
# Secrets: Get/Set (ACME account + resolving the CloudflareKey App Config reference).
# Certificates: Get/Import (read existing cert, import renewed PFX).
resource "azurerm_key_vault_access_policy" "job" {
  key_vault_id = data.azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_key_vault.kv.tenant_id
  object_id    = azurerm_user_assigned_identity.job.principal_id

  secret_permissions      = ["Get", "List", "Set"]
  certificate_permissions = ["Get", "List", "Import"]
}

# Write TXT records to each Azure DNS zone used for DNS-01 challenges.
resource "azurerm_role_assignment" "dns_zone_contributor" {
  for_each             = toset(var.dns_zone_ids)
  scope                = each.value
  role_definition_name = "DNS Zone Contributor"
  principal_id         = azurerm_user_assigned_identity.job.principal_id
}

# ---------------------------------------------------------------------------
# Container Apps environment (+ Log Analytics for container logs)
# ---------------------------------------------------------------------------

resource "azurerm_log_analytics_workspace" "law" {
  name                = var.log_analytics_name
  resource_group_name = data.azurerm_resource_group.rg.name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_container_app_environment" "env" {
  name                       = var.environment_name
  resource_group_name        = data.azurerm_resource_group.rg.name
  location                   = var.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.law.id
  tags                       = var.tags
}

# ---------------------------------------------------------------------------
# Scheduled Container Apps Job
# ---------------------------------------------------------------------------

resource "azurerm_container_app_job" "renew" {
  name                         = var.job_name
  resource_group_name          = data.azurerm_resource_group.rg.name
  location                     = var.location
  container_app_environment_id = azurerm_container_app_environment.env.id

  replica_timeout_in_seconds = var.replica_timeout_in_seconds
  replica_retry_limit        = var.replica_retry_limit

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.job.id]
  }

  schedule_trigger_config {
    cron_expression          = var.cron_expression
    parallelism              = 1
    replica_completion_count = 1
  }

  template {
    container {
      name   = "letsencryptmanager"
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      # Azure App Configuration endpoint the CLI bootstraps from.
      env {
        name  = "CertAzConfigUrl"
        value = data.azurerm_app_configuration.appconfig.endpoint
      }

      # Tell DefaultAzureCredential which user-assigned identity to use.
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.job.client_id
      }
    }
  }

  tags = var.tags

  # Image is public, so no registry {} block is required. The role assignments
  # must exist before the job runs (not strictly before it's created), but we
  # depend on them to keep apply ordering sane.
  depends_on = [
    azurerm_role_assignment.appconfig_data_reader,
    azurerm_key_vault_access_policy.job,
  ]
}
