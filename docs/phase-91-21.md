# Phase 91.21 — propagation de l’identité dans les sources privées

Applications ne dépend plus d’un propriétaire ni d’un profil uniques configurés localement. Pour valider une candidature, il lit le profil courant de l’utilisateur dans Profile puis vérifie le document dans Documents. Un handler HTTP dédié transmet le bearer de la requête à Profile sur une origine loopback validée ; aucun cookie n’est relayé. Le reçu Documents est lu par le jeton dédié `ApplicationsChannel`, et revérifie propriétaire, profil, offre et hashes du reçu.

L’agent de suggestion de relance transmet également le bearer utilisateur à Applications. Il vérifie que le dossier retourné appartient au propriétaire du run avant d’en extraire le contexte. Sa configuration ne demande donc plus de propriétaire, profil ou jeton de service statiques.

Les autres sources Agents restent limitées par leurs profils configurés. Job et Matching n’appliquent pas encore une autorisation multi-utilisateur équivalente; leurs lectures ne sont pas ouvertes à tous les comptes par ce lot. Les tests concernés ont été compilés, mais pas exécutés.
