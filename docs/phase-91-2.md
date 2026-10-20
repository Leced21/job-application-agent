# Phase 91.2 — en-têtes de sécurité du Gateway

Le Gateway applique désormais `Cache-Control: no-store`, `Pragma: no-cache`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` et une `Permissions-Policy` qui désactive caméra, microphone et géolocalisation. Ces en-têtes couvrent les réponses API relayées ainsi que le health check.

Cette mesure réduit le risque de conservation involontaire de réponses de profil ou de candidature par les caches et empêche quelques interprétations ou intégrations navigateur inutiles. Elle ne remplace ni l’authentification, ni l’autorisation, ni la restriction réseau.

Vérification : le projet Gateway compile sans avertissement ni erreur et la configuration YARP est un JSON valide. Les tests ne sont pas exécutés dans le flux de travail demandé.
