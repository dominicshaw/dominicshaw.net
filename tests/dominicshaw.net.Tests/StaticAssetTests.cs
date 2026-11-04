using System.Net;
using System.Text.RegularExpressions;

namespace DominicShawNet.Tests;

public sealed partial class StaticAssetTests
{
    [Fact]
    public void Static_site_contains_key_pages_and_health_probe()
    {
        var webRoot = GetWebRoot();

        Assert.Contains("dominicshaw.net", File.ReadAllText(Path.Combine(webRoot, "index.html")), StringComparison.Ordinal);
        Assert.Contains("Privacy Policy", File.ReadAllText(Path.Combine(webRoot, "priv.html")), StringComparison.Ordinal);
        Assert.Contains("TT International", File.ReadAllText(Path.Combine(webRoot, "jobs", "tt_intro.html")), StringComparison.Ordinal);
        Assert.Equal("Healthy", File.ReadAllText(Path.Combine(webRoot, "healthz")).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void Contact_script_reads_the_runtime_function_url()
    {
        var webRoot = GetWebRoot();
        var index = File.ReadAllText(Path.Combine(webRoot, "index.html"));
        var runtimeConfigPosition = index.IndexOf("js/runtime-config.js", StringComparison.Ordinal);
        var contactScriptPosition = index.IndexOf("js/contact.js", StringComparison.Ordinal);

        Assert.True(runtimeConfigPosition >= 0, "index.html does not load js/runtime-config.js.");
        Assert.True(contactScriptPosition > runtimeConfigPosition, "Runtime configuration must load before contact.js.");
        Assert.Contains("contactApiBaseUrl", File.ReadAllText(Path.Combine(webRoot, "js", "contact.js")), StringComparison.Ordinal);
    }

    [Fact]
    public void Local_static_references_match_files_with_exact_case()
    {
        var webRoot = GetWebRoot();

        var missing = new List<string>();
        var sourcePaths = Directory.EnumerateFiles(webRoot, "*", SearchOption.AllDirectories).ToArray();
        var exactRelativePaths = sourcePaths
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(webRoot, path)))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var sourcePath in sourcePaths)
        {
            var extension = Path.GetExtension(sourcePath);
            var references = extension.ToLowerInvariant() switch
            {
                ".html" => ExtractHtmlReferences(File.ReadAllText(sourcePath)),
                ".css" => ExtractCssReferences(File.ReadAllText(sourcePath)),
                ".js" => ExtractJavaScriptFragmentReferences(File.ReadAllText(sourcePath)),
                _ => []
            };

            foreach (var reference in references.Where(IsLocalFileReference))
            {
                var decoded = WebUtility.UrlDecode(reference.Split(['?', '#'], 2)[0]);
                var referenceRoot = extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
                    ? webRoot
                    : Path.GetDirectoryName(sourcePath)!;
                var candidate = decoded.StartsWith('/')
                    ? Path.Combine(webRoot, decoded.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))
                    : Path.GetFullPath(Path.Combine(referenceRoot, decoded.Replace('/', Path.DirectorySeparatorChar)));

                var candidateRelativePath = Path.GetRelativePath(webRoot, candidate);
                if (candidateRelativePath.StartsWith("..", StringComparison.Ordinal) ||
                    !exactRelativePaths.Contains(NormalizeRelativePath(candidateRelativePath)))
                {
                    missing.Add($"{Path.GetRelativePath(webRoot, sourcePath)} -> {reference}");
                }
            }
        }

        Assert.True(missing.Count == 0, "Missing or incorrectly cased static references:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    private static IEnumerable<string> ExtractHtmlReferences(string html)
    {
        var withoutComments = HtmlCommentRegex().Replace(html, string.Empty);
        return HtmlReferenceRegex().Matches(withoutComments).Select(match => match.Groups[3].Value);
    }

    private static IEnumerable<string> ExtractCssReferences(string css) =>
        CssReferenceRegex().Matches(css).Select(match => match.Groups[2].Value);

    private static IEnumerable<string> ExtractJavaScriptFragmentReferences(string javascript) =>
        JavaScriptLoadRegex().Matches(javascript).Select(match => match.Groups[2].Value);

    private static bool IsLocalFileReference(string reference) =>
        !string.IsNullOrWhiteSpace(reference) &&
        !reference.StartsWith('#') &&
        !reference.StartsWith("//", StringComparison.Ordinal) &&
        !Uri.TryCreate(reference, UriKind.Absolute, out _) &&
        !reference.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) &&
        !reference.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
        !reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRelativePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    private static string GetWebRoot()
    {
        var webRoot = Path.Combine(TestPaths.RepositoryRoot, "dominicshaw.net", "wwwroot");
        Assert.True(Directory.Exists(webRoot), $"Static web root was not found: {webRoot}");
        return webRoot;
    }
    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex("""\b(src|href)\s*=\s*(['"])(.*?)\2""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlReferenceRegex();

    [GeneratedRegex("""url\(\s*(['"]?)(.*?)\1\s*\)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssReferenceRegex();

    [GeneratedRegex("""\.load\(\s*(['"])(.*?)\1\s*\)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JavaScriptLoadRegex();
}
