# Phase 92.6 — métriques HTTP et Matching

Les APIs enregistrent le volume et la durée des requêtes HTTP. Les dimensions se limitent à la méthode, au modèle de route et au statut ; aucun identifiant de requête, de compte ou de ressource n’est ajouté aux métriques. Matching mesure en plus la durée et le résultat des calculs (`completed`, `job_not_found`, `profile_not_found`, `cancelled`, `failed`) et crée une activité `matching.calculate` rattachée à la trace HTTP courante.

Les instruments utilisent `System.Diagnostics.Metrics` et `ActivitySource`, sans exporter les données vers un fournisseur externe. Un listener ou un exporter OpenTelemetry devra être configuré pour les collecter centralement. Aucun tableau de bord ou aucune alerte n’est livré dans ce lot.

Vérification : compilation des APIs et du projet Matching UnitTests réussie ; tests non exécutés conformément à la consigne de travail en cours.
