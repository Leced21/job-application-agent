import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
    selector: 'app-dashboard',
    imports: [RouterLink],
    template: `
        <section aria-labelledby="dashboard-title">
            <h1 id="dashboard-title">Tableau de bord</h1>
            <p>Retrouvez votre profil et explorez les offres d’emploi.</p>
            <nav aria-label="Accès rapides">
                <a routerLink="/profile">
                    <h2>Mon profil</h2>
                    <p>Complétez vos expériences, compétences et préférences.</p>
                </a>
                <a routerLink="/jobs">
                    <h2>Offres d’emploi</h2>
                    <p>Consultez les offres et affinez votre recherche.</p>
                </a>
            </nav>
        </section>
    `,
    styles: `
        :host { display: block; }
        section { padding: 24px; }
        h1 { margin: 0 0 12px; }
        nav { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 20px; margin-top: 28px; }
        a { display: block; padding: 24px; border: 1px solid #dce3ed; border-radius: 16px; background: white; color: inherit; text-decoration: none; }
        a:hover { border-color: #6366f1; }
        a:focus-visible { outline: 2px solid #6366f1; outline-offset: 4px; }
        h2 { margin-top: 0; font-size: 20px; }
        p { line-height: 1.6; }
    `
})
export class Dashboard {}
