# Phase 95.17 — Revue explicite du dossier de candidature

Le suivi Applications permet de demander la revue lorsque les exports du CV et de la lettre sont tous deux associés. Le candidat peut ensuite approuver la version affichée après avoir coché une confirmation. Les transitions utilisent la révision courante du dossier ; l'approbation reste distincte de toute déclaration d'envoi manuel.

Le scénario Playwright vérifie la commande de revue, son évolution de révision, la confirmation obligatoire et l'approbation. `npm run build` réussit et `npx playwright test --list` détecte 15 scénarios. L'exécution ciblée reste à faire : le serveur local configuré par Playwright n'a pas ouvert le port 4200.
