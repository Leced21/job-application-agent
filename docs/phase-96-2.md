# Phase 96.2 — Formaliser le garde-fou de soumission

Le domaine Applications expose une règle fail-closed pour une future intégration contrôlée. Elle refuse toute tentative lorsque l’automatisation globale est désactivée, que le dossier n’est pas approuvé ou a déjà été soumis, que les deux pièces approuvées manquent, ou que la plateforme ne présente pas une API officielle autorisée, sans CAPTCHA et avec une clé d’idempotence.

L’accord attendu est lié à l’identifiant du dossier, à sa révision exacte et à l’empreinte SHA-256 d’un aperçu canonique comprenant l’offre, l’entreprise et les identifiants/empreintes des deux exports. Toute modification invalide donc l’accord précédent. La comparaison des empreintes se fait en temps constant.

Cette règle est un contrat métier et n’est pas encore appelée par un connecteur ou une route HTTP. Aucun envoi externe ni réglage d’automatisation accessible à l’utilisateur n’est activé par cette phase. Avant tout connecteur, il restera à persister l’accord à usage unique, enregistrer l’issue et la preuve, puis relier l’arrêt global à chaque traitement en cours.

Validation : tests unitaires pour le mode désactivé, l’accord absent ou périmé, les restrictions de plateforme, les statuts de candidature et le chemin prêt.
