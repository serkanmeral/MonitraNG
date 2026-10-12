using System.Text.RegularExpressions;

namespace MngDocument.Infrastructure.Services;

internal static class DiSearchIndex
{
    public static string Name(string? domain) => $"mng-{Slug(domain)}-di-docs";

    public static string Slug(string? value)
    {
        var slug = Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "default" : slug;
    }
}
