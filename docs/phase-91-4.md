# Phase 91.4 — noms de fichiers CV non ambigus

Le validateur des imports PDF/DOCX refuse maintenant les contrôles Unicode bidirectionnels dans le nom de fichier, notamment U+202E. Ces caractères peuvent inverser visuellement une partie du nom et faire paraître une extension différente de l'extension réelle. Le nom reste réduit à son dernier segment de chemin avant cette validation.

Un cas unitaire couvre un nom contenant un contrôle bidirectionnel. Les contrôles de format existants restent en place : taille, extension et type MIME appariés, enveloppe PDF ou structure DOCX, et téléchargement en pièce jointe avec `nosniff`.

Vérification : le projet de tests unitaires Documents compile sans avertissement ni erreur. Les tests ne sont pas exécutés dans le flux de travail demandé.

Limite : la validation PDF porte sur l'enveloppe et non sur une analyse complète ou antivirus. Les CV téléchargés ne sont jamais rendus inline ; l'analyse/quarantaine reste une évolution à planifier avant toute exposition publique.
