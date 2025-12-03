using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TranscriptService.Api.Models;

namespace TranscriptService.Api.Utilities;

internal static partial class TranscriptCleaner
{
    private const int DefaultParagraphTarget = 900;

    public static IReadOnlyList<string> ToParagraphs(IEnumerable<TranscriptSegment> segments, int paragraphTargetLength = DefaultParagraphTarget)
    {
        if (segments is null)
        {
            return Array.Empty<string>();
        }

        var paragraphs = new List<string>();
        var builder = new StringBuilder();

        foreach (var segment in segments)
        {
            var cleaned = CleanFragment(segment.Text);
            if (string.IsNullOrEmpty(cleaned))
            {
                continue;
            }

            if (builder.Length + cleaned.Length >= paragraphTargetLength)
            {
                paragraphs.Add(builder.ToString().Trim());
                builder.Clear();
            }

            builder.Append(cleaned);
            builder.Append(' ');
        }

        if (builder.Length > 0)
        {
            paragraphs.Add(builder.ToString().Trim());
        }

        return paragraphs;
    }

    private static string CleanFragment(string? fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(fragment);
        var normalized = (decoded ?? string.Empty)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\u200b", string.Empty, StringComparison.Ordinal);

        normalized = TagTokenRegex().Replace(normalized, string.Empty);
        normalized = TimestampRegex().Replace(normalized, string.Empty);
        normalized = MultipleWhitespaceRegex().Replace(normalized, " ");

        return normalized.Trim();
    }

    [GeneratedRegex(@"\[(?:[a-zA-Z ]{1,30})\]", RegexOptions.Compiled)]
    private static partial Regex TagTokenRegex();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}(?::\d{2})?\b", RegexOptions.Compiled)]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleWhitespaceRegex();
}
