# Phase 91.9 — inventaire des données à isoler par compte

L’inspection des modèles et dépôts distingue les données de candidat des catalogues partagés. Le tableau sert de périmètre à la migration d’identité ; il ne modifie aucun schéma.

| Service | Données actuelles | Travail d’isolation multi-compte |
|---|---|---|
| Profile | Un profil candidat et ses expériences, études, compétences, langues, liens, certifications et préférences. Les reçus d’import de faits portent déjà `OwnerId`. | Ajouter un propriétaire au profil racine, migrer les enfants à partir de leur relation, et filtrer chaque requête par propriétaire. Les reçus doivent référencer le propriétaire dérivé du jeton. |
| Documents | CV, faits extraits, séries, dossiers de préparation, compositions, exports et liaisons portent déjà `OwnerId`. | Remplacer l’identité statique par le sujet validé ; conserver tous les filtres propriétaire et FK composites. |
| Applications | Candidatures et commandes idempotentes portent `OwnerId`; liens de profil, offre et export sont contrôlés. | Résoudre le sujet OIDC vers le même identifiant interne que Documents/Profile et garder les contrôles de liaison interservice. |
| Agents | Exécutions, sources figées, audits et quotas portent `OwnerId`. | Dériver le propriétaire du sujet; ne jamais accepter le propriétaire provenant d’un appel Angular ou d’une requête arbitraire. |
| Matching | Les résultats sont rattachés à `CandidateProfileId`, sans propriétaire propre. | Autoriser le résultat via le profil possédé; toute route de calcul doit aussi vérifier que le profil appartient à l’appelant. |
| Job | Catalogue d’offres partagé, sans propriétaire. | Garder la lecture partagée. Restreindre les écritures aux identités de service ou rôles d’administration. |
| Collection | Catalogue d’offres collectées et outbox partagés, sans propriétaire candidat. | Garder les connecteurs et publications sous identité de service; protéger les routes d’import et d’administration. |

## Migration initiale

L’identifiant interne de compte doit être un UUID stable résolu depuis `(issuer, subject)`. Pour l’installation mono-propriétaire, créer une migration opérée explicitement qui associe le profil existant, puis réutiliser le propriétaire déjà configuré par Documents, Applications et Agents si les valeurs correspondent. En cas de désaccord, interrompre la migration et demander une correspondance explicite ; ne pas fusionner les comptes à partir du seul fait que des UUID statiques semblent différents.

La migration doit être rejouable ou transactionnelle, sauvegardée avant déploiement, et testée sur une copie anonymisée. Les cas de test incluent deux utilisateurs, accès croisé refusé, profil absent, identifiant OIDC inconnu, propriétaire historique incohérent, et ressource partagée Job lue mais non modifiée par un candidat.

L’inventaire confirme que l’implémentation OIDC dépend du choix du fournisseur seulement pour le déploiement et les paramètres client. Le schéma d’autorisation et les contrats de propriété restent indépendants du fournisseur.
