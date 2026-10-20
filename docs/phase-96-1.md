# Phase 96.1 — Préparer une soumission manuelle

Le dossier au statut `Approved` présente un contrôle préalable avant une soumission éventuelle : le candidat confirme avoir vérifié le poste et l’entreprise, puis ouvert le CV et la lettre de la version approuvée. L’interface indique alors que les pièces peuvent être envoyées manuellement. Cette vérification est locale à l’écran et ne remplace ni l’approbation du dossier, ni l’enregistrement de l’envoi réel.

L’application ne contacte aucune plateforme, n’ouvre aucune URL fournie par le candidat et ne transmet aucun document. Après avoir lui-même envoyé sa candidature, le candidat utilise toujours l’action séparée d’enregistrement manuel, qui exige sa confirmation, la date réelle et le canal. Le backend conserve la protection existante par propriétaire, statut approuvé, révision exacte et prévention des soumissions répétées.

Cette étape respecte le mode de préparation et validation manuelle demandé pour la phase 96. Toute intégration future devra être fondée sur une API officielle autorisée, présenter le contenu final à l’utilisateur, demander une approbation explicite propre à l’envoi, conserver le résultat et permettre une désactivation immédiate. CAPTCHA, contrôles d’accès et autres protections de plateforme ne seront pas contournés.

Validation : le scénario Playwright du parcours candidat confirme que le message de préparation reste absent tant que les deux vérifications ne sont pas cochées, puis permet d’enregistrer une déclaration manuelle après cette revue. La suite complète est lancée avec `npm run test:e2e` depuis le projet frontend.
