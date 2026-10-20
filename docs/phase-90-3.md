# Phase 90.3 — bibliothèque de CV

La page `/documents` fournit le premier écran du parcours Documents :

- connexion au service Documents local avec le Bearer existant ; le jeton est conservé uniquement en mémoire de l’application, puis supprimé à la déconnexion ou après un 401 ;
- import et téléchargement de PDF/DOCX, avec contrôle navigateur de l’extension et de la taille maximale de 5 Mio ; les validations de contenu restent effectuées par le serveur ;
- liste des métadonnées de CV et indication du statut, de la taille et de la date d’import ;
- création explicite de dossiers, ajout de versions de fichiers importés et consultation/téléchargement de l’historique ;
- messages d’erreur adaptés aux réponses 401, 404, 409, 413 et 429.

L’écran appelle les routes de la phase 86 (`/api/v1/documents/resumes` et `/api/v1/documents/resume-series`) via la gateway locale. Il ne conserve pas les jetons après rechargement, n’envoie pas de CV vers un fournisseur externe et ne déduit aucun regroupement de versions. La pagination UI consulte jusqu’aux 100 premiers CV/dossiers et aux 100 premières versions de chaque dossier ; des contrôles de pagination visuelle restent à ajouter pour les très grands historiques.

Vérification : `npm run build` réussit. Le SCSS de cette page dépasse le seuil d’avertissement de 4 Ko, tout en restant sous le seuil d’erreur de 8 Ko ; plusieurs pages existantes et les autres écrans de la phase 90 dépassent également le seuil d’avertissement. Les tests n’ont pas été lancés. Aucun jeton, CV utilisateur, service ou base Documents n’a été utilisé pendant le développement.
