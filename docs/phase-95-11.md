# Phase 95.11 — Export contrôlé du contenu approuvé

Le parcours Documents vérifie qu'aucun bouton d'export PDF n'est disponible avant la revue humaine. Après confirmation et approbation liées à l'empreinte du contenu, le candidat peut demander un export PDF ; le scénario contrôle le contrat envoyé et la récupération du fichier simulé.

Les endpoints Documents sont interceptés par Playwright. Le fichier de sortie est synthétique et aucune candidature n'est transmise à un recruteur. La CI exécute le scénario dans Chromium.

Validation locale : formatage, compilation TypeScript et découverte des tests sont vérifiés sans lancer le navigateur.
