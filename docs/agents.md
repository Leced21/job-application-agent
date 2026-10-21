# Agents IA — socle, stockage et agents locaux 88.1 à 88.9

Le [lot 88.4](phase-88-4.md) ajoute ProfileAnalysis : questions sur les rubriques absentes, à partir d'indicateurs de présence uniquement. L'accès au profil est désactivé par défaut et exige un UUID autorisé épinglé côté serveur ; voir le [guide ProfileAnalysis](agents-profile-analysis.md).

Le [lot 88.5](phase-88-5.md) ajoute MatchingEnrichment : priorisation de vérifications à partir de signaux dérivés des résultats Matching. Trois agents sont implémentés ; quatre restent indisponibles. Le connecteur Matching est désactivé par défaut et exige un profil épinglé ; voir le [guide MatchingEnrichment](agents-matching-enrichment.md).

Le [lot 88.6](phase-88-6.md) ajoute ResumeAdaptation : associations entre les exigences d'une offre et les faits confirmés du profil, à partir d'une JobAnalysis approuvée. Quatre agents sont implémentés ; trois restent indisponibles. Aucun texte de CV n'est généré ou modifié ; voir le [guide ResumeAdaptation](agents-resume-adaptation.md).

L'orchestration reste indépendante du fournisseur, sans connexion externe ni action dans les autres services. L'adaptateur [Ollama local 88.3](agents-ollama.md) est disponible sur configuration explicite et reste désactivé par défaut. Aucun modèle n'est téléchargé ou choisi automatiquement.

Le [lot 88.7](phase-88-7.md) ajoute CoverLetter : paragraphes de brouillon rattachés à un fait du profil et à une exigence approuvée. Le connecteur est distinct et désactivé par défaut ; voir le [guide CoverLetter](agents-cover-letter.md).

Le [lot 88.8](phase-88-8.md) ajoute ApplicationPreparation : évaluation des faits disponibles pour chacune des exigences approuvées, sans création de candidature. Le connecteur local dédié est désactivé par défaut ; voir le [guide de préparation](agents-application-preparation.md).

Le [lot 88.9](phase-88-9.md) ajoute FollowUpSuggestion : brouillon de message ou conseil d'attente à partir du statut et des dates d'une candidature Applications. L'accès est en lecture seule, épinglé et désactivé par défaut ; voir le [guide des relances](agents-follow-up-suggestion.md).

Les sept agents prévus sont exécutables. JobAnalysis extrait des citations exactes d'une offre figée. ProfileAnalysis propose des questions sur les rubriques absentes. MatchingEnrichment rapporte les signaux existants avec une priorité de revue, sans recalculer les scores. ResumeAdaptation relie des exigences approuvées à des faits du profil. CoverLetter rédige des paragraphes qui citent un fait et une exigence. ApplicationPreparation évalue les éléments de preuve. FollowUpSuggestion rédige un brouillon de relance sans l'envoyer.

Le workflow réserve une clé propriétaire/requête avant de lire une source propriétaire, construit un prompt versionné séparant instructions et données non fiables, contrôle le volume d'entrée, réserve le coût et les tokens maximaux, appelle un modèle avec timeout, valide sa sortie puis conserve une proposition à revoir. Une reprise terminée retourne le résultat initial ; une requête déjà active n'est pas exécutée deux fois. Aucun retry automatique après appel modèle.

Chaque agent valide sa sortie contre son propre schéma sans champ d'action, outil ou destination. JobAnalysis exige les citations exactes de l'offre ; MatchingEnrichment doit reprendre exactement les focus déterminés par les scores existants. Les instructions contenues dans une offre restent des données. Ces contrôles réduisent la surface d'injection mais ne garantissent pas la qualité sémantique du modèle. Aucune proposition n'est appliquée automatiquement ; la décision humaine exige propriétaire, révision et empreinte exacts.

Les limites et tarifs sont une politique serveur. Le port modèle doit compter les tokens avec son tokenizer/protocole et appliquer les caps reçus. La réservation couvre les caps d'entrée et de sortie, pas seulement une estimation optimiste. La consommation réelle est comptabilisée si connue, même pour une sortie rejetée. Si un timeout/une erreur rend la consommation inconnue, la réservation entière est imputée ; aucun remboursement supposé. Un dépassement déclaré par l'adaptateur est journalisé et la sortie refusée ; la bibliothèque ne peut annuler un coût déjà engagé par un fournisseur défaillant.

Le coût monétaire est calculé à partir du barème serveur, pas lu sur une facture. Les tests utilisent exclusivement des tarifs synthétiques ; un modèle local peut avoir un barème nul tout en consommant le quota de tokens. Une réservation dont l'accusé est perdu avant tout appel modèle est clôturée avec consommation nulle ; le dépôt doit refuser une réservation tardive après clôture et libérer toute réservation interne existante.

Le dépôt PostgreSQL assure atomiquement unicité, quotas, clôture et comparaison de révision. Le journal contient codes de décision, identités techniques, versions, hashes et consommation, sans texte des sources/prompts ni erreur brute du fournisseur. Le résultat conservé contient la source figée et les citations : leur accès HTTP est authentifié ; le chiffrement au repos reste à industrialiser. Les dépôts, sources et modèles simulés sont uniquement dans les tests. Le [guide API 88.2](agents-api.md) décrit stockage, migration et démarrage ; le modèle est désactivé par défaut ; l’instance locale utilise Ollama configuré explicitement.

## Contrats et états

Six projets composent le service : Agents.Domain, Agents.Application, Agents.Infrastructure, Agents.Api, Agents.UnitTests et Agents.IntegrationTests. Aucun autre service ne référence ce socle pour le moment ; Agents lit l'API Job locale.

| Élément | Rôle |
| --- | --- |
| `IAgentSourceReader` | Lire une source figée selon le type d’agent ; l'adaptateur local contrôle le propriétaire configuré et l'identifiant de l'offre du catalogue Job partagé |
| `IAgentModel` | Préparer et compter le prompt exact sans génération, avec I/O bornées/annulables, puis générer une réponse bornée après réservation |
| `IAgentRunRepository` | Réserver la clé, le quota, finaliser l'état/comptabilité/audit atomiquement, lire par propriétaire et comparer la révision lors d'une décision |
| `AgentOrchestrator` | Exécuter le pipeline et classer ses échecs sans exposer les erreurs brutes |
| `AgentReviewService` | Approuver ou rejeter la proposition exacte, sans l'appliquer ailleurs |
| `AgentAuditEntry` | Projection technique du journal, dépourvue des sources, sorties textuelles et notes libres de revue |

États : Running → PendingReview → Approved ou Rejected ; Running → Failed en cas de source invalide, quota, timeout, annulation, réponse/usage invalide ou indisponibilité. Les décisions sont finales ; la même clé retourne l'état conservé, y compris après rejet ou panne. Un nouveau calcul demande une nouvelle clé et un nouveau budget. Une clé ne peut pas changer d'agent ou de ressource, et les clés sont propres au propriétaire.

La revue exige confirmation explicite, révision exacte et SHA-256 de la sortie canonique ; la note facultative est limitée à 1 000 caractères. Le dépôt doit sauvegarder la décision et l'audit dans la même transaction. Si la finalisation ne peut être confirmée sous deux secondes, aucun résultat à revoir n'est rendu ; l'accusé de persistance peut rester incertain et doit être résolu par une relecture/reprise sûre, pas un nouvel appel automatique au modèle.

La bibliothèque borne les attentes asynchrones, même si un modèle simulé ignore l'annulation. Cela ne prouve pas que le fournisseur a arrêté son calcul : l'adaptateur doit limiter le transport, honorer l'annulation et ne pas produire d'effets métier. Les opérations purement locales de comptage doivent elles-mêmes être bornées. Une réponse tardive d'un modèle après timeout n'est pas publiée. Le stockage 88.2 permet de retrouver la requête et clôturer une exécution Running après un bail de cinq minutes, sans relancer de modèle ; une réservation abandonnée est alors imputée au maximum avec usage inconnu.

## Prompt et sortie actuels

Le prompt **job-analysis-fr-v2** conserve séparément les instructions système, un objet JSON de données non fiables et le schéma de sortie. Il ajoute jusqu'à 96 repères lexicaux avec offsets UTF-16 calculés par le serveur, sans les classer en exigences ; le texte entier reste présent et la validation des citations reste inchangée. La trace comprend l'empreinte de l'ensemble et la version. Le schéma **job-analysis-v1** accepte uniquement :

```json
{
  "schemaVersion": "job-analysis-v1",
  "requirements": [
    { "category": "Skill", "value": "SQL", "start": 0, "length": 3 }
  ]
}
```

Le tableau peut être vide ; au maximum 30 citations. Catégories : Skill, Experience, Education, Certification, Language, Other. Une citation de 1 à 1 000 caractères sans contrôle doit correspondre exactement à l'intervalle UTF-16 indiqué dans la source. Les ranges dupliqués, propriétés supplémentaires ou dupliquées, découpes de surrogate Unicode, markdown, schémas inconnus et sorties hors borne sont refusés. Le modèle n'enrichit pas les offres ni les profils existants.

Limites absolues : source de 16 000 caractères et 32 Kio UTF-8 valide, sortie de 32 Kio, politique d'entrée de 1 à 32 768 tokens, sortie de 1 à 4 096 tokens, timeout de 1 ms à 2 minutes. Ces bornes absolues ne remplacent pas le comptage réel du prompt avec son enveloppe. Les erreurs ne déclenchent aucun retry automatique. L'adaptateur Ollama ajoute ses bornes de transport et de tokenisation ; voir son guide.

## Exécution et suite

Les routes authentifiées, la configuration et la migration PostgreSQL sont décrites dans le [guide API](agents-api.md). Les tests ordinaires conservent les modèles simulés ; les intégrations utilisent PostgreSQL éphémère et une vraie API Job de test. Une suite explicitement activée a également qualifié le modèle Ollama local réel sur deux offres synthétiques ; voir le [bilan](phase-88-3-live.md). Aucun fournisseur distant n'est contacté.

```powershell
dotnet test tests/Agents/JobApplicationAgent.Agents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase88-2" -m:1 --disable-build-servers
dotnet test tests/Agents/JobApplicationAgent.Agents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase88-2" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase88-2" -m:1 --disable-build-servers
```

**Suite : phase 89, RabbitMQ et communication asynchrone.** L'adaptateur accepte les profils Llama3 et Qwen2 vérifiés, avec coût USD local nul et quotas de tokens persistants ; qwen2.5:7b est qualifié sur les cas synthétiques décrits dans les bilans. Les effets métier sensibles conservent leurs phases et validations humaines.
