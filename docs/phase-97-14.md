# Phase 97.14 — chevauchement des certificats OpenIddict

Le Gateway accepte maintenant un certificat principal et une liste indexée de certificats précédents pour la signature et le chiffrement. Tous les certificats sont chargés simultanément, validés comme certificats PFX RSA privés, vérifiés pour leur période de validité et leurs usages de clé, puis enregistrés dans OpenIddict. Les certificats configurés doivent tous être distincts. Les anciennes clés de signature restent publiées dans le JWKS durant le chevauchement.

Pour ajouter une clé précédente, définir par exemple `OpenIddict__Certificates__Signing__Previous__0__Path` et `OpenIddict__Certificates__Signing__Previous__0__Password`. Le même format s'applique à `Encryption`. Les index numériques commencent à zéro. La documentation de sécurité précise le déploiement simultané et le retrait une fois les jetons/artefacts protégés expirés ou révoqués.

Les tests unitaires valident le chargement de deux certificats par usage et le rejet des certificats réutilisés. Le test d'intégration du Gateway en `Production` configure une ancienne clé de signature et vérifie que le JWKS publie les deux clés pendant le chevauchement.

Validation le 19 octobre 2026 : 17 tests unitaires Gateway réussis, 3 tests d'intégration Gateway réussis avec PostgreSQL Testcontainers, y compris la vérification de deux clés publiées par le JWKS, puis build Release complet de la solution réussi avec zéro avertissement et zéro erreur.

Le déploiement auprès d'un gestionnaire de secrets, la sauvegarde/restauration et le cycle opérationnel complet de remplacement/retrait d'une clé restent à tester pour l'environnement cible.
