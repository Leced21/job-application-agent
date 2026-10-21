# Lot 88.4 — Profile Analysis Agent

L'agent identifie les rubriques absentes du profil et propose jusqu'à cinq questions issues d'un catalogue français fixe. Le modèle local ne reçoit que les indicateurs de présence et des métadonnées techniques. Il ne complète pas le profil et n'invente aucune qualification. Voir le [contrat, les limites et l'activation](agents-profile-analysis.md).

Le routage des sources dépend maintenant du type d'agent : une ressource Profile ne peut pas être interprétée comme une offre Job. L'accès Profile exige une activation et un UUID épinglé côté serveur ; il reste désactivé dans l'instance locale après livraison. Deux agents sont implémentés, cinq restent à développer.

## Validation

- 94 tests unitaires Agents.
- 117 tests d'intégration ordinaires avec PostgreSQL éphémère et API Job/Profile de test.
- 4 tests avec le véritable modèle local qwen2.5:7b : deux offres et deux profils synthétiques.
- 215 tests réussis au total, sans échec ni test ignoré dans ces exécutions séparées.

Les deux profils réels au sens du transport Ollama restent entièrement synthétiques. Le premier produit cinq questions, pour 552 tokens comptabilisés ; le second contient un résumé avec une instruction hostile, non transmise au modèle, et produit deux questions pour 493 tokens. Les reprises ne génèrent pas de second appel ; la revue ne modifie pas Profile. Ces cas vérifient le contrat et les barrières techniques, pas la qualité générale des priorités sur tous les profils.

Aucun profil personnel ni CV n'a été utilisé. Les tests emploient leur base éphémère et ne consomment pas le quota de l'instance persistante. La compilation de la solution et le redémarrage local sont consignés avec la livraison.

Suite : 88.5, Matching Enrichment Agent, avec son propre contrat et ses évaluations, avant les autres agents puis l'asynchronisme de phase 89.

Vérification finale : compilation des 44 projets réussie, zéro avertissement et zéro erreur. API locale redémarrée avec le code 88.4 ; aucune migration en attente. Catalogue vérifié : deux agents implémentés, onze questions fixes, ProfileAnalysis désactivé. Budget conservé à zéro token consommé et zéro réservé ; accès anonyme refusé HTTP 401.
