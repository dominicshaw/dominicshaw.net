namespace DominicShawNet.Tests;

public sealed class DeploymentConfigurationTests
{
    [Fact]
    public void Infrastructure_uses_serverless_hosting_without_an_app_service_basic_plan()
    {
        var repositoryRoot = TestPaths.RepositoryRoot;
        var infrastructure = File.ReadAllText(Path.Combine(repositoryRoot, "infra", "resources.bicep"));

        Assert.Contains("Microsoft.Web/staticSites", infrastructure, StringComparison.Ordinal);
        Assert.Contains("tier: 'Free'", infrastructure, StringComparison.Ordinal);
        Assert.Contains("tier: 'FlexConsumption'", infrastructure, StringComparison.Ordinal);
        Assert.Contains("maximumInstanceCount: 1", infrastructure, StringComparison.Ordinal);
        Assert.DoesNotContain("tier: 'Basic'", infrastructure, StringComparison.Ordinal);
        Assert.Contains(
            "output staticWebAppHostname string = staticWebApp.properties.defaultHostname",
            infrastructure,
            StringComparison.Ordinal);
        Assert.Contains(
            "output functionAppHostname string = functionApp.properties.defaultHostName",
            infrastructure,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "output staticWebAppHostname string = staticWebAppUrl",
            infrastructure,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "output functionAppHostname string = 'https://${functionApp.properties.defaultHostName}'",
            infrastructure,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Infrastructure_routes_the_website_to_static_web_apps_without_changing_mail()
    {
        var repositoryRoot = TestPaths.RepositoryRoot;
        var infrastructure = File.ReadAllText(Path.Combine(repositoryRoot, "infra", "resources.bicep"));

        Assert.Contains("resource legacyWildcardARecord 'Microsoft.Network/dnsZones/A", infrastructure);
        Assert.Contains("resource apexAliasRecord 'Microsoft.Network/dnsZones/A", infrastructure);
        Assert.Contains("targetResource:", infrastructure);
        Assert.Contains("id: staticWebApp.id", infrastructure);
        Assert.Contains("resource wwwCnameRecord 'Microsoft.Network/dnsZones/CNAME", infrastructure);
        Assert.Contains("cname: staticWebApp.properties.defaultHostname", infrastructure);
        Assert.Contains("resource legacyMailCnameRecord 'Microsoft.Network/dnsZones/CNAME", infrastructure);
        Assert.Contains("resource legacyMxRecord 'Microsoft.Network/dnsZones/MX", infrastructure);
        Assert.Contains("resource legacySpfRecord 'Microsoft.Network/dnsZones/TXT", infrastructure);
        Assert.Equal(1, infrastructure.Split("ipv4Address: '45.58.159.45'").Length - 1);
        Assert.Contains("cname: 'mail5009.site4now.net'", infrastructure);
        Assert.Contains("exchange: 'igw09.site4now.net'", infrastructure);
        Assert.Contains("v=spf1 a mx include:_spf.site4now.net ?all", infrastructure);
        Assert.Contains("_7botitch7tg8rqppk2ls4tcps54xwyf", infrastructure);
        Assert.Contains("resource wwwValidationRecord 'Microsoft.Network/dnsZones/TXT", infrastructure);
        Assert.Contains("_y6ddfjxbnf29zr3yldll1rlrnx03bl0", infrastructure);
    }

    [Fact]
    public void Workflow_uses_oidc_and_deploys_both_serverless_artifacts()
    {
        var repositoryRoot = TestPaths.RepositoryRoot;
        var workflowPath = Path.Combine(
            repositoryRoot,
            ".github",
            "workflows",
            "azure-static-and-function.yml");
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("id-token: write", workflow, StringComparison.Ordinal);
        Assert.Contains("azure/login@", workflow, StringComparison.Ordinal);
        Assert.Contains("Azure/functions-action@", workflow, StringComparison.Ordinal);
        Assert.Contains("Azure/static-web-apps-deploy@", workflow, StringComparison.Ordinal);
        Assert.Contains("az staticwebapp secrets list", workflow, StringComparison.Ordinal);
        Assert.Contains("node --check .artifacts/site/js/runtime-config.js", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("publish-profile", workflow, StringComparison.OrdinalIgnoreCase);

        var functionUploadStart = workflow.IndexOf("- name: Upload Function artifact", StringComparison.Ordinal);
        var staticUploadStart = workflow.IndexOf("- name: Upload static-site artifact", StringComparison.Ordinal);
        Assert.True(functionUploadStart >= 0 && staticUploadStart > functionUploadStart);
        Assert.Contains(
            "include-hidden-files: true",
            workflow[functionUploadStart..staticUploadStart],
            StringComparison.Ordinal);

        var functionDownloadPosition = workflow.IndexOf("- name: Download Function artifact", StringComparison.Ordinal);
        var functionArtifactGuardPosition = workflow.IndexOf(
            "test -f .artifacts/function/.azurefunctions/Microsoft.Azure.WebJobs.Extensions.FunctionMetadataLoader.dll",
            StringComparison.Ordinal);
        var functionDeployPosition = workflow.IndexOf("- name: Deploy Function App", StringComparison.Ordinal);
        Assert.True(
            functionDownloadPosition >= 0 &&
            functionArtifactGuardPosition > functionDownloadPosition &&
            functionDeployPosition > functionArtifactGuardPosition,
            "The downloaded Function artifact must be verified before deployment.");

        var actionReferences = workflow
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("uses: ", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(actionReferences);
        Assert.All(
            actionReferences,
            reference => Assert.Matches(
                "^uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40} # v[0-9][A-Za-z0-9_.-]*$",
                reference));

        Assert.Contains(
            "if: github.ref == 'refs/heads/master' && (github.event_name == 'push' || github.event_name == 'workflow_dispatch')",
            workflow,
            StringComparison.Ordinal);
    }
}
