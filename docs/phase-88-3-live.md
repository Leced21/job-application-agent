# Qualification locale 88.3 — qwen2.5:7b

Qualification effectuée le **16 octobre 2026**, avec Ollama **0.40.5**, le modèle local `qwen2.5:7b` installé par l'utilisateur et deux offres entièrement synthétiques. Le [guide Ollama](agents-ollama.md) fournit la configuration et les commandes réutilisables.

## Artefacts et corrections

- Modèle : `845dbda0ea48ed749caafd9e6037047aa19acfcfd82e704d7ca97d631a0b697e`.
- Tokenizer : `6cf5f2a2560a7d581548aeb0c31c6a60de2d3e873152346e421affc871699be4`, extrait directement des métadonnées détaillées du GGUF installé. 151643 pièces BPE de base et 151387 fusions, sans téléchargement externe. Le digest modèle est contrôlé avant/après lecture ; les trois délimiteurs Qwen2 sont vérifiés.
- Export et provenance : `.local/agents/qwen2.5-7b-845dbda0ea48-qualified/`, ignorés par Git. Le convertisseur a été corrigé pour préserver la casse et éviter les collisions PowerShell avec les tokens nommés `Count`/`Keys`.
- Le serveur actif annonçait initialement le cloud autorisé malgré la variable utilisateur configurée. Ollama a été redémarré avec `OLLAMA_NO_CLOUD=1`. `/api/status` confirme désormais `cloud.disabled: true`. L'adaptateur vérifie aussi cet état au moment de la préparation et avant génération ; un drapeau de configuration seul ne suffit plus.
- Le prompt initial calculait correctement les tokens, mais le modèle proposait des offsets inexacts. Ces sorties ont été refusées et leur usage connu comptabilisé dans les bases de test. Le prompt **job-analysis-fr-v2** fournit des repères UTF-16 calculés par le serveur, limite leur nombre/taille et précise la distinction entre compétences, modalités et mots de liaison. Le validateur de citations n'a pas été assoupli et aucun résultat invalide n'a été corrigé silencieusement.

## Résultats réels

| Offre synthétique | Citations finales | Tokens imputés | Appels de génération avec reprise |
|---|---|---:|---:|
| `SQL required. Rust optional.` | SQL : start 0, length 3 ; Rust : start 14, length 4 | 720 | 1 |
| `🙂 Compétences requises : SQL et Rust.` | SQL : start 26, length 3 ; Rust : start 33, length 4 | 775 | 1 |

Les deux cas passent par l'API authentifiée de test, la lecture d'une vraie offre Job de test, le tokenizer extrait, le vrai serveur Ollama local et PostgreSQL éphémère. Les tests exigent exactement SQL/Rust, vérifient leur provenance via le validateur, une consommation connue, un résultat idempotent, une revue synthétique confirmée par hash/révision, trois révisions d'audit et l'absence de réservation restante. L'emoji vérifie que les offsets sont en UTF-16.

Les trois audits de chaque cas correspondent au début, à la proposition et à sa revue. Ils n'incluent pas la source textuelle. Le bilan local des sorties synthétiques est conservé dans `.local/agents/qualification-summary.json` ; il ne constitue pas le journal métier de production.

## Reproduire

Depuis la racine du dépôt, dans PowerShell 7, après démarrage d'Ollama en mode local et disponibilité de Docker :

```powershell
. ./tools/Use-QualifiedOllama.ps1 -ProvenancePath '.local/agents/qwen2.5-7b-845dbda0ea48-qualified/provenance.json'
$env:AGENTS_LIVE_OLLAMA = '1'
$env:AGENTS_LIVE_TOKENIZER_PATH = $env:Agents__Model__TokenizerPath
$env:AGENTS_LIVE_TOKENIZER_SHA256 = $env:Agents__Model__TokenizerSha256
dotnet test tests/Agents/JobApplicationAgent.Agents.IntegrationTests --filter 'FullyQualifiedName~LiveOllamaQualificationTests' --artifacts-path "$env:TEMP/job-agent-phase88-3-live" -m:1 --disable-build-servers
Remove-Item Env:AGENTS_LIVE_OLLAMA
```

Les tests réels sont désactivés par défaut : la suite ordinaire les signale comme ignorés, sauf si le filtre les exclut explicitement. Les exécutions réelles demandent le drapeau d'activation et le tokenizer local ; aucun modèle n'est téléchargé par les tests.

Pour les régressions sans appel réel :

```powershell
dotnet test tests/Agents/JobApplicationAgent.Agents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase88-3-live" -m:1 --disable-build-servers
dotnet test tests/Agents/JobApplicationAgent.Agents.IntegrationTests --filter 'FullyQualifiedName!~LiveOllamaQualificationTests' --artifacts-path "$env:TEMP/job-agent-phase88-3-live" -m:1 --disable-build-servers
```

**176 tests réussis : 74 unitaires, 100 intégrations ordinaires et 2 cas sur le modèle réel ; zéro échec et zéro ignoré dans ces exécutions ciblées.** Compteurs TRX relus séparément.

**Compilation des 44 projets réussie, zéro erreur et zéro avertissement.** L'export final avec délai de lecture a aussi été rejoué : empreinte strictement identique au tokenizer qualifié.

## Portée

Cette qualification ne couvre que ces deux offres, ce digest, ce tokenizer et cette version d'Ollama. Elle ne démontre pas la qualité générale sur les autres catégories, les longues offres ou les injections complexes. La revue humaine reste nécessaire et les propositions approuvées ne sont pas appliquées dans les autres services.

Les bases de qualification sont éphémères et supprimées après les tests. Aucune base persistante utilisateur n'a été migrée, aucun vrai profil/CV traité et aucune candidature envoyée. La configuration du modèle est prête à charger ; l'API Agents persistante sur 5008 n'a pas été démarrée. Sa connexion PostgreSQL, son identité et ses quotas restent ceux du guide API. Aucun commit/push ni changement de branche.

La suite de la phase 88 porte sur les autres agents et leurs jeux d'évaluation. Les files et traitements asynchrones restent en phase 89.
