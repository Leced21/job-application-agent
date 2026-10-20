# Phase 97.15 — lecture des mots de passe PFX depuis des fichiers secrets

Le Gateway peut maintenant recevoir `PasswordFile` à la place de `Password`, pour les certificats principaux et les certificats précédents. Le fichier est lu au démarrage ; seuls les retours à la ligne finaux sont retirés et les espaces du mot de passe sont préservés. Définir les deux options en même temps provoque une erreur explicite, et une erreur de lecture ne révèle pas le contenu du secret.

Exemple pour les certificats principaux : `OpenIddict__Certificates__Signing__PasswordFile` et `OpenIddict__Certificates__Encryption__PasswordFile`. Monter ces fichiers et les PFX en lecture seule depuis le gestionnaire de secrets de l'environnement de déploiement. Le Compose local reste en `Development` et n'a pas besoin de certificats persistants.

Validation le 19 octobre 2026 : les 20 tests unitaires Gateway réussissent, ainsi que les 3 tests d'intégration Gateway avec PostgreSQL Testcontainers. Le build Release complet de la solution réussit sans avertissement ni erreur.

La création des secrets et leur injection par un gestionnaire externe restent à configurer pour la plateforme retenue.
