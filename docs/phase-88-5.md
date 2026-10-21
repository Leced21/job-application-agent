# Lot 88.5 — Matching Enrichment Agent

Le catalogue Agents rend MatchingEnrichment disponible pour prioriser les vérifications d'un résultat Matching existant. Le modèle choisit uniquement les priorités de critères nommés ; les signaux, les messages affichés et les seuils sont fournis/calculés par le serveur. Les scores Matching restent inchangés et toute proposition attend une revue humaine.

Le connecteur local lit le résultat par son identifiant, vérifie le profil épinglé et retire les explications et les autres données avant de figer la source. L'activation reste désactivée par défaut. Voir le [contrat et les commandes locales](agents-matching-enrichment.md).

Trois agents sont maintenant implémentés : JobAnalysis, ProfileAnalysis et MatchingEnrichment. Les quatre autres restent indisponibles. Ce lot ne modifie pas la base Agents et n'active pas le connecteur sur l'instance persistante. La compilation des 44 projets réussit sans avertissement ni erreur. Les projets de tests sont compilés, mais les tests n'ont pas été exécutés dans ce lot.

L'API Agents locale a été redémarrée et son catalogue confirme trois agents exécutables, quatre messages fixes et `matchingEnrichmentEnabled=false`. Le quota persistant reste à zéro token consommé et zéro réservé. Le modèle réel n'a pas été sollicité par cette vérification.
