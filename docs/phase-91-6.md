# Phase 91.6 — adresses d’écoute des API locales

Les profils `dotnet run` du Gateway et des API Profile, Job, Matching, Collection, Documents, Applications et Agents utilisent des URL `localhost`. Les ports de développement ne sont donc pas explicitement liés à toutes les interfaces par ces profils. Les URL de destination du Gateway pointent elles aussi vers les services locaux.

Le dépôt ne définit pas encore de conteneur Compose pour ces API. Le Dockerfile Profile déclare le port interne 8080, sans publication hôte dans Compose. Dans une future topologie conteneurisée, les API devront écouter sur l’interface du conteneur pour que le Gateway puisse les joindre, tout en restant sur un réseau Docker interne sans ports publiés. Lier ces conteneurs à leur loopback empêcherait leur communication avec le Gateway.

Vérification : les fichiers launchSettings des huit applications ont été examinés et les fichiers Compose existants restent valides (`docker compose config --quiet`). Aucun port d’API n’est publié par les Compose présents. Cette tranche documente l’état sans changer la topologie de lancement locale.

Limite : le lancement direct des DLL sans profil `dotnet run` peut utiliser les valeurs par défaut Kestrel et écouter sur toutes les interfaces. En attendant le Compose complet et l’authentification multi-utilisateur, ne pas lancer ces API de cette manière sur un réseau non fiable.
