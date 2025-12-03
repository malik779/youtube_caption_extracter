import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AppComponent } from './app.component';
import { TranscriptApiService } from './core/services/transcript-api.service';
import { TRANSCRIPT_API_BASE_URL } from './core/config/api-tokens';

class TranscriptApiServiceStub {
  fetchTranscript = jasmine.createSpy('fetchTranscript').and.returnValue(of());
}

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: TranscriptApiService, useClass: TranscriptApiServiceStub },
        { provide: TRANSCRIPT_API_BASE_URL, useValue: '/api' }
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should keep the form invalid until a url is provided', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.form.valid).toBeFalse();
    app.form.patchValue({ url: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ' });
    expect(app.form.valid).toBeTrue();
  });
});
