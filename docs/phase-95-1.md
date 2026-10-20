# Phase 95.1 — Premier parcours candidat automatisé

Le premier scénario de parcours monte les routes Angular et le shell réel avec un backend HTTP simulé par `HttpTestingController`. Il vérifie la redirection d'un candidat anonyme vers la connexion, l'échange antiforgery et la connexion, le retour au tableau de bord, le chargement des recommandations, puis leur ouverture depuis la navigation. Les réponses de session, de connexion et de matching sont contrôlées dans le test ; aucune candidature n'est soumise et aucune donnée externe n'est utilisée.

Le scénario est ajouté à la suite Vitest déjà exécutée par le job frontend de `Repository CI`. Le contrôle local de types peut être lancé avec `npx tsc --noEmit -p tsconfig.spec.json` depuis le dossier frontend. Les tests ne sont pas exécutés localement, conformément à la pratique retenue pour ce dépôt.

Le parcours navigateur réel a ensuite été ajouté en phase [95.3](phase-95-3.md). Le présent test Vitest garde une vérification rapide du routage et des appels HTTP sous le DOM Angular.
