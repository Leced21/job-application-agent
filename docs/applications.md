# Candidatures — règles métier de la phase 87.1

Ces règles sont définies avant leur implémentation. Le namespace Applications évite la confusion avec la couche Application de chaque service.

## Identité et création

Une candidature appartient à un propriétaire et rattache un profil à une offre Job existante, active (Discovered ou Saved). Le serveur doit vérifier le profil et son propriétaire. Le titre et l'entreprise sont figés à la création. Une seule candidature est autorisée pour le triplet propriétaire/profil/offre, même après retrait. Une nouvelle clé de requête pour ce triplet retrouve la candidature existante. Une même clé de requête utilisée pour un autre triplet est un conflit. La persistance devra garantir ces deux unicités atomiquement, y compris sous concurrence.

## Machine à états

| Départ | Arrivée | Condition |
| --- | --- | --- |
| Création | Draft | Offre et profil vérifiés côté serveur |
| Draft | ReadyForReview | CV et lettre exportés, approuvés, appartenant au même propriétaire/profil/offre |
| ReadyForReview | Approved | Confirmation humaine explicite avec la révision exacte du dossier |
| ReadyForReview ou Approved | Draft | Retour explicite en édition ; nouvelle revue nécessaire |
| Draft, ReadyForReview ou Approved | Withdrawn | Retrait humain explicite et motif obligatoire |

Les pièces ne peuvent changer qu'en Draft. Le domaine reçoit des références **déjà vérifiées par le service Documents**, jamais des déclarations du navigateur. Deux pièces distinctes sont requises, CV et lettre. Chaque pièce conserve son identifiant d'export et son empreinte SHA-256. Une modification incrémente la révision ; une confirmation sur une ancienne révision échoue. Le retrait est définitif dans ce premier lot. Les changements et notes ont une date UTC, un auteur propriétaire et un numéro séquentiel ; le temps ne peut reculer. Les notes sont bornées à 2 000 caractères et ne modifient pas le statut.

Le [lot 87.3](applications-follow-up.md) permet la déclaration d'un envoi manuel déjà effectué, puis le suivi Submitted, InReview, Interview, Offer et Rejected. Les transitions et états terminaux sont précisés dans ce guide. Aucun export ni approbation n'envoie une candidature. Une déclaration exige confirmation, date et canal ; un éventuel envoi automatisé relève des phases 96–97 avec autorisation spécifique.

## Frontières et étapes suivantes

### Décisions 87.2b avant implémentation

Documents conservera un reçu immuable par export, après confirmation du propriétaire du profil configuré et de l'empreinte exacte du fichier. Le reçu reliera propriétaire, profil, offre, type, export, composition et snapshot source. Il ne prétend pas que les faits ont été transférés dans Profile : le lien au profil est une déclaration humaine distincte. Un canal de service à jeton dédié permettra à Applications de relire ce reçu, avec revalidation du fichier et de son approbation. Aucune donnée de rattachement déclarée par le navigateur ne remplacera cette vérification.

Applications vérifiera les deux reçus avant ReadyForReview et Approved, hors transaction SQL, puis vérifiera la révision sous verrou à l'écriture. Une modification concurrente fera échouer la transition. Les pièces et reçus sont immuables via leurs API ; il n'y a pas de transaction distribuée ni de garantie contre une altération administrative entre deux services. Le retour en Draft restera possible pendant une panne Documents. Aucune de ces actions ne soumettra une candidature.

87.1 fournit le domaine et le contrat applicatif de création. Le [lot 87.2a](applications-api.md) ajoute PostgreSQL, les contraintes d'unicité, l'authentification et les routes de création/lecture/notes/retrait, avec vérification des vraies sources Job/Profile. Le [lot 87.2b](applications-review.md) ajoute la confirmation des liens dans Documents et les routes de revue. Le [lot 87.3](applications-follow-up.md) ajoute le suivi des envois manuellement déclarés et des retours recruteurs ; le [lot 87.4](applications-reminders.md) ajoute les relances. Aucun stockage mémoire n'est installé en production. L'atomicité est testée sur PostgreSQL. L'historique est borné à 1 000 événements, avec une place réservée au retrait des dossiers encore actifs.
