# Agents — Modèle local Ollama (88.3)

L'adaptateur utilise uniquement un serveur **Ollama local**, explicitement configuré. Il ne télécharge ni ne choisit de modèle, n'utilise aucun fournisseur distant et reste **Disabled par défaut**. Le modèle **qwen2.5:7b sur Ollama 0.40.5** a été qualifié sur deux offres synthétiques ; voir le [bilan de qualification](phase-88-3-live.md). Cette validation ciblée ne garantit pas la qualité sur toute offre réelle.

Les garanties du [socle](agents.md) et les routes du [guide API](agents-api.md) restent applicables. Aucune candidature n'est envoyée et une proposition approuvée n'est pas appliquée aux autres services.

## Modèle et tokenizer requis

Ollama fournit des compteurs d'usage dans la réponse de génération, mais son API publique ne fournit pas de tokenisation préalable. L'adaptateur prépare donc localement un prompt brut et le compte avec **Microsoft.ML.Tokenizers 2.0.0**, sans estimation par caractères ou mots. Il contrôle ensuite la concordance avec `prompt_eval_count` : tout écart produit `UsageViolation`, avec conservation de la consommation rapportée et sans proposition à revoir.

Deux profils de protocole/tokenizer sont pris en charge :

| Profil | Métadonnées exigées dans `/api/show` | Vocabulaire local |
|---|---|---|
| `Llama3` | architecture `llama`, tokenizer `gpt2`, pré-tokenizer `llama-bpe`, ajout BOS `true` | 128000 entrées BPE, format tiktoken (le fichier `tokenizer.model` original de Llama 3 est dans ce format) |
| `Qwen2` | architecture `qwen2`, tokenizer `gpt2`, pré-tokenizer `qwen2`, ajout BOS `false` | 151643 entrées BPE, format tiktoken, issu du tokenizer exact du modèle |

Chaque ligne tiktoken contient `base64_des_octets rang`. Le fichier est borné à 16 Mio, son SHA-256 est obligatoire et ses rangs/pièces doivent être uniques, complets et couvrir les 256 octets. Les fichiers SentencePiece nommés `.model` ne sont pas des vocabulaires tiktoken. Aucun téléchargement implicite de tokenizer n'a lieu.

Les empreintes figent séparément le modèle et le fichier tokenizer ; elles ne prouvent pas à elles seules que les deux artefacts correspondent. Leur association correcte relève de la configuration et de la qualification opérateur. Le contrôle des compteurs détecte une divergence sur le prompt exécuté, sans prouver l'équivalence des tokenizers sur tous les textes possibles.

Pour le modèle qualifié, le vocabulaire et les règles de fusion ont été extraits directement du GGUF installé via les métadonnées détaillées d'Ollama. Le script ci-dessous vérifie le digest avant/après lecture et le mode cloud désactivé avant de créer un nouveau répertoire ; il ne télécharge aucun fichier :

```powershell
./tools/Export-OllamaQwen2Tokenizer.ps1 -ModelName 'qwen2.5:7b' -ExpectedDigest '845dbda0ea48ed749caafd9e6037047aa19acfcfd82e704d7ca97d631a0b697e' -OutputDirectory '.local/agents/qwen2-new-export'
```

L'export présent sur cette machine est dans `.local/agents/qwen2.5-7b-845dbda0ea48-qualified/`, ignoré par Git. Depuis la racine du dépôt, charger sa configuration vérifiée dans le terminal courant :

```powershell
. ./tools/Use-QualifiedOllama.ps1 -ProvenancePath '.local/agents/qwen2.5-7b-845dbda0ea48-qualified/provenance.json'
```

Cette commande configure uniquement le modèle pour les processus lancés depuis ce terminal. La connexion PostgreSQL, l'identité et les quotas du service Agents restent ceux du guide API ; aucun serveur Agents persistant n'est démarré par ce script.

Ces profils ne prennent pas en charge tous les modèles d'Ollama : variantes multimodales, tokenizers différents, modèles à raisonnement ou templates différents nécessitent leur adaptateur/qualification. Des métadonnées requises absentes entraînent un refus, pas une hypothèse de compatibilité. Un alias doit inclure son tag explicite et rester immuable pendant les requêtes. Le digest configuré est celui du modèle listé par `/api/tags`, sans préfixe `sha256:`.

Pour Qwen2, si vous disposez localement des `vocab.json` et `merges.txt` exacts du modèle, le convertisseur **hors ligne** fourni vérifie que les priorités de fusion sont compatibles avec les rangs BPE :

```powershell
# PowerShell 7 ; chemins locaux vers des fichiers déjà acquis et autorisés
./tools/Export-Qwen2Tokenizer.ps1 -VocabPath 'C:/models/tokenizer/vocab.json' -MergesPath 'C:/models/tokenizer/merges.txt' -OutputPath 'C:/models/tokenizer/qwen2.tiktoken'
```

Il refuse d'écraser un fichier existant et ne lance aucun code issu du modèle. Il affiche le SHA-256 à reporter en minuscules. Les assets du modèle restent hors Git et doivent provenir d'une source de confiance avec les droits d'utilisation adaptés. La conversion a été vérifiée avec le vocabulaire réel extrait du modèle qualifié ; un refus de compatibilité sur un autre modèle ne doit pas être contourné.

## Configuration explicite

Conserver la connexion Agents, l'identité locale et les quotas du [guide API](agents-api.md). Aucune migration supplémentaire n'est nécessaire pour 88.3 ; les nouvelles traces sont des champs JSON optionnels, compatibles avec les snapshots antérieurs.

Configurer **le serveur Ollama lui-même** avec `OLLAMA_NO_CLOUD=1`, puis le démarrer/reprendre avec cette valeur. Un simple accès à localhost ne désactive pas les modèles cloud d'Ollama. Le drapeau Agents `LocalServerCloudDisabled` atteste cette configuration opérateur ; il ne modifie pas le processus Ollama. L'adaptateur exige aussi que `/api/status` confirme `cloud.disabled: true` avant préparation et génération. Un état absent, une route non prise en charge ou un cloud actif font refuser l'appel. Cette vérification a été qualifiée sur Ollama 0.40.5. Les réponses signalant un modèle distant sont aussi refusées.

Renseigner ces paramètres serveur hors Git après avoir identifié le modèle et le tokenizer :

| Variable | Valeur |
|---|---|
| `Agents__Model__Provider` | `Ollama` pour activer ; `Disabled` par défaut |
| `Agents__Model__BaseUrl` | `http://127.0.0.1:11434` par défaut ; origine loopback uniquement |
| `Agents__Model__ModelName` | Nom **avec tag** tel que listé par Ollama |
| `Agents__Model__ModelDigest` | Empreinte de 64 caractères hexadécimaux minuscules, issue de `/api/tags` |
| `Agents__Model__TokenizerProfile` | `Llama3` ou `Qwen2`, correspondant réellement au modèle |
| `Agents__Model__TokenizerPath` | Chemin absolu du vocabulaire BPE local |
| `Agents__Model__TokenizerSha256` | Empreinte SHA-256 du fichier, en minuscules |
| `Agents__Model__MaxInputTokens` | Plafond incluant tout le prompt et ses tokens de protocole ; 1–32768 |
| `Agents__Model__MaxOutputTokens` | 1–4096 |
| `Agents__Model__ContextSize` | 512–65536 ; au moins entrée maximale + sortie maximale + 1, et compatible avec le modèle |
| `Agents__Model__TimeoutSeconds` | 1–120 secondes pour le workflow complet |
| `Agents__Model__AllowJobDescriptions` | `true` pour autoriser explicitement la description Job locale comme donnée du modèle |
| `Agents__Model__LocalServerCloudDisabled` | `true` après configuration effective de `OLLAMA_NO_CLOUD=1` côté serveur |

Exemple de limites à ajuster au modèle choisi : entrée 4096, sortie 1024, contexte 8192, timeout 120 secondes. Le coût USD configuré par cet adaptateur local est nul ; le quota de tokens reste obligatoire et ne représente pas la consommation électrique ou matérielle.

Pour identifier les modèles déjà installés, sans en télécharger :

```powershell
Invoke-RestMethod 'http://127.0.0.1:11434/api/tags' | Select-Object -ExpandProperty models | Select-Object name, digest
$metadata = @{ model = $env:Agents__Model__ModelName; verbose = $false } | ConvertTo-Json
Invoke-RestMethod 'http://127.0.0.1:11434/api/show' -Method Post -ContentType 'application/json' -Body $metadata
Get-FileHash -LiteralPath $env:Agents__Model__TokenizerPath -Algorithm SHA256
dotnet run --project src/services/Agents/JobApplicationAgent.Agents.Api --launch-profile http
```

Ne pas activer Ollama sans le fichier et l'empreinte correspondants. Une configuration ou un fichier invalide fait échouer le démarrage. Une indisponibilité du serveur/modèle à l'exécution donne un échec persisté ; aucun basculement distant ni retry automatique n'est tenté.

## Exécution et contrôle

Le catalogue expose fournisseur, identité `nom@sha256:digest`, profil et empreinte du tokenizer, plafonds et `availabilityVerified: false`. Pour compatibilité, `modelAvailable` indique **la configuration active**, pas une vérification de disponibilité ni de qualité. Chaque nouvelle exécution vérifie les métadonnées du modèle avant réservation, puis à nouveau avant génération ; le catalogue ne contacte pas Ollama.

Le prompt brut assemble instructions système, schéma JSON, description déjà encodée comme donnée non fiable et délimiteurs de rôles du profil. Le prompt `job-analysis-fr-v2` ajoute au plus 96 repères lexicaux de 64 caractères avec coordonnées UTF-16 calculées par le serveur ; ils ne constituent pas des exigences prédéfinies et ne remplacent pas le texte entier. Le modèle doit sélectionner les compétences et distinguer les mots de liaison/modalités. Toute citation produite reste validée contre la source exacte, même si elle reprend un repère. Le prompt est limité à 64 Kio et refuse les séquences non espacées de plus de 2048 caractères pour borner la tokenisation locale. Les délimiteurs littéraux injectés dans les entrées sont refusés ; les caractères de la description sont échappés par le sérialiseur du prompt. Llama3 compte le BOS automatiquement ajouté par le moteur sans le dupliquer dans le prompt brut.

Après réservation, `/api/generate` reçoit le prompt exactement préparé avec `raw: true`, `stream: false`, `think: false`, schéma structuré, contexte et `num_predict` explicites. Aucune image, aucun outil, historique de conversation ou identifiant utilisateur n'est envoyé. Les cookies, redirections et proxy HTTP système sont désactivés. Le client ne dispose d'aucun appel de pull/push/suppression de modèle. Le transport HTTP nommé Ollama ne journalise pas les requêtes via les loggers du client ; vérifier séparément les logs du serveur local.

Les réponses sont bornées, y compris sans Content-Length : liste 1 Mio, métadonnées 2 Mio, génération 256 Kio ; la sortie métier reste limitée à 32 Kio. Les propriétés JSON dupliquées, réponse non finale, modèle différent, signal distant ou usage invalide ne donnent aucune proposition. Une sortie tronquée par `num_predict`, du raisonnement ou des tool calls sont refusés avec leur usage connu. Les citations et offsets passent ensuite par la validation métier habituelle.

Avant envoi de génération, un refus vérifié libère la réservation avec consommation nulle. Après envoi possible, une réponse perdue/inexploitable ou un timeout impute le maximum réservé comme usage inconnu. Une consommation valide est conservée même si la sortie est rejetée. Une réponse tardive ne publie rien. Le client ne peut garantir l'arrêt instantané du calcul après annulation ; le serveur doit respecter les plafonds reçus.

L'audit conserve désormais les empreintes du prompt brut préparé et du tokenizer, en plus du prompt métier versionné et du digest de modèle dans son identité. Il ne contient pas le prompt brut ni le chemin du tokenizer. Les résultats se rejouent depuis PostgreSQL sans rappeler Ollama, même après changement de configuration.

## Qualification et extension

1. Identifier le nom/tag/digest du modèle installé et fournir son tokenizer exact.
2. Vérifier les métadonnées et désactiver le cloud dans le serveur local.
3. Activer la configuration, analyser une offre synthétique, contrôler l'absence de `UsageViolation` et la pertinence des citations.
4. Rejouer la même requestId et contrôler budget, audit et revue humaine avant d'utiliser des offres réelles.

Les régressions ordinaires utilisent des serveurs simulés et vocabulaires synthétiques. Deux tests réels supplémentaires ont vérifié qwen2.5:7b et son tokenizer extrait, en anglais et en français avec emoji, sur API de test et PostgreSQL éphémère. Ils ne prouvent ni la compatibilité d'une autre version/modèle ni la qualité générale de l'analyse. Ils sont désactivés par défaut et s'activent explicitement ; voir le bilan.

Références de contrat : [génération Ollama](https://docs.ollama.com/api/generate), [liste des modèles](https://docs.ollama.com/api/tags), [API et métadonnées Ollama](https://github.com/ollama/ollama/blob/main/docs/api.md), [tokenisation Llama 3](https://github.com/meta-llama/llama3/blob/main/llama/tokenizer.py), [tokenisation Qwen2](https://github.com/huggingface/transformers/blob/v4.45.2/src/transformers/models/qwen2/tokenization_qwen2.py), [bibliothèque Microsoft.ML.Tokenizers](https://www.nuget.org/packages/Microsoft.ML.Tokenizers/2.0.0).
