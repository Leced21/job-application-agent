# Phase 96.10 — Résultat incertain et blocage des reprises

Le scénario Chromium couvre désormais les résultats `Submitted` et `Unknown`. Pour `Unknown`, il vérifie que le résultat reste visible, qu’aucune nouvelle réservation n’est proposée et que le formulaire d’envoi manuel est masqué. Les appels sont simulés par une plateforme synthétique et le test confirme qu’aucun hôte externe n’est contacté.

Le test d’intégration Applications vérifie aussi que l’API refuse l’aperçu et toute nouvelle réservation après un résultat `Unknown`. Le domaine conserve l’idempotence de la déclaration initiale et interdit de remplacer ce résultat par une affirmation d’échec certain.

Validation : Chromium, 2 scénarios réussis ; test d’intégration des résultats, 3 cas réussis ; test unitaire domaine ciblé, 1 réussi.
