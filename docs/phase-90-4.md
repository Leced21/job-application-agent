# Phase 90.4 — consultation des préparations

La route `/preparations` ajoute un écran de consultation des dossiers préparés de Documents. Depuis l’historique, le candidat peut ouvrir un dossier et lire l’instantané de l’offre, le CV/version source, les faits sélectionnés avec leurs extraits de preuve et le contenu composé lorsqu’il existe. Les notices de vérification, la décision de revue et l’empreinte du contenu restent visibles.

L’écran utilise le Bearer Documents local déjà saisi dans la bibliothèque de CV tant que l’application est ouverte ; en accès direct ou après expiration, il le redemande. Un 401 efface le jeton en mémoire. Les réponses et contenus sont affichés comme texte échappé par Angular. La liste des dossiers affiche les 100 entrées les plus récentes. Les actions explicites de composition, de revue et d’export sont décrites dans le [lot 90.5](phase-90-5.md).

Vérification : `npm run build` et `npx tsc --noEmit -p tsconfig.spec.json` réussissent ; les tests ne sont pas exécutés. Les nouveaux écrans Documents et Préparations dépassent le seuil d’avertissement SCSS de 4 Ko (7,96 et 7,21 Ko), en restant sous le seuil d’erreur de 8 Ko. Les autres avertissements de budget préexistants restent présents.
