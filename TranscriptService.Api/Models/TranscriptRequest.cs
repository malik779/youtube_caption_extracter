using System.ComponentModel.DataAnnotations;

namespace TranscriptService.Api.Models;

public sealed record TranscriptRequest
{
    [Required]
    [Url]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Optional ISO language code (e.g. en, es) to target when multiple caption tracks exist.
    /// </summary>
    [RegularExpression("^[a-zA-Z-]{2,10}$", ErrorMessage = "Language codes should follow ISO-639 format.")]
    public string? Language { get; init; }
}
