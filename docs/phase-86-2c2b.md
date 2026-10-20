# Phase 86.2c.2b — Import des faits datés dans Profile

## Résultat

Les expériences, diplômes et certifications approuvés, accompagnés du mapping et des dates explicitement confirmés, peuvent être transférés par le POST profile-import existant. Les compétences restent compatibles avec 86.2c.1. Le [guide de transfert](documents-profile-import.md) décrit la configuration locale ; le [guide de confirmation](documents-profile-mapping.md) décrit les preuves et dates préalables.

Le récepteur valide les libellés et preuves, la décision, les dates typées, leur auteur, note et horodatage UTC postérieur ou égal à la revue. Les dates ne sont jamais déduites d'une période CV. La source et la confirmation du propriétaire restent distinguées dans l'audit. Aucun transfert ne se produit pendant une revue ou une confirmation : le propriétaire doit déclencher le POST d'import.

## Conciliation

Le rapprochement utilise les libellés après Trim et comparaison OrdinalIgnoreCase, ainsi qu'une date exacte :

| Catégorie | Clé de rapprochement | Contradictions bloquantes |
|---|---|---|
| Skill | Name | Plusieurs correspondances |
| Experience | Title + Company + StartDate | Plusieurs correspondances, EndDate ou IsCurrent différents |
| Education | Degree + Institution + StartDate | Plusieurs correspondances, EndDate ou IsCurrent différents |
| Certification | Name + Issuer + IssueDate | Plusieurs correspondances, expiration différente, identifiant existant non vide et différent de l'identifiant source fourni |

Une correspondance compatible donne LinkedExisting et ne modifie aucun champ ni horodatage de la cible. Les informations facultatives existantes sont conservées, même différentes de celles du CV ; elles ne sont pas complétées par cet import. Sans correspondance, Created crée seulement les libellés sourcés, dates confirmées et description/identifiant éventuellement sourcés. Localisation, spécialité et URL non renseignées restent null. Une autre date de début/émission constitue une autre clé ; ce lot ne détecte pas sémantiquement les périodes qui se chevauchent.

Les recherches sont bornées à 10 001 lignes de métadonnées par catégorie ; au-delà de 10 000, le transfert est refusé. Les imports utilisent les verrous transactionnels existants par fait et profil. Entité et reçu sont enregistrés dans la même transaction. Une reprise après réponse perdue consulte le reçu sans nouveau POST. Supprimer une entité ne la ressuscite pas lors d'une reprise.

## Reçu et migration

Le reçu ajoute **Kind** et **EntityId**. Le champ historique SkillId reste égal à EntityId pour Skill, et vaut Guid.Empty pour les autres catégories ; les nouveaux clients doivent employer Kind/EntityId. L'audit RequestJson contient la preuve et, pour une catégorie datée, Dates avec confirmation, note et auteur/horodatage. Le SHA du corps normalisé bloque la réutilisation d'un factId avec un autre contenu. Les faits et confirmations sont immuables côté Documents.

Migration Profile **20261015174418_GeneralizeDocumentFactReceipts** : renommage SkillId → EntityId sans perte de valeurs ; ajout Kind avec valeur par défaut Skill pour les reçus antérieurs. Le corps sérialisé d'une ancienne compétence sans Dates conserve la même empreinte. Le test de migration remonte une base de l'ancien schéma contenant un reçu réel et vérifie la reprise HTTP directe avec l'empreinte historique.

Le Down refuse un retour à l'ancien schéma tant qu'il existe un reçu d'une catégorie datée, car celui-ci ne pourrait pas être représenté correctement. Arrêter les anciens binaires Profile avant de migrer et démarrer les nouveaux ensuite ; ne pas utiliser un ancien binaire contre le schéma renommé. Appliquer la migration avec la connexion Profile configurée hors Git :

```powershell
dotnet ef database update --project src/services/Profile/JobApplicationAgent.Profile.Infrastructure --startup-project src/services/Profile/JobApplicationAgent.Profile.Api --context ProfileDbContext
```

La migration Documents **20261015152633_AddConfirmedProfileDates** du lot précédent reste prérequise. Aucune nouvelle migration Documents dans ce lot. Seules les bases PostgreSQL 17 éphémères des tests ont été modifiées pendant le développement.

## Fichiers et validation

Documents.Application/ProfileFactImport et ProfileFactMapping, Documents.Infrastructure/ProfileFactImportClient : validation avant réseau, transmission de la confirmation persistée et contrôle du reçu typé. Profile.Domain/DocumentFactImport, Profile.Application/Profiles/DocumentFacts, Profile.Infrastructure/Repositories et migration/snapshot : reçu générique, validation et conciliation atomique. Profile.Api/DocumentFactsController : corps d'import borné à 128 KiB pour conserver les preuves et notes Unicode des quatre catégories ; les confirmations Documents restent bornées à 16 KiB. Aucun secret ni donnée utilisateur n'est ajouté au dépôt.

Compilation finale des **32 projets : zéro erreur et zéro avertissement**. Les quatre suites ciblées totalisent **523 tests réussis, zéro échec et zéro ignoré** : Profile 327 unitaires + 71 d'intégration ; Documents 43 unitaires + 82 d'intégration. Les quatre TRX finaux ont été relus. Ce lot ajoute 41 tests : 22 unitaires Profile et 19 d'intégration Documents/Profile. Les suites Job, Matching et Collection ainsi qu'Angular n'ont pas été relancées ; leurs projets .NET compilent.

Les nouveaux tests d'intégration couvrent les trois créations avec dates exactes, perte de réponse après commit, suppression de la cible puis reprise, conservation des informations vérifiées, imports concurrents avec et sans cible existante, dates contradictoires, correspondances multiples, refus avant confirmation et migration d'un reçu historique. Les validations unitaires Profile refusent notamment les dates/auteurs/horodatages incohérents et les champs requis absents. Les contrôles EF ne détectent pas de changement de modèle non migré. Les parcours antérieurs d'extraction, revue, confirmation et import de compétences restent verts, comme les contrats Profile historiques.

```powershell
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-dated-imports" -m:1 --disable-build-servers
# Quatre suites : Profile.UnitTests, Profile.IntegrationTests,
# Documents.UnitTests et Documents.IntegrationTests.
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase86-dated-imports" -m:1 --disable-build-servers
```

Les transports de test passent par deux vrais hôtes ASP.NET TestServer et deux PostgreSQL 17 éphémères. Les faits des nouveaux tests sont synthétiques ; les tests antérieurs du vrai worker d'extraction restent inclus. Aucun CV ni profil réel traité, pas de service externe, de migration utilisateur, de commit, de push ou de changement de branche. Les modifications préexistantes sont conservées. Aucun frontend n'est modifié ; les tests ne constituent pas une vérification Kestrel des bornes de transport.

## Limites et suite

L'identité demeure celle du propriétaire local unique. Les anciens endpoints Profile restent anonymes et ne prennent pas les verrous d'import : ce lot ne garantit pas une course avec leur CRUD manuel et ne constitue pas la sécurisation globale prévue en phase 91. Les confirmations et reçus n'ont pas de révocation ni de synchronisation continue. Les reçus conservés après suppression de la cible nécessiteront une politique de rétention.

Le transfert contrôlé couvre désormais les quatre catégories lorsque leurs données requises sont disponibles et confirmées. La phase 86 reste en cours : prochain lot **86.3, regroupement et historique des versions de CV**, puis génération, export et prévisualisation selon le cahier des charges. Les agents IA restent prévus en phases 88/89.
