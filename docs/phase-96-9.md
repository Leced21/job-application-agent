# Phase 96.9 — Parcours navigateur synthétique

Un test Playwright couvre le parcours candidat de soumission contrôlée : ouverture d’un dossier approuvé, relecture de l’aperçu, accord explicite, réservation idempotente, puis déclaration d’un envoi confirmé avec sa référence. Le registre et toutes les réponses API sont simulés avec une plateforme synthétique ; le test vérifie qu’aucun hôte externe n’est contacté.

Le scénario passe aussi par l’intercepteur anti-CSRF réel du frontend avec un jeton synthétique. Il vérifie les révisions transmises aux mutations, l’identifiant de tentative envoyé au serveur et le statut Submitted après la confirmation.

Validation : `npx playwright test e2e/controlled-submission.spec.ts --project=chromium --reporter=line --workers=1` — 1 scénario réussi.
