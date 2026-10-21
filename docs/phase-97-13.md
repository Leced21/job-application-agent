# Phase 97.13 — certificats persistants OpenIddict hors développement

Le Gateway accepte une paire de certificats PFX configurée par environnement hors `Development`. Il exige sa base Identity, deux certificats RSA distincts avec clés privées d'au moins 2 048 bits, actuellement valides, avec `DigitalSignature` pour le certificat de signature et `KeyEncipherment` pour celui de chiffrement. Les certificats sont chargés en mémoire avec `EphemeralKeySet`; les clés ne sont pas copiées dans le conteneur. Sans base ou certificat valide, le processus échoue au démarrage au lieu de servir le proxy sans authentification.

Les paramètres sont `OpenIddict__Certificates__Signing__Path`, `OpenIddict__Certificates__Signing__Password`, `OpenIddict__Certificates__Encryption__Path` et `OpenIddict__Certificates__Encryption__Password`. Le guide de sécurité précise le stockage et le montage des PFX. Compose reste en `Development` et conserve les clés éphémères locales.

Les tests unitaires couvrent le chargement d'une paire correcte, les paramètres manquants, la réutilisation accidentelle d'un certificat et un usage de clé incorrect. Un test d'intégration démarre le Gateway en `Production` sur PostgreSQL Testcontainers, puis vérifie le document de découverte et le JWKS.

Validation initiale : les 15 tests unitaires Gateway et les 3 tests d'intégration Gateway réussissent ; le build Release complet de la solution passe sans avertissement. La prise en charge du chevauchement des clés est ajoutée en [phase 97.14](phase-97-14.md).
