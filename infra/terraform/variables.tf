# ---------------------------------------------------------------------------
# Subscription / location
# ---------------------------------------------------------------------------

variable "subscription_id" {
  type        = string
  description = "Azure subscription ID that hosts the Container Apps Job, environment, and managed identity."
}

variable "location" {
  type        = string
  description = "Azure region for the new resources (Container Apps environment, job, identity, Log Analytics)."
  default     = "eastus"
}

variable "resource_group_name" {
  type        = string
  description = "Name of the EXISTING resource group the new resources will be created in."
}

# ---------------------------------------------------------------------------
# Existing dependencies the job's identity needs access to
# ---------------------------------------------------------------------------

variable "app_config_name" {
  type        = string
  description = "Name of the existing Azure App Configuration store the CLI reads from (CertAzConfigUrl)."
}

variable "app_config_resource_group_name" {
  type        = string
  description = "Resource group of the existing Azure App Configuration store."
}

variable "key_vault_name" {
  type        = string
  description = "Name of the existing Key Vault where certificates and ACME account secrets are stored."
}

variable "key_vault_resource_group_name" {
  type        = string
  description = "Resource group of the existing Key Vault."
}

variable "dns_zone_ids" {
  type        = list(string)
  description = <<-EOT
    Full resource IDs of the Azure DNS zones the identity may write TXT records to (DNS-01 challenges).
    Each gets a "DNS Zone Contributor" role assignment. Zones may live in other resource groups or
    subscriptions; provide the full ID, e.g.
    /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/dnszones/example.com
    Leave empty ([]) if all certs use Cloudflare only.
  EOT
  default     = []
}

# ---------------------------------------------------------------------------
# Container image (public GHCR image)
# ---------------------------------------------------------------------------

variable "image" {
  type        = string
  description = "Fully qualified container image reference to run."
  default     = "ghcr.io/ronbrogan/letsencryptmanager:latest"
}

# ---------------------------------------------------------------------------
# Schedule
# ---------------------------------------------------------------------------

variable "cron_expression" {
  type        = string
  description = "Cron schedule (UTC) for the renewal job. Default: daily at 05:00 UTC."
  default     = "0 5 * * *"
}

# ---------------------------------------------------------------------------
# Resource names (override if you have a naming convention)
# ---------------------------------------------------------------------------

variable "identity_name" {
  type        = string
  description = "Name for the user-assigned managed identity used by the job."
  default     = "id-letsencryptmanager"
}

variable "environment_name" {
  type        = string
  description = "Name for the Container Apps managed environment."
  default     = "cae-letsencryptmanager"
}

variable "job_name" {
  type        = string
  description = "Name for the Container Apps Job."
  default     = "caj-letsencryptmanager"
}

variable "log_analytics_name" {
  type        = string
  description = "Name for the Log Analytics workspace backing the Container Apps environment."
  default     = "log-letsencryptmanager"
}

# ---------------------------------------------------------------------------
# Job runtime sizing / behavior
# ---------------------------------------------------------------------------

variable "cpu" {
  type        = number
  description = "vCPU allocated to the job container. Must pair with a valid memory value (e.g. 0.5 CPU / 1Gi)."
  default     = 0.5
}

variable "memory" {
  type        = string
  description = "Memory allocated to the job container (e.g. \"1Gi\")."
  default     = "1Gi"
}

variable "replica_timeout_in_seconds" {
  type        = number
  description = "Max seconds a job replica may run before being terminated. Generous to allow DNS propagation waits."
  default     = 1800
}

variable "replica_retry_limit" {
  type        = number
  description = "Number of retries for a failed replica before the execution is marked failed."
  default     = 1
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to created resources."
  default     = {}
}
