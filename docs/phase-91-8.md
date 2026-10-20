# Phase 91.8 — protection des fichiers d’environnement locaux

Le `.gitignore` exclut maintenant `.env`, `.env.*`, `secrets/` et `*.secrets.json`, tout en autorisant le modèle non secret `.env.example`. Aucun fichier `.env` n’était suivi par Git au moment du contrôle.

Les jetons Documents, Applications, Agents et les secrets des canaux internes sont chargés depuis la configuration externe et leurs implémentations refusent de démarrer lorsque les valeurs requises sont absentes ou trop faibles. Les fichiers `appsettings` suivis ne contiennent pas de clé de configuration `LocalAuth`.

Limite relevée : le Compose Profile/Job/Matching conserve le mot de passe de développement `jobagent_dev` dans sa configuration et dans les chaînes de connexion Development suivies. Cette valeur existante maintient la compatibilité avec les volumes PostgreSQL déjà créés. Elle ne doit servir qu’au poste local, dont les ports sont désormais liés à `127.0.0.1`. La rotation vers des secrets par environnement doit être coordonnée avec les volumes de base et les variables des processus applicatifs ; elle est à traiter avec la configuration locale de la phase 93, avant tout usage partagé ou exposition réseau.

Vérification : `git check-ignore` confirme que `.env` et `.env.local` sont ignorés et que `.env.example` peut être suivi. Aucun secret réel n’a été ajouté.
