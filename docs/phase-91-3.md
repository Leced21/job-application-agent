# Phase 91.3 — limite de débit des lectures Agents

Les routes GET du service Agents appliquent maintenant une limite fixe de 60 requêtes par minute, partitionnée par propriétaire authentifié, avec rejet immédiat en HTTP 429. La limite existante de dix POST `/runs` par minute reste inchangée.

Les quotas sont en mémoire et par instance, conformément au mécanisme ASP.NET Core déjà utilisé par le service. Ils ne protègent pas contre une charge distribuée entre plusieurs instances ; une politique distribuée et des seuils communs restent à traiter avant une exposition publique.

Un test d’intégration vérifie le rejet au-delà de 60 lectures. Le projet API compile ; les tests ne sont pas exécutés dans le flux de travail demandé.
