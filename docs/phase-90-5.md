# Phase 90.5 — revue et export des préparations

La page `/preparations` relie maintenant la consultation du dossier aux routes de composition et d'export Documents :

- composer explicitement le contenu déterministe depuis l'instantané immuable ; aucun modèle externe n'est appelé ;
- relire le contenu, les notices, les sources et son SHA-256 avant la décision ;
- approuver ou rejeter après avoir coché une confirmation distincte et facultativement saisi une note ; la décision est finale selon le contrat Documents et porte sur l'empreinte exacte ;
- générer un PDF ou un DOCX uniquement après approbation, puis télécharger le fichier conservé ; les exports déjà générés restent téléchargeables.

Les routes existantes vérifient de nouveau l'identité propriétaire, la revue, le hash et les limites du fichier. La génération reste locale au service Documents : aucun envoi au recruteur ni soumission de candidature n'est ajouté. Le bloc d'actions est un composant séparé pour maintenir chaque feuille de style sous le plafond d'erreur Angular de 8 Ko.

Vérification : `npm run build` et `npx tsc --noEmit -p tsconfig.spec.json` réussissent ; les tests ne sont pas exécutés. Les avertissements SCSS existants restent sous le seuil d'erreur. Aucun vrai CV, export utilisateur ou jeton n'a été utilisé.
