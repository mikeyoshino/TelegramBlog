namespace TeleGramBlog.Core;
public static class LinkExtractor
{
    public static IReadOnlyList<string> Extract(string text, IEnumerable<string>? entityLinks = null)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"https?://[^\s<>""']+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return (entityLinks ?? []).Concat(matches.Select(m=>m.Value.TrimEnd('.',',',';',')',']')))
            .Where(IsSafe).Select(s=>new Uri(s).AbsoluteUri).Distinct(StringComparer.Ordinal).ToArray();
    }
    public static bool IsSafe(string value) => Uri.TryCreate(value,UriKind.Absolute,out var uri)
        && (uri.Scheme == "https" || uri.Scheme == "http") && !string.IsNullOrWhiteSpace(uri.Host);
}
