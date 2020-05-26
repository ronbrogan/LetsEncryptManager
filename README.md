# LetsEncryptManager

LetsEncryptManager is an opinionated ACME automation project intended for use on the Azure platform. Based on [ACMESharpCore](https://github.com/PKISharp/ACMESharpCore)

 - It is config driven (via Azure App Configuration) and by default never stores private keys of certs, except for the final push into an Azure KeyVault instance. 
 - It automatically handles DNS-01 challenges via creating/modifying record sets in an Azure DNS Zone, which is discovered based on the TXT record's fully qualified name.

The host application is an Azure Function app that runs every hour (could be daily, weekly, without real harm done)
There's also a function that is an HTTP trigger to allow on-demand certificate creation/renewal. (Just empty get/post to the endpoint will trigger the same evalution logic as the timer trigger)
The function app uses managed identity to authenticate to other Azure Services via `AzureDefaultCredential` and the appropriate environment variables

There's also a console app project that is setup for the same process. When running locally, it will use your Visual Studio Azure Auth, Az CLI auth, or whatever other providers they have in the Azure.Identity `DefaultAzureCredential` 

The identity will need the ability to 
 - Read the AppConfiguration
 - Read/write secrets and certificates in the KeyVault
 - Write to the Azure DNS Zones for the certs

## Theory of Operation
 - Read App Configuration for CertDefinitions (key,value pairs of `(string certId, string hostnames)`)
    - `certID` is the unique name of the cert in KeyVault - must abide by KeyVault naming restrictions
    - `hostnames` is a comma-separated list of the DNS hostnames for the cert. The first will be the SubjectName, others will be Subject Alternative Names, ex `*.example.com,example.com` for a wildcard and toplevel cert for example.com
 - For each CertDefinition obtained, get the certificate info from KeyVault. A new certificate will be requested if any of the following conditions are true:
    - No certificate exists in the vault
    - The certificate is nearing expiration (10 day threshold currently)
    - The certificate subject name is different than the first hostname in the config
    - The certificate is missing any hostname in the config
 - If a cert is requested, an ACME account will be retrieved from KeyVault secrets or created (and subsequently stored in KeyVault for future use)
 - An order is created for the hostnames specified, challenges completed via the DNS provider, and the order completed
 - Public certificate is downloaded from the ACME CA, combined with the local private key, and exported as PFX to KeyVault

## Limitations
 - There is no order storage. If challenges fail (due to misconfiguration, network issues, etc) that order is abandoned. It's expected the next run of the tool will succeed.
 - Only DNS-01 challenges are supported
 - Only KeyVault and local-file system providers (for account/certificate storage) are included, only KeyVault provider is recommended for live certificates.