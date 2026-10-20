# Lot 88.9 — FollowUpSuggestion

FollowUpSuggestion transforme une candidature Applications admissible en suggestion de relance consultative. Il utilise une lecture locale en lecture seule, épinglée à l'identité propriétaire et au profil configurés. La source est limitée au titre, à l'entreprise, au statut actif, aux dates de soumission et de dernier retour recruteur, ainsi qu'à l'échéance et au canal d'une relance planifiée. Les notes, messages, références de soumission et coordonnées ne sont pas transmis au modèle.

La sortie est validée côté serveur : `DraftMessage` exige un texte générique borné, `Wait` et `NoFollowUp` n'en acceptent aucun. Une échéance future force `Wait`. Aucun appel ne planifie, n'envoie ni ne modifie une candidature. Le connecteur reste désactivé par défaut ; son activation exige un jeton Applications dédié et une URL loopback. Les paramètres et l'exemple d'exécution sont dans le [guide FollowUpSuggestion](agents-follow-up-suggestion.md).

Aucun changement de schéma ou de migration n'est requis. Quatre tests unitaires couvrent le gel de la source, les dates UTC, les échéances futures et la validation des brouillons. La génération d'un exemple réel n'est pas lancée pour préserver le quota et les données de candidature. Les tests ne sont pas exécutés dans cette étape.

Validation : compilation complète de `JobApplicationAgent.slnx`, redémarrage de l'API locale, contrôle du catalogue et du quota persistant. Le catalogue doit exposer sept agents exécutables et `followUpSuggestionEnabled` à `false` tant que le connecteur n'est pas configuré. Le quota n'est pas consommé par ces contrôles.
