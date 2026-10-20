# Transfert des faits approuvés vers Profile — 86.2c

## Périmètre

Depuis Documents, le propriétaire déclenche explicitement le transfert d'un fait **Approved** par `POST /api/v1/documents/resumes/{resumeId}/facts/{factId}/profile-import`, sans corps. Les catégories sont Skill, Experience, Education et Certification ; les trois dernières exigent un mapping et des dates confirmés. L'identité vient du Bearer Documents ; le profil cible vient exclusivement de la configuration serveur. Une lecture de la liste Approved, une revue ou une confirmation de dates ne déclenche aucun import. Réponse 200 : reçu contenant factId, ownerId, profileId, kind, entityId, sourceSha256, action et importedAtUtc. SkillId est conservé pour compatibilité : il reprend entityId pour Skill et vaut Guid.Empty pour les autres catégories.

Le nom sourcé doit avoir au plus 150 caractères. Un nom équivalent après Trim et comparaison OrdinalIgnoreCase réutilise la compétence existante sans modifier son orthographe, sa catégorie, son niveau, ses années d'expérience ni ses dates. Plusieurs correspondances bloquent le transfert : les doublons existants doivent être conciliés explicitement. Une nouvelle compétence reçoit uniquement son nom ; catégorie, niveau et ancienneté restent null.

Pour les expériences, diplômes et certifications, préparer le [mapping confirmé](documents-profile-mapping.md), inspecter les preuves et l'aperçu, puis déclencher l'import explicitement. Les dates restent des déclarations confirmées du propriétaire ; aucun champ manquant n'est inventé. Les [règles de conciliation 86.2c.2b](phase-86-2c2b.md) réutilisent une cible compatible sans la modifier et refusent les correspondances multiples ou contradictoires.

## Activation locale

Préparer un profil existant et vérifier son ID dans Profile. Configurer hors Git, via variables d'environnement, secrets utilisateur ou gestionnaire de secrets :

| Service | Clé | Valeur |
|---|---|---|
| Profile | Profile:DocumentImport:OwnerId | Même GUID que Documents:LocalAuth:OwnerId |
| Profile | Profile:DocumentImport:ProfileId | GUID du profil existant explicitement choisi |
| Profile | Profile:DocumentImport:Token | Secret dédié, 32–512 caractères sans espace |
| Documents | Documents:ProfileImport:ProfileId | Même GUID de profil |
| Documents | Documents:ProfileImport:Token | Même secret d'import dédié |
| Documents | Documents:ProfileImport:BaseUrl | http://localhost:5001 par défaut |

Les variables d'environnement remplacent `:` par `__`. Le token d'import doit être distinct du Bearer utilisateur Documents. Ne jamais l'envoyer au navigateur ni le consigner dans Git. Une configuration Profile incomplète désactive le récepteur (401) ; une configuration Documents invalide produit 503 sans import. Il faut redémarrer les services après modification. Ce lien statique est prévu pour le propriétaire local unique, sans sélection de profil par le client.

Appliquer les migrations Profile jusqu'à **20261015174418_GeneralizeDocumentFactReceipts** (incluant 20261015135115_AddDocumentFactImports) à la base choisie, avec sa connexion configurée hors Git. Arrêter les anciens binaires Profile avant le renommage de colonne, puis démarrer les nouveaux. Documents doit avoir sa migration de confirmation **20261015152633_AddConfirmedProfileDates** :

```powershell
dotnet ef database update --project src/services/Profile/JobApplicationAgent.Profile.Infrastructure --startup-project src/services/Profile/JobApplicationAgent.Profile.Api --context ProfileDbContext
```

Le développement a appliqué cette migration uniquement aux bases éphémères des tests. Aucune base utilisateur n'a été modifiée. Aucun changement de schéma Documents n'est nécessaire pour ce lot.

Pour le parcours local, démarrer Profile avec `--launch-profile http` sur 5001 et Documents sur 5005 selon leurs guides existants. Avec les variables `$resumeId`, `$fact` et `$documentHeaders` issues du [guide de revue](documents-facts.md), après contrôle et approbation explicites :

```powershell
$receipt = Invoke-RestMethod -Method Post `
    -Uri "http://localhost:5005/api/v1/documents/resumes/$resumeId/facts/$($fact.id)/profile-import" `
    -Headers $documentHeaders
$receipt | Format-List
```

Ce POST peut être répété avec le **même factId** après une interruption. Il ne faut pas recréer le fait pour contourner une erreur. Aucun transfert de CV utilisateur n'a été effectué pendant le développement.

## Récepteur et reprise

Profile expose `POST /api/v1/profile/document-facts` et `GET /api/v1/profile/document-facts/{factId}` sous authentification DocumentsImport. Le récepteur contrôle le propriétaire/profil configuré, la décision Approved, l'auteur et la date UTC de revue, les champs de catégorie et les bornes de preuve. Pour les catégories datées, il contrôle également les dates, leur confirmation, note et auteur/horodatage. Le secret atteste le service appelant ; Profile ne relit pas le CV et ne vérifie pas la vérité sémantique du contenu. Le corps POST d'import est limité à 128 KiB pour les preuves et notes Unicode ; les réponses sont no-store.

Le reçu autoritatif reste dans Profile, table `document_fact_imports`, avec empreinte SHA-256 du corps sérialisé normalisé et audit JSONB de la preuve, source, extraction, revue et confirmation éventuelle des dates. Verrous transactionnels par fait puis profil, insertion entité/reçu atomique. Deux imports concurrents du même fait retournent le même reçu ; deux compétences portant le même nom réutilisent la même cible. Pour les catégories datées, les libellés et dates clés ainsi que la compatibilité déterminent le rapprochement selon les règles 86.2c.2b. La conciliation examine au plus 10 001 lignes de métadonnées par catégorie ; au-delà de 10 000 elle refuse le transfert.

Documents consulte d'abord le reçu avant de poster. Si Profile a enregistré puis que sa réponse est perdue, la reprise récupère ce reçu sans réinsérer ni reposter. Chaque reçu est contrôlé contre le fait, le propriétaire, le profil configuré et le SHA source. Une nouvelle soumission du même factId avec un autre corps ou une autre cible est refusée (409 côté Profile). Les reçus n'ont pas de FK en cascade : supprimer la compétence ou le profil ne les supprime pas, et une reprise ne ressuscite pas les données supprimées. Les reçus contiennent des données personnelles ; leur purge/rétention et leur affichage dédié restent à prévoir.

Le client utilise un délai de 15 secondes, limite les reçus à 16 KiB et refuse les redirections HTTP. HTTP est accepté uniquement vers une adresse de boucle locale ; ailleurs HTTPS est requis. Aucun Bearer utilisateur n'est transmis à Profile. Il n'y a pas de tâche de fond ni de nouvelle tentative automatique.

## Erreurs et limites

| Réponse Documents | Signification |
|---|---|
| 401 | Bearer Documents absent ou invalide |
| 404 | Fait absent, autre propriétaire ou autre CV |
| 422 | Fait non approuvé, mapping/dates non confirmés, champs non supportés ou conflit de conciliation Profile |
| 429 | Limiteur partagé des écritures de faits : 30/minute |
| 503 | Configuration, accès Profile, profil absent, délai ou réponse invalide ; reprendre le même fait après correction |

**Les endpoints Profile historiques restent anonymes et conçus pour l'usage local.** Ce nouveau canal authentifié ne sécurise pas tout Profile et ne constitue pas une isolation multi-utilisateur. Leur sécurisation générale reste prévue en phase 91 ; ne pas exposer l'API historique publiquement. Les verrous d'import ne sont pas utilisés par les anciens endpoints CRUD : la déduplication ne garantit pas une course avec un ajout manuel via ces endpoints. Les décisions approuvées sont finales dans le socle actuel ; aucune révocation ni synchronisation continue n'est ajoutée ici.
