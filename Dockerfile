# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first so restore can be cached independently of source changes.
COPY LetsEncryptManager.Console/LetsEncryptManager.Cli.csproj LetsEncryptManager.Console/
COPY LetsEncryptManager.Core/LetsEncryptManager.Core.csproj LetsEncryptManager.Core/
RUN dotnet restore LetsEncryptManager.Console/LetsEncryptManager.Cli.csproj

# Copy the remaining source and publish.
COPY LetsEncryptManager.Console/ LetsEncryptManager.Console/
COPY LetsEncryptManager.Core/ LetsEncryptManager.Core/
RUN dotnet publish LetsEncryptManager.Console/LetsEncryptManager.Cli.csproj \
    -c Release -o /app --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The CLI reads all configuration from Azure App Configuration / Key Vault at runtime
# (CertAzConfigUrl env var) and authenticates via DefaultAzureCredential (managed identity).
ENTRYPOINT ["dotnet", "LetsEncryptManager.Cli.dll"]
