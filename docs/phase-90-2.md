# Phase 90.2 — fiche détaillée d’une offre

Le catalogue disposait d’un panneau de prévisualisation, mais aucune URL stable ne permettait d’ouvrir une offre complète. Ce lot ajoute `/jobs/:id`, qui récupère les données à jour avec `GET /api/v1/jobs/{id}`.

Depuis le panneau de prévisualisation du catalogue, « Fiche complète » ouvre cette route. La page affiche un état de chargement, un message adapté aux réponses 404 et une fiche réutilisant le composant de détail existant. Si l’offre fournit une URL d’origine, elle devient un lien externe ouvert avec les protections `noopener noreferrer`.

Les commandes de favoris, de candidature et les onglets sans comportement ont été retirés de la fiche pour ne pas présenter des actions inactives. L’accès à l’offre d’origine et le retour au catalogue sont disponibles.

Vérification : `npm run build` réussit. Les avertissements SCSS restants concernent le tableau de bord, les recommandations et plusieurs pages préexistantes ; le composant de fiche d’offre respecte le budget configuré. Les tests n’ont pas été lancés.
