# Phase 91.30 — conserver l’identité des validateurs Collection

Les décisions Approved/Rejected des révisions Collection enregistrent maintenant `ReviewedByOwnerId` avec `ReviewedAtUtc`. L’API prend l’identifiant du claim `jobagent_owner_id` du Bearer authentifié ; la route exige toujours `CollectionAdmin`. Le champ est renvoyé sous `reviewedByOwnerId` dans les DTO de révision.

Une décision est immuable : une reprise de la même décision conserve la première date et le premier auteur, même si un autre administrateur la répète. Une décision opposée reste un conflit. La migration additive `20261018130853_AddRevisionReviewer` ajoute une colonne UUID nullable ; les décisions déjà enregistrées ne sont pas attribuées rétroactivement à l’owner configuré.

Validation : les tests unitaires Collection passent (48/48) ; les tests PostgreSQL passent (50/50), avec vérification de la persistance, des reprises concurrentes et des lignes historiques sans auteur. EF confirme qu’aucun changement du modèle ne manque de migration.
