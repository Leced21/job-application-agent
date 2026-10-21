# Phase 92.7 — métriques du transport événementiel

Les relays Job et Collection mesurent les dispatches outbox et leur durée, avec les dimensions bornées `service` et `outcome` (`published` ou `failed`). Le consommateur Matching mesure les livraisons traitées, rejetées ou remises en file, ainsi que leur durée. Aucun identifiant d’événement, de corrélation ou d’utilisateur n’est enregistré comme dimension.

Les instruments sont exposés via `System.Diagnostics.Metrics` dans le Meter `JobApplicationAgent.EventTransport`. Ils restent accessibles aux outils locaux compatibles avec les Meters .NET. L’export centralisé OpenTelemetry/Prometheus est reporté : aucun SDK ou exporter OpenTelemetry n’est présent dans le cache NuGet local et la restauration de nouveaux packages n’est pas disponible dans cet environnement.

Vérification : compilation des APIs Job, Collection et Matching ; tests compilés selon la consigne en cours, sans exécuter les suites.
