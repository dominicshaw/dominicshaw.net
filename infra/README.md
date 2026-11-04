# Azure infrastructure

`main.bicep` creates the phase-one production resources: a Free Azure Static Web App, a .NET 10 Flex Consumption Function App in UK South, identity-only Function storage, Application Insights, Azure Communication Services email, and a GitHub OIDC deployment identity restricted to the repository's `production` environment. The Static Web Apps control resource is in West Europe while its content is served globally.

The template creates the Azure DNS zone, routes the apex domain to the Static Web App through an Azure alias, and routes `www` through a CNAME. It retains the DiscountASP wildcard, mail CNAME, MX, and SPF records during the post-cutover soak period, so the existing mail service continues to operate independently of the website.

Deploy from the repository root with the authenticated Azure CLI:

```powershell
az provider register --namespace Microsoft.Network --wait
az provider register --namespace Microsoft.ManagedIdentity --wait
az deployment sub create `
  --name dominicshaw-production `
  --location uksouth `
  --template-file infra/main.bicep
```

The outputs provide both generated HTTPS hostnames, the managed Azure Communication Services sender, and the unused Azure DNS nameservers. Application deployment is handled by `.github/workflows/azure-static-and-function.yml` after the GitHub OIDC identity and repository variables are configured.

The static deployment artifact receives only the public Function URL in `js/runtime-config.js`. Azure credentials and the Communication Services endpoint remain in the Function App. Function CORS permits the generated Static Web Apps hostname plus the future apex and `www` hostnames; it is a browser policy, while honeypot and rate limiting remain the abuse controls.

The apex and `www` ownership-validation TXT records are retained for managed-certificate operation. Static Web Apps custom-domain bindings and the registrar nameserver delegation are one-time control-plane operations outside the template. Keep the DiscountASP mail records and hosting account active until every forwarded address has been recreated and externally tested at the replacement mail provider.
