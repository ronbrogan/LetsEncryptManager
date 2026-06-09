# LetsEncryptManager — Azure deployment (Terraform)

Provisions a scheduled **Azure Container Apps Job** that runs the `LetsEncryptManager.Cli`
image (from GHCR) once daily to renew certificates.

## What this creates

| Resource | Purpose |
| --- | --- |
| User-assigned managed identity | Identity the job runs as (`DefaultAzureCredential`). |
| Role assignments | `App Configuration Data Reader`, `Key Vault Certificates Officer`, `Key Vault Secrets Officer`, and `DNS Zone Contributor` (per zone). |
| Log Analytics workspace | Backs the Container Apps environment; holds container logs. |
| Container Apps environment | Hosts the job. |
| Container Apps Job | Cron-scheduled (default `0 5 * * *` UTC), pulls the public GHCR image, runs once, exits. |

The existing **resource group**, **App Configuration store**, **Key Vault**, and **DNS zones**
are referenced (data sources), not created.

## Prerequisites

1. The container image has been built and pushed to GHCR by the
   `.github/workflows/build-push-image.yml` workflow, and the GHCR package is set to **public**
   (Repo → Packages → the package → Package settings → Change visibility → Public).
2. Azure CLI logged in (`az login`) with rights to create the resources above **and** to create
   role assignments on the App Config / Key Vault / DNS zones (Owner or User Access Administrator
   on those scopes). If DNS zones live in another subscription, you need those rights there too.
3. The `Microsoft.App` and `Microsoft.OperationalInsights` resource providers registered in the
   subscription:
   ```
   az provider register --namespace Microsoft.App
   az provider register --namespace Microsoft.OperationalInsights
   ```
4. **Key Vault must use Azure RBAC** for its permission model (this config grants RBAC roles). If
   your vault still uses access policies, either switch it to RBAC, or tell us and we'll swap the
   role assignments for an `access_policy` block.

## Usage

```sh
cd infra/terraform
cp terraform.tfvars.example terraform.tfvars   # then edit values
terraform init
terraform plan
terraform apply
```

### Run on demand (outside the schedule)

```sh
az containerapp job start -n <job_name> -g <resource_group_name>
```

### View logs

Logs land in the Log Analytics workspace (`ContainerAppConsoleLogs_CL`), or:

```sh
az containerapp job execution list -n <job_name> -g <resource_group_name> -o table
```

## Notes

- `AZURE_CLIENT_ID` is injected into the container so `DefaultAzureCredential` selects the
  user-assigned identity rather than guessing.
- All app configuration (contact email, CA URL, cert definitions, Cloudflare token, etc.) comes
  from App Configuration / Key Vault at runtime — only `CertAzConfigUrl` and `AZURE_CLIENT_ID` are
  set as env vars here.
- The Cloudflare token is read from App Configuration (`ManagerConfig:CloudflareKey`), so no extra
  Azure role is needed for Cloudflare-validated certs — only Azure DNS zones need role assignments.

## Switching to remote state (optional)

Replace the comment in `versions.tf` with a backend block:

```hcl
terraform {
  backend "azurerm" {
    resource_group_name  = "tfstate-rg"
    storage_account_name = "<unique>"
    container_name       = "tfstate"
    key                  = "letsencryptmanager.tfstate"
  }
}
```

then `terraform init -migrate-state`.
