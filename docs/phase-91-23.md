# Phase 91.23 — sources Agents liées à l’utilisateur connecté

Les lectures privées déclenchées par Agents réutilisent maintenant le bearer OpenIddict de la requête courante pour appeler Profile et Matching. Les sources ne dépendent plus d’un propriétaire statique ni d’un profil épinglé dans leur configuration. Profile confirme que le profil retourné correspond à la ressource demandée ; Matching autorise le résultat par rapport au profil du compte avant de le retourner. Les analyses de profil, adaptations de CV, lettres et préparations utilisent cette lecture de profil vérifiée.

La fixture démarre maintenant le vrai Gateway avec sa base Identity migrée, crée un compte de test et demande un jeton client-credentials à `/connect/token`. Elle lit l’émetteur et la clé publique via les métadonnées OpenIddict pour configurer Profile, Matching et Agents. Les intégrations transmettent le jeton émis par le Gateway dans les appels Agents → Profile/Matching. Les cas de profil désactivé, d’identifiant différent et d’isolation entre comptes restent couverts. Le catalogue Job reste public.

Vérification : restauration et compilation du projet d’intégration Agents réussies ; les tests ne sont pas exécutés. La chaîne Gateway → Agents → Profile/Matching reste à valider à l’exécution. La restauration a signalé que NuGet ne pouvait pas joindre son index de vulnérabilités dans cet environnement.

La prochaine étape est d’exécuter les scénarios d’intégration et de corriger les éventuelles différences de validation ou de routage observées. Les identités locales statiques restent réservées au développement local et ne permettent pas à elles seules de propager un accès interservice protégé.
