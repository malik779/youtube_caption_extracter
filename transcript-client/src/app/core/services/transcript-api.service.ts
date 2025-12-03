import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TranscriptRequest, TranscriptResponse } from '../models/transcript.model';
import { TRANSCRIPT_API_BASE_URL } from '../config/api-tokens';

@Injectable({ providedIn: 'root' })
export class TranscriptApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(TRANSCRIPT_API_BASE_URL);

  fetchTranscript(payload: TranscriptRequest): Observable<TranscriptResponse> {
    return this.http.post<TranscriptResponse>(`${this.baseUrl}/transcripts`, payload);
  }
}
