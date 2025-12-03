namespace TranscriptService.Api.Models;

public sealed record TranscriptResponse(
    string VideoId,
    string Title,
    string SourceLanguage,
    string TrackType,
    IReadOnlyList<string> Paragraphs,
    string FullText,
    DateTime RetrievedAt
);
