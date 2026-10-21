# Phase 88.3 — Adaptateur Ollama local et tokenisation BPE

Le choix utilisateur est **Ollama local**. Le [guide Ollama](agents-ollama.md) précise les fichiers nécessaires, la configuration, les limites et le protocole de qualification. L'intégration est implémentée ; **son activation et sa qualification avec un modèle réel restent en attente du modèle et de son tokenizer**. Aucun serveur ne répondait sur `127.0.0.1:11434` pendant cette livraison.

## Implémenté

- Adaptateur HTTP Ollama indépendant du domaine, désactivé par défaut, sans choix de modèle ni téléchargement automatique. Origine loopback, cookies/redirections/proxy désactivés et aucun jeton utilisateur transmis au moteur.
- Préparation asynchrone bornée et annulable avant réservation : prompt brut figé, tokenizer BPE local, contrôle des métadonnées et digest du modèle. L'ancien comptage synchrone est remplacé par un résultat préparé conservé jusqu'à génération.
- Tokenisation locale Microsoft.ML.Tokenizers 2.0.0, profils Llama3 et Qwen2, vocabulaires complets contrôlés par SHA-256, rangs, couverture et volume. Délimiteurs de rôles et schéma inclus dans le comptage ; ajout BOS propre au profil. Aucun estimateur par caractères/mots.
- Convertisseur PowerShell 7 hors ligne des vocabulaires/merges Qwen2 compatibles vers tiktoken, sans écrasement ni téléchargement.
- Configuration serveur explicite du modèle/tag, digest, tokenizer, plafonds, contexte, timeout et données autorisées. Le serveur Ollama doit désactiver son cloud ; l'adaptateur refuse les métadonnées distantes et n'offre aucun fallback.
- Génération en mode brut non diffusé, JSON structuré, contexte et sortie plafonnés, aucun outil/image/historique. Nouvelle vérification du modèle juste avant envoi, validation de l'intégrité de la préparation et aucun retry.
- Réponses bornées sans dépendre de Content-Length, JSON sans propriétés dupliquées ; rejet du modèle incohérent, résultat non final, sortie tronquée, raisonnement/tool calls et incohérence entre compte local et compte rapporté.
- Consommation connue conservée même si une sortie est rejetée. Un refus prouvé avant envoi libère la réservation ; après envoi possible, usage inconnu imputé au maximum réservé. Les annulations/timeout ne laissent pas de proposition tardive.
- Trace persistée enrichie avec les empreintes du prompt brut et du tokenizer ; digest du modèle dans son identité. Aucun prompt brut ou chemin de fichier dans l'audit. Snapshots antérieurs compatibles, sans nouvelle migration.
- Catalogue enrichi de la configuration et des plafonds ; `availabilityVerified: false` distingue cette information d'une sonde réelle de disponibilité.

## Vérification

**169 tests Agents réussis : 71 unitaires et 98 dans le projet d'intégration, zéro échec et zéro ignoré.** Les compteurs des deux TRX ont été relus.

**Compilation des 44 projets réussie, zéro erreur et zéro avertissement.**

Les tests vérifient préparation sans génération, comptage BPE/UTF-8 et différences de pré-tokenisation des profils, chargement d'un vocabulaire synthétique complet épinglé par hash, artefacts invalides, caractères de contrôle du protocole, altération de préparation, métadonnées/digest/architecture incompatibles, réponses malformées/tronquées, erreurs HTTP, corps sans longueur hors borne et comptabilisation avant/après timeout. Le parcours API utilise le vrai adaptateur HTTP avec serveur Ollama simulé et PostgreSQL éphémère, puis vérifie budget, audit et reprise sans nouvelle génération.

Les fusions/vocabulaires des tests sont synthétiques. Ils ne constituent pas une qualification d'un modèle, d'un fichier tokenizer tiers ou d'une version d'Ollama. Le convertisseur PowerShell a été vérifié syntaxiquement ; sa conversion avec vos assets réels reste à vérifier.

```powershell
dotnet test tests/Agents/JobApplicationAgent.Agents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase88-3" -m:1 --disable-build-servers
dotnet test tests/Agents/JobApplicationAgent.Agents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase88-3" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase88-3" -m:1 --disable-build-servers
```

Aucun modèle/tokenizer téléchargé, aucun appel réel de génération, aucune configuration utilisateur activée, aucune base persistante migrée, aucun commit/push ou changement de branche. Les autres suites et Angular ne sont pas exécutés ; leurs projets .NET participent à la compilation de solution.

## Suite

Identifier le modèle local et son tokenizer exact, vérifier les métadonnées et la désactivation du cloud côté Ollama, puis effectuer une analyse synthétique et contrôler concordance des tokens, citations, reprise, budget et revue. Les profils non pris en charge nécessitent une implémentation spécifique ; ne pas contourner les contrôles pour les activer.

Les six autres agents restent à développer avec leurs schémas, sources et validations. L'orchestration asynchrone reste en phase 89. Aucun résultat, même approuvé, n'est appliqué dans les autres services et aucune candidature n'est envoyée.
