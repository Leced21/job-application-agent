# Lot 88.8 — Application Preparation Agent

ApplicationPreparation produit une évaluation de preuve indexée pour chacune des exigences d'une offre. Le modèle peut associer jusqu'à trois faits existants et classer le niveau de preuve `RelevantEvidence`, `PotentialEvidence` ou `NoEvidence`. La sortie ne contient pas de texte libre. Le serveur vérifie l'exhaustivité, l'unicité, les bornes des index et la compatibilité des catégories.

La source Profile/Job est construite uniquement si le connecteur dédié est activé et un profil épinglé. Comme pour ResumeAdaptation et CoverLetter, une JobAnalysis doit être approuvée pour l'offre demandée et sa description doit rester identique. Contrairement à ces agents rédactionnels, une source sans fait profil ou sans exigence reste évaluable et peut produire un tableau vide ou des statuts `NoEvidence`.

La proposition sert à examiner les éléments disponibles avant de sélectionner les faits et documents dans Documents. Elle ne choisit pas de CV, ne compose pas de document, ne crée pas de candidature et ne change aucun état Applications. Le guide [ApplicationPreparation](agents-application-preparation.md) décrit activation et utilisation.

Six agents sont exécutables : JobAnalysis, ProfileAnalysis, MatchingEnrichment, ResumeAdaptation, CoverLetter et ApplicationPreparation. FollowUpSuggestion reste à développer.

Validation : compilation complète de `JobApplicationAgent.slnx` réussie, sans avertissement ni erreur. L'API locale a été redémarrée ; son catalogue authentifié expose ApplicationPreparation comme exécutable avec connecteur désactivé. Le quota persistant reste à 100000 tokens maximum, zéro consommé et zéro retenu. Les projets de test compilent, mais les tests ne sont pas exécutés dans cette étape.
