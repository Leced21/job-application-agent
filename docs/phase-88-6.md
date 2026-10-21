# Lot 88.6 — Resume Adaptation Agent

ResumeAdaptation associe des exigences citées d'une offre aux faits structurés du profil. Il exige une analyse JobAnalysis approuvée pour l'offre courante, puis vérifie que la description n'a pas changé. Les résultats ne contiennent que des références indexées et une priorité contrôlées ; aucun texte de CV n'est rédigé ou appliqué.

Le connecteur est désactivé par défaut, exige un profil épinglé et n'accepte que des sources Profile/Job locales. Voir le [guide et les commandes](agents-resume-adaptation.md). Aucun changement de schéma n'est requis.

Quatre agents sont implémentés : JobAnalysis, ProfileAnalysis, MatchingEnrichment et ResumeAdaptation. Les trois autres restent indisponibles.

Validation de ce lot : compilation complète de `JobApplicationAgent.slnx` réussie, sans avertissement ni erreur. L'API locale a été redémarrée et son catalogue authentifié expose ResumeAdaptation comme exécutable, connecteur désactivé. Le quota persistant est resté à 100000 tokens maximum, zéro consommé et zéro retenu. Les tests n'ont pas été exécutés dans cette étape.
