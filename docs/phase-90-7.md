# Phase 90.7 — exécution et revue des agents

La page `/agents` affiche le modèle local, le tokenizer, les limites, l’état des sept agents et le budget. Elle permet de choisir un agent et une ressource appartenant à la session : profil, offre, résultat de matching ou candidature active. L’interface crée un identifiant de requête neuf, lance `POST /api/v1/agents/runs` et affiche la proposition retournée.

L’historique charge les résumés de runs ; l’ouverture d’un run récupère sa proposition. Une proposition `PendingReview` doit être lue puis explicitement approuvée ou rejetée. La revue est liée à la révision et au SHA-256 exacts du résultat. L’approbation enregistre la décision et peut autoriser les agents CV/lettre à réutiliser une analyse d’offre ; elle ne modifie pas automatiquement le profil, les documents ou les candidatures.

Les sélecteurs n’affichent que les ressources compatibles : offres Discovered/Saved, résultats de matching terminés et candidatures Submitted/InReview/Interview. L’adaptation de CV et la lettre nécessitent une analyse de l’offre préalablement approuvée. Le Gateway relaie les routes authentifiées `/api/v1/agents/*` et les autres lectures locales utilisées pour remplir ces sélecteurs.

## Vérification

La compilation Angular de production réussit. Le lancement des tests Vitest échoue avant l’exécution des tests dans l’environnement sandbox, car celui-ci refuse le renommage d’un fichier temporaire (`EPERM`).
