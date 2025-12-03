using Microsoft.AspNetCore.Mvc;
using TranscriptService.Api.Models;
using TranscriptService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddHttpClient<ITranscriptService, YouTubeTranscriptService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TranscriptDownloader/1.0 (+https://github.com)");
    client.Timeout = TimeSpan.FromSeconds(20);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/transcripts", async Task<IResult> ([FromBody] TranscriptRequest request, ITranscriptService transcriptService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Url))
    {
        return Results.BadRequest(new { error = "A YouTube URL is required." });
    }

    try
    {
        var transcript = await transcriptService.FetchTranscriptAsync(request, cancellationToken);
        return Results.Ok(transcript);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (TranscriptUnavailableException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (HttpRequestException ex)
    {
        app.Logger.LogError(ex, "HTTP error retrieving captions from YouTube.");
        return Results.Problem(title: "Unable to reach YouTube", detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
