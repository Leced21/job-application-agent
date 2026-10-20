import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';

export interface AccountSession {
  isAuthenticated: boolean;
  email: string | null;
  registrationEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);

  session(): Observable<AccountSession> {
    return this.http.get<AccountSession>('/auth/session', { withCredentials: true });
  }

  login(email: string, password: string, rememberMe: boolean): Observable<AccountSession> {
    return this.withAntiforgery(token => this.http.post<AccountSession>('/auth/login',
      { email, password, rememberMe }, { headers: this.csrfHeaders(token), withCredentials: true }));
  }

  register(email: string, password: string): Observable<AccountSession> {
    return this.withAntiforgery(token => this.http.post<AccountSession>('/auth/register',
      { email, password }, { headers: this.csrfHeaders(token), withCredentials: true }));
  }

  logout(): Observable<void> {
    return this.withAntiforgery(token => this.http.post<void>('/auth/logout', {},
      { headers: this.csrfHeaders(token), withCredentials: true }));
  }

  private withAntiforgery<T>(action: (token: string) => Observable<T>): Observable<T> {
    return this.http.get<{ requestToken: string }>('/auth/antiforgery', { withCredentials: true })
      .pipe(switchMap(({ requestToken }) => action(requestToken)));
  }

  private csrfHeaders(token: string): HttpHeaders {
    return new HttpHeaders({ 'X-CSRF-TOKEN': token });
  }
}
