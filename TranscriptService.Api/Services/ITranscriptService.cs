using TranscriptService.Api.Models;

namespace TranscriptService.Api.Services;

public interface ITranscriptService
{
    Task<TranscriptResponse> FetchTranscriptAsync(TranscriptRequest request, CancellationToken cancellationToken);
}
