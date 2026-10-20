# Phase 91.22 — cloisonnement des résultats Matching

L’API Matching exige désormais un jeton d’accès OpenIddict avec l’audience `jobagent_api`. Sans issuer valide ou bearer JWT, elle refuse la requête. Le Gateway fournit le jeton utilisateur pour le cluster Matching.

Les lectures d’un résultat ou d’une recherche sont limitées à l’identifiant de profil renvoyé par Profile pour le compte authentifié. Un identifiant de profil fourni par le client ne peut pas élargir ce périmètre. Le calcul et les recommandations utilisent aussi le profil courant. Le client Profile transmet le bearer utilisateur; le consommateur d’événements en arrière-plan garde son jeton local de service pour le profil configuré.

Agents transmet son bearer uniquement vers les routes internes Applications et Matching. Les autres adaptateurs Agents qui restent épinglés à un profil continuent de refuser les données d’un autre profil. Job conserve ses routes de catalogue d’offres publiques.

Vérification : compilation des API Matching, Agents et Gateway ainsi que des projets de tests unitaires et d’intégration Matching. Les tests n’ont pas été exécutés.
