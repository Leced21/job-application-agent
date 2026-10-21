# Lot 88.7 — Cover Letter Agent

CoverLetter prépare des paragraphes de lettre en français à partir du profil structuré et d'une offre locale. Chaque paragraphe référence un fait du profil et une exigence issue d'une JobAnalysis approuvée pour l'offre courante ; une description modifiée depuis l'analyse est refusée. Les préférences et coordonnées sont exclues du prompt.

Le contrat strict borne la sortie et refuse les références hors plage, catégories incompatibles, associations répétées, propriétés supplémentaires, balisage et caractères de contrôle. Les formulations restent du texte généré et exigent une revue de fond ; aucune proposition n'est appliquée à Documents ni envoyée.

Le connecteur a un interrupteur et un profil épinglé propres à CoverLetter ; accès local seulement et désactivé par défaut. Aucun changement de schéma n'est nécessaire. La configuration et les requêtes figurent dans le [guide CoverLetter](agents-cover-letter.md).

Cinq agents sont exécutables : JobAnalysis, ProfileAnalysis, MatchingEnrichment, ResumeAdaptation et CoverLetter. ApplicationPreparation et FollowUpSuggestion restent à développer.

Validation : compilation complète de `JobApplicationAgent.slnx` réussie, sans avertissement ni erreur. L'API locale a été redémarrée ; son catalogue authentifié expose CoverLetter comme exécutable, avec connecteur désactivé. Le quota persistant reste à 100000 tokens maximum, zéro consommé et zéro retenu. Les projets de test compilent, mais les tests ne sont pas exécutés dans cette étape.
