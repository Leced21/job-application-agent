import { Component, inject, output, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthApiService } from '../../auth/auth-api.service';

@Component({
  selector: 'app-header',
  imports: [],
  templateUrl: './header.html',
  styleUrl: './header.scss'
})
export class Header {
    private readonly auth = inject(AuthApiService);
    private readonly router = inject(Router);
    readonly menuClicked = output<void>();
    readonly email = signal('');
    readonly signingOut = signal(false);

    constructor() {
        this.auth.session().subscribe({ next: session => this.email.set(session.email ?? '') });
    }

    signOut(): void {
        if (this.signingOut()) return;
        this.signingOut.set(true);
        this.auth.logout().subscribe({
            next: () => { void this.router.navigate(['/login']); },
            error: () => { this.signingOut.set(false); }
        });
    }
}
