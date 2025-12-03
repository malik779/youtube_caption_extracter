using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TranscriptService.Api.Models;
using TranscriptService.Api.Utilities;

namespace TranscriptService.Api.Services;

internal sealed class YouTubeTranscriptService : ITranscriptService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YouTubeTranscriptService> _logger;

    private static readonly Regex PlayerResponseRegex =
        new(@"ytInitialPlayerResponse\s*=\s*(\{.+?\})\s*;", RegexOptions.Singleline | RegexOptions.Compiled);

    public YouTubeTranscriptService(HttpClient httpClient, ILogger<YouTubeTranscriptService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TranscriptResponse> FetchTranscriptAsync(TranscriptRequest request, CancellationToken cancellationToken)
    {
        var videoId = YouTubeVideoIdParser.Extract(request.Url);
        var playerResponseJson = await FetchPlayerResponseAsync(videoId, cancellationToken);
        using var playerDoc = JsonDocument.Parse(playerResponseJson);
        var root = playerDoc.RootElement;
        var title = root.TryGetProperty("videoDetails", out var videoDetails) &&
                    videoDetails.TryGetProperty("title", out var titleElement)
            ? titleElement.GetString() ?? "YouTube Video"
            : "YouTube Video";

        var captionTrack = ResolveCaptionTrack(root, request.Language);
        var segments = await DownloadTranscriptAsync(captionTrack, cancellationToken);
        if (segments.Count == 0)
        {
            throw new TranscriptUnavailableException("No transcript segments were returned for this video.");
        }

        var paragraphs = TranscriptCleaner.ToParagraphs(segments);
        if (paragraphs.Count == 0)
        {
            throw new TranscriptUnavailableException("Transcript text could not be cleaned.");
        }

        var fullText = string.Join("\n\n", paragraphs);
        var trackType = captionTrack.Kind is "asr" ? "auto" : "manual";

        return new TranscriptResponse(
            videoId,
            title,
            captionTrack.LanguageCode ?? "unknown",
            trackType,
            paragraphs,
            fullText,
            DateTime.UtcNow
        );
    }

    private async Task<string> FetchPlayerResponseAsync(string videoId, CancellationToken cancellationToken)
    {
        var watchUrl = $"https://www.youtube.com/watch?v={videoId}&hl=en";
        var response = await _httpClient.GetAsync(watchUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = PlayerResponseRegex.Match(html);
        if (!match.Success)
        {
            _logger.LogWarning("Player response payload not found for video {VideoId}", videoId);
            throw new TranscriptUnavailableException("Unable to locate caption metadata for this video.");
        }

        return match.Groups[1].Value;
    }

    private static CaptionTrackInfo ResolveCaptionTrack(JsonElement playerRoot, string? preferredLanguage)
    {
        if (!playerRoot.TryGetProperty("captions", out var captionsRoot) ||
            !captionsRoot.TryGetProperty("playerCaptionsTracklistRenderer", out var listRoot) ||
            !listRoot.TryGetProperty("captionTracks", out var tracksElement))
        {
            throw new TranscriptUnavailableException("No captions are published for this video.");
        }

        var tracks = tracksElement.EnumerateArray().Select(ParseTrack).ToList();
        if (tracks.Count == 0)
        {
            throw new TranscriptUnavailableException("Caption metadata is empty.");
        }

        CaptionTrackInfo? selected = null;
        if (!string.IsNullOrWhiteSpace(preferredLanguage))
        {
            selected = tracks.FirstOrDefault(track => string.Equals(track.LanguageCode, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                selected = tracks.FirstOrDefault(track => track.IsTranslatable);
                if (selected is not null)
                {
                    var builder = new StringBuilder(selected.BaseUrl);
                    builder.Append(selected.BaseUrl.Contains('?') ? '&' : '?');
                    builder.Append("tlang=").Append(Uri.EscapeDataString(preferredLanguage));
                    selected = selected with { BaseUrl = builder.ToString() };
                }
            }
        }

        selected ??= tracks.FirstOrDefault(track => !string.Equals(track.Kind, "asr", StringComparison.OrdinalIgnoreCase));
        selected ??= tracks.First();

        return selected;
    }

    private static CaptionTrackInfo ParseTrack(JsonElement element)
    {
        var baseUrl = element.GetProperty("baseUrl").GetString();
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new TranscriptUnavailableException("Encountered caption track without a base URL.");
        }

        var lang = element.TryGetProperty("languageCode", out var langElement) ? langElement.GetString() : null;
        var kind = element.TryGetProperty("kind", out var kindElement) ? kindElement.GetString() : null;
        var isTranslatable = element.TryGetProperty("isTranslatable", out var transElement) && transElement.GetBoolean();

        return new CaptionTrackInfo(baseUrl, lang, kind ?? "", isTranslatable);
    }

    private async Task<IReadOnlyList<TranscriptSegment>> DownloadTranscriptAsync(CaptionTrackInfo track, CancellationToken cancellationToken)
    {
        var url = track.BaseUrl.Contains("fmt=", StringComparison.OrdinalIgnoreCase)
            ? track.BaseUrl
            : $"{track.BaseUrl}{(track.BaseUrl.Contains('?') ? '&' : '?')}fmt=json3";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var transcriptDoc = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

        if (!transcriptDoc.RootElement.TryGetProperty("events", out var eventsElement))
        {
            return Array.Empty<TranscriptSegment>();
        }

        var segments = new List<TranscriptSegment>(eventsElement.GetArrayLength());
        foreach (var evt in eventsElement.EnumerateArray())
        {
            if (!evt.TryGetProperty("segs", out var segsElement))
            {
                continue;
            }

            var textBuilder = new StringBuilder();
            foreach (var seg in segsElement.EnumerateArray())
            {
                if (!seg.TryGetProperty("utf8", out var utf8Element))
                {
                    continue;
                }

                var snippet = utf8Element.GetString();
                if (!string.IsNullOrEmpty(snippet))
                {
                    textBuilder.Append(snippet);
                }
            }

            var text = textBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            TimeSpan? start = null;
            if (evt.TryGetProperty("tStartMs", out var startElement) &&
                startElement.TryGetDouble(out var startMs))
            {
                start = TimeSpan.FromMilliseconds(startMs);
            }

            segments.Add(new TranscriptSegment(text, start));
        }

        return segments;
    }

    private sealed record CaptionTrackInfo(string BaseUrl, string? LanguageCode, string? Kind, bool IsTranslatable)
    {
        public string BaseUrl { get; init; } = BaseUrl;
        public string? LanguageCode { get; init; } = LanguageCode;
        public string? Kind { get; init; } = Kind;
        public bool IsTranslatable { get; init; } = IsTranslatable;
    }
}
