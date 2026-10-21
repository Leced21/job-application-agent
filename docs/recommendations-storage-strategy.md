# Décision 84.3 — classement, persistance et invalidation

## Solution locale livrée

Le contrat Matching est enrichi de manière additive avec `totalCount` et `totalPages`. Le contrat Job reste un tableau pour ne pas casser les clients existants. La pagination de récupération des offres (lots de 100) est indépendante de celle des recommandations. Aucun total amont n'est supposé fiable pendant le parcours : le total est celui des identifiants uniques observés jusqu'à la fin.

Chaque lot est évalué avant de récupérer le suivant. Un ensemble d'identifiants élimine les doublons ; la première version observée est retenue. Le nombre d'évaluations et le total ne sont pas augmentés par un doublon. Une file de priorité conserve les K meilleurs résultats, K = min(page × pageSize, MaxUniqueJobs), puis trie ces seuls résultats pour extraire la page finale. Si le début de la page est au-delà du maximum d'offres autorisé, le service ne conserve/évalue aucun score mais parcourt encore les identifiants pour calculer le total.

L'ordre est `RankingScore DESC NULLS LAST`, `EvaluationCoverage DESC`, `JobId ASC`. Le score zéro arrive avant un score absent. L'ordre est déterministe pour un même profil et un même ensemble d'offres ; il ne garantit pas des pages immuables si leurs données changent entre deux requêtes.

Coût par requête : O(U + K + B) éléments en mémoire (U identifiants uniques, K recommandations, B offres du lot), O(U log K) pour la sélection et O(K log K) pour le tri final. Il n'y a plus de liste de toutes les descriptions ni de liste de tous les résultats si K < U. Il reste nécessaire de lire toutes les offres éligibles pour annoncer un classement global exact de l'ensemble observé.

Limites synchrones configurables et validées au démarrage :

| Paramètre | Défaut | Intervalle de configuration |
|---|---:|---:|
| MaxUniqueJobs | 10 000 | 1–100 000 |
| MaxSourcePages | 200 | 1–1 000 000 |
| TimeoutSeconds | 30 | 1–300 |

Le plafond des pages source protège aussi contre un producteur répétant indéfiniment les mêmes offres. Un lot plein exige une page supplémentaire pour confirmer la fin du catalogue, même si le nombre d'offres est exactement la limite. Les réponses HTTP Job sont limitées à 8 Mio par le client de production. Ces bornes s'appliquent par requête, pas à la somme de requêtes concurrentes ; contrôle de concurrence et limites globales restent à dimensionner avec l'observabilité et la sécurité.

Une limite de volume/pages renvoie 503 avec `Recommendation processing capacity exceeded`. Le délai global couvre profil, lectures Job et scoring ; il utilise une cancellation coopérative et renvoie 503 sur dépassement. Le titre de timeout de dépendance existant est conservé pour compatibilité, avec une description élargie au délai global. Une cancellation de l'appelant est propagée. Une requête échouée ne fournit ni liste ni totaux partiels.

## Cache actuel : désactivé volontairement

Aucun cache applicatif ou résultat persistant n'est réutilisé par les recommandations. Le profil est relu une fois par requête et les offres sont relues à chaque calcul ; une expiration ou un changement de profil sera pris en compte au prochain parcours. `Cache-Control: no-store` interdit la réutilisation de la réponse HTTP. Le stockage existant des calculs individuels n'est pas un cache de classement.

Le DTO de profil ne fournit pas aujourd'hui de version et le parcours des offres par offset n'est pas un instantané. Mettre un TTL seul masquerait les modifications et pourrait recommander une offre devenue ignorée/expirée : cette solution n'est pas retenue. Les identifiants seuls ne constituent pas une clé de cache suffisante.

## Stratégie retenue pour les volumes élevés (conçue, non implémentée)

1. Faire exposer par les services propriétaires une version du profil et une révision du catalogue éligible, mises à jour pour chaque modification pertinente. Une offre doit changer de version pour un changement de contenu, statut ou disponibilité. Les suppressions doivent invalider la révision du catalogue. Garder la propriété de ces versions dans Profile/Job.
2. Remplacer le parcours offset pour ces traitements par une lecture stable : contrat versionné de snapshot/cursor ou liste d'identifiants/version capturée côté Job. Vérifier la révision au début et à la fin ; rejeter/recommencer un calcul dont les données ont changé. La seule déduplication ne détecte pas les offres sautées pendant une mutation concurrente.
3. Persister dans Matching des snapshots de recommandations identifiés par `(candidateProfileId, profileVersion, catalogueRevision, scoringVersion)`. Une future isolation par propriétaire devra être intégrée à cette clé. Une table parent porte statut, total, versions et dates ; une table enfant porte les scores, critères, explications et rang. Indexer snapshot + ordre ; contrainte unique snapshot + jobId pour l'idempotence. Toute création de ces tables nécessitera une migration EF.
4. Calculer les scores par lots et les écrire progressivement dans un snapshot non publié. Publier atomiquement le statut Completed et ses totaux uniquement quand toutes les versions sont vérifiées ; ne jamais servir les lignes d'un snapshot Pending/Failed comme classement terminé. La pagination devient une requête SQL sur le snapshot publié et sa clé stable.
5. Exposer un workflow asynchrone explicite pour ces catalogues (202 + identifiant de tâche, suivi Pending/Completed/Failed). Les budgets synchrones actuels ne créent **aucune tâche** et ne promettent pas une reprise automatique. Une limite 503 n'est pas un accusé de réception asynchrone.
6. En phase 89, transporter `ProfileUpdated`, `JobUpdated`, `JobExpired` et les demandes de calcul avec outbox, retry et idempotence. Invalider les snapshots touchés et empêcher leur sélection comme actuels. En l'absence de messages, vérifier les versions auprès des services propriétaires avant réutilisation. Une expiration TTL sert à nettoyer, pas à prouver la fraîcheur.
7. Incrémenter `scoringVersion` lors d'un changement des pondérations, règles ou normalisations (84.4). Conserver la provenance des anciens scores pour leur explication, sans les mélanger à un classement calculé avec une autre version.

Cette persistance, le protocole de versions et le workflow asynchrone doivent être implémentés ensemble dans une évolution dédiée avec tests de concurrence/reprise. Aucun RabbitMQ, stockage de snapshot ou mécanisme d'invalidation événementielle n'est ajouté prématurément dans cette étape.

## Cohérence et limites assumées

Le parcours livré est cohérent pour un catalogue stable. Sous modifications concurrentes, il peut manquer des offres déplacées entre pages ; un doublon peut avoir une version antérieure, et le statut d'une offre peut changer après sa lecture. Les totaux décrivent ce qui a été observé, pas une vue transactionnelle au dernier instant. Les budgets empêchent un travail sans limite et les doublons n'augmentent pas les totaux, mais ne créent pas une garantie d'instantané.

Avant de lever les plafonds pour un grand catalogue, mettre en place la persistance et les versions ci-dessus ; augmenter les limites ne remplace pas ce travail. Pour la plateforme locale actuelle, la prochaine évolution fonctionnelle est 84.4, en conservant ces limites explicites.
