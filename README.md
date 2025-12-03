# YouTube Transcript Downloader

A full-stack reference implementation that turns any public YouTube link into clean, downloadable transcripts. The Angular front end accepts a URL, and the .NET 9 minimal API fetches captions from YouTube's public timed-text endpoints, merges the segments, scrubs timestamps/tags, and returns readable paragraphs that can be exported as `.txt` or `.pdf`.

## Tech stack

- **Frontend:** Angular 17 standalone app (`transcript-client`), Reactive Forms, jsPDF, FileSaver.
- **Backend:** ASP.NET Core 9 minimal API (`TranscriptService.Api`) with `HttpClientFactory` and strongly-typed services.
- **Cross-cutting:** Clean service boundaries, shared DI tokens, proxy configuration for local dev.

## Local development

### Prerequisites

- Node.js 18+ / npm 9+
- .NET 9 SDK (installed locally via `dotnet-install.sh` in this repo)

### Backend API

```bash
cd TranscriptService.Api
~/.dotnet/dotnet run
```

The API listens on `http://localhost:5196` (HTTPS on `https://localhost:7299`).

### Frontend

```bash
cd transcript-client
npm install
npm start
```

`npm start` uses `proxy.conf.json` so `/api` requests are forwarded to `http://localhost:5196`. Visit `http://localhost:4200` to use the UI.

## API contract

- **Endpoint:** `POST /api/transcripts`
- **Body:** `{ "url": "https://www.youtube.com/watch?v=...", "language": "en" }` (language optional ISO code)
- **Response:**
  ```json
  {
    "videoId": "dQw4w9WgXcQ",
    "title": "Video title",
    "sourceLanguage": "en",
    "trackType": "manual", // or "auto"
    "paragraphs": ["Clean paragraph 1", "Paragraph 2"],
    "fullText": "Paragraph 1\n\nParagraph 2",
    "retrievedAt": "2025-12-03T22:00:00Z"
  }
  ```
- **Errors:** 400 for invalid URLs, 404 when captions are unavailable, 503 for upstream YouTube issues.

## Implementation details

1. **Video ID extraction** — Reusable utility handles `watch`, `short`, `embed`, and raw IDs.
2. **Caption discovery** — The backend scrapes `ytInitialPlayerResponse`, selects manual captions when possible, falls back to auto (`kind === "asr"`), and supports translated tracks via `tlang`.
3. **Transcript merging** — Caption events fetched as `fmt=json3` JSON, segments stitched together, timestamps and bracketed tags (`[Music]`, `[Applause]`, etc.) removed, whitespace normalized, and paragraphs batched for readability.
4. **Downloads** — The Angular UI lets users export raw text or generate a PDF via jsPDF with automatic line wrapping.

## Testing

- `~/.dotnet/dotnet build TranscriptService.Api/TranscriptService.Api.csproj`
- `cd transcript-client && npm run build`
- `npm run test -- --watch=false` (requires Chrome; set `CHROME_BIN` in CI or install Chromium locally)

## Next steps

- Add caching for frequent requests
- Introduce persistence for saved transcripts or transcript history
- Expand cleaning rules (custom stop phrases, profanity filters) as needed
