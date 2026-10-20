# Phase 85.3 — connecteur d'export JSON local autorisé

Lot livré le 15 octobre 2026. La source choisie par l'utilisateur est un **export JSON local autorisé, sans accès externe**. Un connecteur et son chemin de collecte commun sont livrés, avec validation intégrale, transaction et limites. Le [guide Collection](collection.md) contient le contrat et un exemple PowerShell ; le fichier `samples/collection/local-export.example.json` fournit deux offres fictives valides.

## Comportement

GET `/api/v1/collection/sources` liste les connecteurs installés. POST `/api/v1/collection/sources/local-json/collect` accepte un objet `offers` contenant 1 à 100 offres structurées. Le client transmet le contenu JSON, aucun chemin local ou distant n'est ouvert par le serveur. Le connecteur force SourceName à LocalJsonExport ; un nom de plateforme déclaré dans un fichier ne constitue pas une provenance vérifiée. Titre original, URL, contenu normalisé et dates serveur passent par les mécanismes existants.

Chaque offre est validée et normalisée avant la première écriture du lot. Une erreur invalide l'ensemble avec 400 et un chemin d'erreur indexé. Le stockage réutilise le repository existant, les contraintes de déduplication et les révisions, dans une seule transaction. Les résultats reviennent dans l'ordre d'entrée ; les verrous sont pris selon l'ordre des clés de déduplication pour protéger les lots concurrents inversés.

Le bilan expose nombre reçu, créations, doublons, observations modifiées et identifiants par index. Réenvoyer un lot ne crée pas d'identité ou de révision identique supplémentaire, mais augmente les compteurs de réobservation. Il n'y a pas de numéro de lot durable ni d'idempotence des compteurs. Une réponse perdue au commit laisse le résultat indéterminé pour le client ; une reprise réutilise les identités.

Les limites sont 1 Mio par corps HTTP, 100 offres, un délai de traitement de 30 secondes et 10 appels de collecte par fenêtre fixe d'une minute pour l'instance entière, sans attente. Le quota en mémoire inclut les demandes invalides et se réinitialise au redémarrage. Source inconnue : 404 ; quota dépassé : 429 ; deadline : 503 ; erreur interne : 500 générique. Les annulations du client se propagent. Une panne avant commit annule créations, révisions et compteurs du lot.

La collecte ne contacte aucune plateforme ni Job, ne valide aucune révision et ne publie aucune offre automatiquement. Le parcours de revue et de publication de 85.2 reste explicite. Les compétences manquantes ne sont pas inventées ; l'expiration provient uniquement des dates déclarées, sans contrôle de disponibilité en ligne.

## Architecture

Application définit IOfferSourceConnector, les DTO et CollectSourceHandler, puis le port de stockage de lots ICollectedOfferBatchRepository. Infrastructure fournit LocalJsonExportConnector, sans dépendance réseau, et implémente le port de lots dans CollectedOfferRepository. Ce dernier possède la transaction du lot ; ImportAsync réutilise une transaction déjà active au lieu d'en ouvrir une seconde. Les imports unitaires continuent à gérer leur propre transaction et leur déduplication concurrente.

API ajoute SourcesController et un limiteur nommé uniquement sur l'action de collecte. Aucun projet, package, schéma ou migration n'est ajouté. Les tests existants de modèle ne détectent pas de migration manquante. Les migrations Collection 85.1/85.2a restent requises pour une base neuve ou ancienne. Aucune base persistante utilisateur n'a été modifiée.

Les connecteurs sont indépendants du parcours de normalisation et de stockage. Une future API officielle devra apporter son propre adaptateur, ses accès et ses quotas, après sélection de la source et vérification de ses conditions. Le connecteur local ne vérifie pas les droits d'importation auprès d'un tiers : il traite l'export autorisé fourni par l'utilisateur.

## Fichiers concernés

Créés :

- `src/services/Collection/JobApplicationAgent.Collection.Application/Connectors/CollectSourceHandler.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Connectors/LocalJsonExportConnector.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Api/Controllers/SourcesController.cs`
- `tests/Collection/JobApplicationAgent.Collection.UnitTests/CollectSourceHandlerTests.cs`
- `tests/Collection/JobApplicationAgent.Collection.IntegrationTests/Api/SourcesControllerTests.cs`
- `samples/collection/local-export.example.json`
- `docs/phase-85-3.md`

Modifiés : Application/DependencyInjection.cs ; Infrastructure/DependencyInjection.cs et Repositories/CollectedOfferRepository.cs ; Api/Program.cs ; `docs/collection.md` et `README.md`. Aucun changement Job/Profile/Matching, frontend, branche, commit ou push dans ce lot. Les modifications préexistantes du dépôt sont conservées.

## Validation

Compilation finale des **25 projets : zéro erreur, zéro avertissement**. **84 tests Collection réussis, zéro échec** : 42 unitaires et 42 d'intégration, soit 16 nouveaux tests. Les suites autonomes Job/Profile/Matching et Angular ne sont pas relancées ; les tests d'intégration Collection conservent les deux parcours réels vers Job ajoutés en 85.2b.

Les tests couvrent sélection de source, rejet intégral avant persistance, index des erreurs, limites de nombre, annulation et normalisation avant stockage. Côté API/PostgreSQL : liste des sources, provenance non usurpée, doublons internes au fichier, nouvelles révisions, erreurs de lots nuls/vides/trop grands/invalides, source absente, quota du onzième appel et deux lots inversés simultanés. Une panne injectée à la deuxième sauvegarde confirme zéro offre et zéro révision après rollback, même si la première sauvegarde a réussi dans la transaction.

Les limites HTTP de taille sont configurées par RequestSizeLimit pour le serveur ; TestServer ne constitue pas une vérification du transport Kestrel sur un corps trop volumineux. Le test de quota utilise un nouveau serveur par cas pour isoler sa fenêtre. Les conteneurs PostgreSQL 17 et credentials de test sont éphémères. Aucun téléchargement ni publication sur une base utilisateur.

Commandes principales depuis la racine :

```powershell
dotnet build src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj --artifacts-path "$env:TEMP/job-agent-phase85-3" -m:1 --disable-build-servers
dotnet test tests/Collection/JobApplicationAgent.Collection.IntegrationTests/JobApplicationAgent.Collection.IntegrationTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-3" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase85-3-results"
dotnet test tests/Collection/JobApplicationAgent.Collection.UnitTests/JobApplicationAgent.Collection.UnitTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-3" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase85-3-results"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase85-3" -m:1 --disable-build-servers
```

Les deux TRX ont été relus : 42/42 puis 42/42, aucun échec. Le JSON d'exemple est syntaxiquement valide et contient deux entrées. Les artefacts TEMP évitent de remplacer les binaires des API locales éventuellement actives. Aucun secret ajouté au dépôt.

## Suite

85.3 est livré pour le premier connecteur choisi. La **phase 85 reste en cours** : les événements à destination du Matching Service ne sont pas encore publiés. Prochain lot proposé **85.4 : contrats d'événements et outbox transactionnelle**, en préparant leur transport/consommation avec la phase 89. Les futures sources externes seront ajoutées selon les accès disponibles. Le service reste local jusqu'à la sécurisation, et aucune candidature réelle n'est soumise par cette collecte.
