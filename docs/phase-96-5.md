# Phase 96.5 — Registre serveur des plateformes

Applications résout maintenant toute capacité de plateforme depuis un registre construit au démarrage à partir de `Applications:Submission`. Chaque entrée porte un identifiant et les indicateurs utilisés par la politique fail-closed : API officielle, conditions d’utilisation compatibles, absence de CAPTCHA et prise en charge d’une clé anti-doublon. Les identifiants sont limités à 64 caractères minuscules alphanumériques, points et tirets ; les doublons ou valeurs de configuration manquantes/invalides arrêtent le démarrage.

La configuration par défaut reste `Enabled: false` avec `Platforms: []`. Une valeur envoyée par le client ne peut pas créer ou remplacer une capacité : le service de réservation interne accepte un identifiant et ne poursuit que s’il existe dans le registre serveur. La politique vérifie ensuite les capacités enregistrées. L’endpoint de réservation est exposé au lot 96.7 ; aucun connecteur ni envoi externe n’est activé.

Configuration de développement, à activer uniquement après validation des capacités réelles de l’API cible :

```json
{
  "Applications": {
    "Submission": {
      "Enabled": false,
      "Platforms": [
        {
          "PlatformId": "example-official-api",
          "HasOfficialApi": true,
          "TermsAllowAutomation": true,
          "RequiresCaptcha": false,
          "SupportsIdempotencyKey": true
        }
      ]
    }
  }
}
```

Validation au lot 96.5 : 232 tests unitaires Applications réussis ; compilation de l’API Applications réussie sans avertissement. Les tests couvrent le registre vide, l’allowlist, les identifiants invalides et en doublon, ainsi que le rejet d’une plateforme inconnue avant mutation. La transition de résultat est documentée en [phase 96.6](phase-96-6.md) et l’exposition HTTP de la réservation en [phase 96.7](phase-96-7.md).
