using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace TranscriptService.Api.Utilities;

internal static partial class YouTubeVideoIdParser
{
    private const int VideoIdLength = 11;

    public static string Extract(string input)
    {
        if (!TryExtract(input, out var videoId))
        {
            throw new ArgumentException("Unable to determine a valid YouTube video id from the provided value.", nameof(input));
        }

        return videoId;
    }

    public static bool TryExtract(string? input, [NotNullWhen(true)] out string? videoId)
    {
        videoId = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (IsValidVideoId(trimmed))
        {
            videoId = trimmed;
        }
        else if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var queryValues = QueryHelpers.ParseQuery(uri.Query);
            if (queryValues.TryGetValue("v", out var vValue))
            {
                var candidate = vValue.FirstOrDefault();
                if (IsValidVideoId(candidate))
                {
                    videoId = candidate;
                }
            }

            if (videoId is null)
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    var lastSegment = segments[^1];
                    if (lastSegment.Length >= VideoIdLength)
                    {
                        var candidate = lastSegment[^VideoIdLength..];
                        if (IsValidVideoId(candidate))
                        {
                            videoId = candidate;
                        }
                    }
                }
            }
        }

        if (videoId is null)
        {
            var match = VideoIdRegex().Match(trimmed);
            if (match.Success)
            {
                var candidate = match.Groups[1].Value;
                if (IsValidVideoId(candidate))
                {
                    videoId = candidate;
                }
            }
        }

        return videoId is not null;
    }

    private static bool IsValidVideoId(string? candidate)
    {
        return !string.IsNullOrEmpty(candidate)
               && candidate.Length == VideoIdLength
               && candidate.All(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
    }

    [GeneratedRegex(@"(?:v=|/)([0-9A-Za-z_-]{11})", RegexOptions.Compiled)]
    private static partial Regex VideoIdRegex();
}
