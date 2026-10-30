import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestSessionsService } from './guest-sessions.service';

describe('GuestSessionsService', () => {
    let service: GuestSessionsService;
    let httpMock: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: API_BASE_URL, useValue: 'https://test.api' },
            ],
        });
        service = TestBed.inject(GuestSessionsService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => httpMock.verify());

    it('fetches a guest session using its session token', () => {
        let result: unknown;

        service.getSession('session-1', 'session-token').subscribe(response => result = response);

        const request = httpMock.expectOne('https://test.api/guest/sessions/session-1');
        expect(request.request.method).toBe('GET');
        expect(request.request.headers.has('Authorization')).toBeTrue();
        request.flush({ name: 'Alex', items: [], total: 0, isSubmitted: true });
        expect(result).toEqual({ name: 'Alex', items: [], total: 0, isSubmitted: true });
    });
});
