import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap } from 'rxjs';

const safeMethods = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);

export const csrfInterceptor: HttpInterceptorFn = (request, next) => {
  if (safeMethods.has(request.method) || request.url.startsWith('/auth/')) {
    return next(request);
  }

  const http = inject(HttpClient);
  return http.get<{ requestToken: string }>('/auth/antiforgery', { withCredentials: true }).pipe(
    switchMap(({ requestToken }) => next(request.clone({
      withCredentials: true,
      setHeaders: { 'X-CSRF-TOKEN': requestToken }
    })))
  );
};
