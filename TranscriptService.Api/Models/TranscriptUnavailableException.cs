namespace TranscriptService.Api.Models;

public sealed class TranscriptUnavailableException : Exception
{
    public TranscriptUnavailableException(string message) : base(message)
    {
    }
}
