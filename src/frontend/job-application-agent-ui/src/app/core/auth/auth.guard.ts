import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthApiService } from './auth-api.service';

export const requireSession: CanActivateFn = (_, state) => {
  const auth = inject(AuthApiService);
  const router = inject(Router);
  return auth.session().pipe(
    map(session => session.isAuthenticated || router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })),
    catchError(() => of(router.createUrlTree(['/login'])))
  );
};
