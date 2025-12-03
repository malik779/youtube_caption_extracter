export interface TranscriptRequest {
  url: string;
  language?: string | null;
}

export interface TranscriptResponse {
  videoId: string;
  title: string;
  sourceLanguage: string;
  trackType: 'manual' | 'auto' | string;
  paragraphs: string[];
  fullText: string;
  retrievedAt: string;
}
