namespace TranscriptService.Api.Models;

internal sealed record TranscriptSegment(string Text, TimeSpan? StartTime);
