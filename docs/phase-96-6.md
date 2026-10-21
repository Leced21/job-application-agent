# Phase 96.6 — Enregistrer le résultat d’une tentative

La route authentifiée `POST /api/v1/applications/{id}/submission-attempt-result` enregistre le résultat d’une tentative déjà réservée via le parcours exposé en [phase 96.7](phase-96-7.md). Le propriétaire doit fournir la révision courante, l’identifiant exact de la tentative, une confirmation explicite, l’issue et sa date UTC. L’événement `ControlledSubmissionResultRecorded` est conservé dans l’historique existant ; aucune migration n’est nécessaire.

Trois issues sont définies : `Submitted` marque la candidature comme envoyée et accepte une référence facultative ; `DefinitelyNotSubmitted` confirme qu’aucun envoi n’a été accepté ; `Unknown` couvre notamment un timeout ou une réponse perdue. Un rejeu exact du résultat est idempotent, même si la révision indiquée est devenue ancienne. Un résultat différent pour la même tentative, une tentative inconnue, une révision périmée ou un autre propriétaire sont refusés.

Les issues `Submitted` et `Unknown` interdisent tout nouvel essai. Après `DefinitelyNotSubmitted`, l’aperçu redevient disponible, mais une nouvelle tentative exige un nouvel accord humain lié à la révision courante et une nouvelle clé idempotente. Cet endpoint authentifie le propriétaire et enregistre sa déclaration ; il ne vérifie pas lui-même un reçu auprès d’une plateforme. Aucun adaptateur réseau n’est branché et aucune donnée n’est transmise à un site externe.

Exemple de corps :

```json
{
  "expectedRevision": 7,
  "attemptId": "00000000-0000-0000-0000-000000000000",
  "outcome": "Unknown",
  "occurredAtUtc": "2026-10-18T10:00:00Z",
  "confirmed": true,
  "reference": null
}
```

Validation : 235 tests unitaires Applications réussis. Quatre tests d’intégration PostgreSQL/TestServer couvrent la persistance et le rejeu des trois issues ainsi que le refus d’un autre propriétaire. Aucune migration de schéma ni requête externe n’est ajoutée.
