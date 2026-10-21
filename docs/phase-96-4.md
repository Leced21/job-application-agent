# Phase 96.4 — Réserver une tentative sans doublon

Le domaine Applications peut maintenant préparer une tentative liée à l’accord enregistré. La réservation contient une clé idempotente, la révision approuvée, l’empreinte relue, les capacités déclarées de la plateforme et la date UTC. Elle est ajoutée à l’historique JSON de la candidature dans la transaction et sous verrou de ligne déjà utilisés par les mutations Applications.

La première réservation exige l’interrupteur d’automatisation actif, l’accord explicite exact, une candidature approuvée et les capacités de plateforme autorisées par la politique 96.2. Le service ne possède toujours aucun connecteur réseau : « préparée » signifie uniquement qu’une tentative locale a été réservée. Rejouer la même clé et les mêmes paramètres retourne le même état sans ajouter d’événement ; une autre clé est refusée. L’accord ne permet donc pas de réserver une seconde tentative. La réservation elle-même rend aussi l’aperçu indisponible.

Cette méthode reste un contrat interne au service et n’est exposée par aucune route HTTP ; le registre serveur est vide par défaut et aucun envoi externe n’est possible. La phase 96.5 ajoute ce registre configurable. La phase 96.6 ajoute le résultat authentifié de la tentative et bloque les nouveaux essais lorsque l’issue est inconnue.

Validation : les 225 tests unitaires Applications passent. Un test d’intégration PostgreSQL exécute cinq réservations concurrentes avec la même clé et vérifie qu’un seul événement est conservé.
