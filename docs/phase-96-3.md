# Phase 96.3 — Persister l’accord de revue

Le service Applications expose `GET /api/v1/applications/{id}/submission-preview` pour relire le poste, l’entreprise, les identifiants et empreintes des deux pièces ainsi que l’empreinte SHA-256 de la revue. La réponse expose `externalSubmissionEnabled: false` : cette route ne contacte aucun site.

Après avoir affiché cet aperçu et obtenu l’accord explicite, le client peut appeler `POST /api/v1/applications/{id}/submission-approval` avec `{ expectedRevision, confirmed: true, reviewSha256 }`. Applications vérifie le propriétaire authentifié, le statut Approved, l’absence d’envoi manuel, la révision exacte et l’empreinte recalculée sous le verrou de ligne. L’accord est conservé comme événement `ControlledSubmissionApproved` dans l’historique de candidature. Un rejeu avec l’ancienne révision échoue en 409. Toute note ou transition ultérieure change la révision et invalide l’accord pour la politique de phase 96.2.

Ce consentement documente une décision humaine et n’est pas encore consommé par un connecteur. Aucun résultat ou reçu externe n’est enregistré, puisqu’aucune soumission n’est effectuée. Avant d’activer un adaptateur, il faudra ajouter une prise atomique à usage unique du consentement, une clé d’idempotence durable et les états de résultat permettant de traiter sûrement les délais d’attente et réponses perdues.

Validation : les 224 tests unitaires Applications passent. Le test PostgreSQL/TestServer `SubmissionPreviewAndExplicitApprovalPersistWithoutSending` vérifie aperçu, confirmation requise, persistance/relecture de l’événement et rejet du rejeu. Aucun envoi externe n’est simulé ou déclenché.
