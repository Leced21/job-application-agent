# Phase 97.6 — Contrats et exploitation des événements

Le [guide des événements](events.md) rassemble le routage Collection et Job, un exemple du contrat canonique Job, les garanties de transaction, de livraison au moins une fois, de déduplication, de reprise et les requêtes SQL d'inspection en lecture seule. Il indique explicitement que l'événement d'acquisition Collection n'est pas consommé par Matching.

Le [guide des migrations](migrations.md) utilise maintenant la vraie clé `ConnectionStrings__MatchingDb` pour `MatchingDbContext`.

Vérification effectuée : les noms de tables, types d'événement, files et configurations ont été comparés au code et aux fichiers Compose. `git diff --check` et le contrôle des liens locaux sont exécutés après cette mise à jour. Aucun worker n'a été activé, aucune base ni file RabbitMQ n'a été modifiée.
