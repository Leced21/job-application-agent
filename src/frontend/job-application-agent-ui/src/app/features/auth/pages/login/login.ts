import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { RouterLink } from '@angular/router';
import { AuthApiService } from '../../../../core/auth/auth-api.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class LoginPage {
  private readonly auth = inject(AuthApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  email = '';
  password = '';
  rememberMe = false;
  isRegistering = false;
  busy = false;
  readonly error = signal('');
  readonly registrationEnabled = signal(false);

  constructor() {
    this.auth.session().subscribe({
      next: session => {
        this.registrationEnabled.set(session.registrationEnabled);
        if (session.isAuthenticated) this.navigateAfterLogin();
      },
      error: () => this.error.set('Le service de comptes est indisponible. Configurez la base Identity du Gateway.')
    });
  }

  submit(): void {
    if (this.busy) return;
    this.busy = true;
    this.error.set('');
    const operation = this.isRegistering
      ? this.auth.register(this.email, this.password)
      : this.auth.login(this.email, this.password, this.rememberMe);
    operation.subscribe({
      next: () => this.navigateAfterLogin(),
      error: (error: HttpErrorResponse) => {
        this.busy = false;
        this.error.set(this.messageFor(error));
      }
    });
  }

  toggleMode(): void {
    this.isRegistering = !this.isRegistering;
    this.error.set('');
  }

  private navigateAfterLogin(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    const safeReturnUrl = returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/dashboard';
    void this.router.navigateByUrl(safeReturnUrl);
  }

  private messageFor(error: HttpErrorResponse): string {
    if (error.status === 401) return 'Adresse e-mail ou mot de passe invalide.';
    if (error.status === 400 && error.error?.errors) {
      return Object.values(error.error.errors as Record<string, string[]>).flat().join(' ');
    }
    if (error.status === 404 && this.isRegistering) return 'La création de compte est désactivée sur cette installation.';
    if (error.status === 404) return 'Le service de comptes n’est pas activé sur le Gateway.';
    return 'La connexion a échoué. Vérifiez le service et réessayez.';
  }
}
