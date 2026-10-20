# Phase 85.5 — Source continue Adzuna

## Objectif

Ajouter une source d'offres réelle, continue et configurée par l'utilisateur, en utilisant l'API de recherche officielle d'Adzuna. Aucun scraping de HelloWork ni d'autre site n'est ajouté.

## Comportement livré

- Source `adzuna` enregistrée dans Collection et appel de `GET /jobs/fr/search/{page}` avec `app_id`, `app_key`, `what`, `where`, le format JSON et le tri par date.
- Champs conservés sans inférence : titre, entreprise, localisation, extrait de description, date de création, type de contrat et lien de redirection Adzuna. Le salaire et les compétences ne sont pas inventés.
- Réponse protégée par limite de taille, délai maximal, redirections HTTP désactivées et contrôle d'hôte du lien de provenance. Les erreurs n'exposent pas la clé API ; les journaux du client HTTP Adzuna sont désactivés car ses identifiants sont transmis dans la query string exigée par l'API.
- Synchronisation désactivée par défaut. Quand elle est activée, Collection lance une recherche au démarrage puis toutes les 60 à 1440 minutes, sur 1 à 10 critères configurés. Chaque critère collecte la première page, entre 1 et 50 résultats, et passe par la validation, la normalisation et la déduplication existantes.
- Les offres restent en attente de revue. Seule une décision administrateur explicite permet de publier vers Job.
- Les annonces publiées affichent « Adzuna » avec un lien vers Adzuna France d'au moins 116 × 23 pixels. Les paramètres de suivi Adzuna sont conservés pour l'attribution.
- `ADZUNA_APP_ID`, `ADZUNA_APP_KEY`, les critères et l'intervalle sont configurés dans le `.env` local, jamais dans Git ni dans le code source.

## Limites et conditions d'accès

La couverture France ne garantit pas qu'une annonce soit issue de HelloWork. L'API ne renvoie qu'un extrait de description. Un résultat absent n'indique pas qu'une annonce précédente est toujours en ligne. La synchronisation ne parcourt que la première page de chaque recherche et peut donc manquer des annonces en période de forte activité ; adapter fréquence, taille de page et critères aux quotas du compte.

Les conditions actuelles d'Adzuna autorisent la recherche d'emploi personnelle et demandent l'attribution de chaque annonce affichée. La clé gratuite sert à évaluer l'API ; les volumes supérieurs dépendent du plan lié au compte. L'utilisation commerciale, d'un produit générant des revenus ou des prospects nécessite un accord écrit. En cas de résiliation, les données obtenues doivent être retirées. Ces conditions peuvent évoluer : vérifier les conditions Adzuna et l'offre du compte avant tout usage durable ou partagé.

## Validation

- Tests unitaires Collection : 53 réussis.
- Build Angular : réussi, avec des avertissements de budgets SCSS déjà présents dans plusieurs fonctionnalités.
- Tests d'intégration nécessitant PostgreSQL Testcontainers non exécutés : l'accès local au moteur Docker a répondu « permission denied » sur le pipe Docker Desktop.
- Aucune requête réelle à Adzuna n'a été exécutée : les identifiants du compte ne sont pas fournis dans le dépôt.
