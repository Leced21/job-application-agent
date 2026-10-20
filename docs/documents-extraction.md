# 86.2a — extraction de texte sourcé

Cette tranche extrait le texte des CV importés, sans transformer des mots trouvés en expériences, compétences ou diplômes. Le résultat est conservé avec les références au document source et le statut `PendingReview`. Aucune intégration au profil ni approbation automatique. La structuration des données candidates et leur revue explicite constituent le prochain lot 86.2b.

## Préparation et migration

Avec la connexion et l'identité Documents du [guide de démarrage](documents.md) déjà configurées, appliquer la migration `20261015131355_AddResumeTextExtractions`. Elle ajoute les résultats d'extraction sans modifier les CV existants, et sans démarrer d'extraction sur les fichiers déjà présents.

Depuis la racine PowerShell :

```powershell
dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api
dotnet build src/services/Documents/JobApplicationAgent.Documents.ExtractionWorker/JobApplicationAgent.Documents.ExtractionWorker.csproj --configuration Release
$env:Documents__Extraction__WorkerPath = (Resolve-Path 'src/services/Documents/JobApplicationAgent.Documents.ExtractionWorker/bin/Release/net10.0/JobApplicationAgent.Documents.ExtractionWorker.dll').Path
dotnet run --project src/services/Documents/JobApplicationAgent.Documents.Api --launch-profile http
```

Le chemin du worker est absolu et configuré côté serveur ; il n'est jamais fourni par une requête. Garder l'ensemble de son répertoire de build avec ses DLL, runtimeconfig et deps, pas uniquement le fichier d'entrée. Le runtime dotnet doit être disponible sur l'hôte. Si un autre répertoire d'artefacts est utilisé au build, configurer le chemin réel correspondant. Les builds de validation en TEMP ne constituent pas une installation du service utilisateur.

Sans WorkerPath valide, les imports et lectures continuent de fonctionner ; une nouvelle extraction renvoie 503. Un résultat déjà enregistré peut être relu sans redémarrer le worker. La migration n'a été appliquée qu'aux bases éphémères de tests ; aucune base persistante utilisateur n'a été migrée et aucun vrai CV utilisateur n'a été lu.

## API

L'authentification Bearer et le propriétaire sont ceux de 86.1. Les nouvelles routes héritent de no-store et nosniff. Le texte est une donnée personnelle protégée au même titre que le fichier.

| Méthode | Route | Réponse |
|---|---|---|
| POST | `/api/v1/documents/resumes/{id}/extraction` | 200 résultat existant ou nouvellement extrait |
| GET | `/api/v1/documents/resumes/{id}/extraction` | 200 résultat de la version courante ; 404 absent |

Identifiant absent ou d'un autre propriétaire : 404. Sans authentification : 401. Fichier non exploitable, chiffré, vide de texte ou au-delà des limites : 422 générique, sans détails du parseur. Worker absent, capacité occupée ou délai dépassé : 503. Quota POST atteint : 429. Les annulations appelantes se propagent. Aucune erreur de parsing ne produit de résultat partiel en base.

Exemple après l'import manuel de 86.1, dans le terminal où le jeton est configuré :

```powershell
$resumeId = Read-Host 'Identifiant du CV déjà importé'
$documentHeaders = @{ Authorization = "Bearer $env:Documents__LocalAuth__Token" }
$extracted = Invoke-RestMethod -Method Post `
    -Uri "http://localhost:5005/api/v1/documents/resumes/$resumeId/extraction" `
    -Headers $documentHeaders
$extracted | Select-Object id, resumeId, sourceSha256, engineVersion, reviewStatus, unitKind
$extracted.segments | Format-Table index, text -Wrap
```

Le résultat contient id, resumeId, sourceSha256, engineVersion, extractedAtUtc, reviewStatus, unitKind et segments. Un segment possède un index à partir de 1 et son texte. Pour PDF, l'unité est Page, y compris les pages vides d'un document comportant du texte ailleurs ; pour DOCX, Paragraph du corps principal. Ces références permettent de revenir au CV, mais ne sont pas encore des preuves validées de qualifications.

EngineVersion vaut `resume-text-v1-pdfpig-0.1.16`. Une contrainte unique document/version garantit un résultat conservé par moteur ; les demandes concurrentes retrouvent l'identifiant gagnant. Une répétition n'ajoute pas une nouvelle extraction identique. Un changement de moteur devra changer la version ; les anciennes lignes restent conservées, même si le GET courant ne les liste pas. L'état d'import UploadedUnanalysed reste inchangé : l'existence d'un texte n'atteste pas une analyse complète du CV.

## Parsing et limites

PDF utilise **PdfPig 0.1.16**, épinglé, en parsing non permissif avec profondeur de pile limitée. L'extraction passe par ContentOrderTextExtractor, selon le [guide du projet](https://github.com/UglyToad/PdfPig), plutôt que par la concaténation brute de page.Text. La version et l'API ont été vérifiées sur [NuGet](https://www.nuget.org/packages/PdfPig/0.1.16). Les PDF chiffrés sont refusés, sans demander ni tenter un mot de passe fourni par l'utilisateur.

DOCX réutilise d'abord les contrôles de paquet de 86.1, puis lit le XML du corps principal avec DTD interdit et sans résolution externe. Le texte des paragraphes et tableaux est conservé, avec tabulations et sauts explicites ; les instructions de champs, révisions supprimées et dessins sont exclus. Il ne s'agit pas d'un rendu Word : en-têtes, pieds de page, commentaires, texte d'images et mises en page complexes ne sont pas reconstitués. Le texte extrait ne garantit pas une reproduction complète de ce qu'affiche Word.

Le worker traite les octets transmis par stdin, sans chemin de CV en argument ni fichier temporaire explicite. Sa sortie JSON arrive par stdout ; le contenu du CV et les exceptions du parseur ne sont pas journalisés. Le parent ne lui transmet ni les variables de connexion ni le token API. Seules les variables système nécessaires au lancement sont conservées.

Limites fixes du contenu : 5 Mio en entrée, 40 pages PDF ou 2 000 paragraphes DOCX, 200 000 caractères de texte total. La sortie doit être complète et valide, pas tronquée. Un document sans texte exploitable donne 422 ; aucun OCR n'est lancé.

Le parent autorise au plus deux workers à la fois, sans attente, et le POST partage un quota de 10 appels par fenêtre fixe d'une minute pour l'instance, y compris les résultats déjà disponibles. Le délai est de 15 secondes par défaut, configurable par `Documents__Extraction__TimeoutMilliseconds` de 1 à 30 000 millisecondes. Une annulation ou un dépassement entraîne l'arrêt du processus et de son arbre, puis sa collecte avant libération de la capacité.

Le worker reçoit une limite de tas managé de 256 Mio via DOTNET_GCHeapHardLimit, au format hexadécimal décrit par la [documentation .NET](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector). Le parent surveille aussi le working set toutes les 50 ms et arrête le worker au-delà de 512 Mio. La sortie stdout est limitée à deux millions de caractères environ (2×1024×1024), stderr à 8 192 caractères ; les diagnostics ne sont pas renvoyés bruts au client.

Le processus séparé permet d'interrompre un parseur qui ne coopère pas avec un CancellationToken. **Ce n'est pas une sandbox de sécurité OS ni un antivirus.** Le worker conserve les droits système de l'utilisateur qui lance l'API ; la surveillance mémoire n'est pas un plafond OS empêchant tout pic entre deux observations. Conteneurisation/permissions minimales et analyse de fichiers restent à industrialiser.

## Suite

PDF scannés et documents sans couche texte nécessiteront un lot OCR distinct. L'ordre de lecture des colonnes, les polices et la qualité du document peuvent affecter le texte extrait ; il doit être vérifié contre l'original. Aucune donnée n'est qualifiée de certaine parce qu'un mot est présent : « not certified in Azure » reste du texte négatif, pas une certification Azure.

La [structuration manuelle sourcée et la revue explicite 86.2b](documents-facts.md) sont disponibles. La phase 86.2 reste en cours : le lot 86.2c devra définir le transfert vers Profile avec identité propriétaire et idempotence. La validation d'un fait particulier ne transforme pas le texte brut en CV entièrement vérifié.
