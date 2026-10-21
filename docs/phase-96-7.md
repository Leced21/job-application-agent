# Phase 96.7 — Exposer la réservation contrôlée

La route authentifiée `POST /api/v1/applications/{id}/submission-attempts` permet de réserver la tentative après l’aperçu et l’accord humain. Le corps comprend `expectedRevision`, `confirmed`, `attemptId`, `platformId` et `reviewSha256`. `platformId` est uniquement une clé de recherche : Applications prend les capacités dans le registre serveur et refuse les identifiants absents. L’interrupteur global doit également être activé côté serveur.

La réservation reste atomique dans la transaction PostgreSQL sous verrou de ligne. La même clé et les mêmes paramètres retournent la tentative déjà créée sans nouvel événement, y compris lors d’un rejeu après perte de réponse. Une clé différente, une empreinte ou révision incorrecte, une confirmation absente, une plateforme inconnue ou une politique non satisfaite produit un conflit. Après réservation, l’aperçu est fermé jusqu’à un résultat `DefinitelyNotSubmitted`, qui exige ensuite un nouvel accord.

Le registre reste vide et désactivé par défaut dans la configuration livrée. La route prépare uniquement l’état local ; aucun connecteur n’est appelé et aucune donnée de candidature n’est envoyée à la plateforme. Le résultat reste déclaré par le propriétaire via la route de la phase 96.6.

Validation : les cinq tests d’intégration ciblés du cycle réservation/résultat passent. Ils couvrent notamment la résolution serveur, le refus d’une plateforme inventée, la confirmation obligatoire, le rejeu idempotent et le refus d’une autre clé. Les tests unitaires du domaine passent également (235 tests).
