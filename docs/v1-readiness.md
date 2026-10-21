# Critères de préparation V1

Cette checklist définit une version fonctionnelle locale centrée sur le parcours candidat. Elle ne vaut pas autorisation d'exposer les services sur Internet, de soumettre des candidatures réelles ou d'activer une collecte non autorisée. Le dépôt contient déjà une large partie du parcours : profil, recommandations, documents, préparation, revue humaine et suivi. Une V1 n'est prête qu'après validation reproductible des critères ci-dessous.

Le scénario d’inscription navigateur avec jeton antiforgery de la phase 97.10 réussit en Chromium avec Gateway simulé. Le test Gateway applique les migrations sur PostgreSQL éphémère et valide la création de deux comptes ; la [phase 97.17](phase-97-17.md) fournit un script pour les huit migrations Compose. Les phases [97.18](phase-97-18.md) et [97.19](phase-97-19.md) ajoutent à la CI le démarrage de l'infrastructure, les migrations et la readiness de la pile complète. La [phase 97.20](phase-97-20.md) ajoute l'inscription et la reconnexion Chromium réelles ; la [phase 97.30](phase-97-30.md) ajoute l'isolation de deux comptes via la pile réelle et les phases [97.31](phase-97-31.md) et [97.32](phase-97-32.md) permettent respectivement de créer le profil dans le navigateur et documentent le rétablissement local d'Identity/Compose. La [phase 97.21](phase-97-21.md) vérifie la sauvegarde et la restauration isolée des huit bases ; la [phase 97.22](phase-97-22.md) restaure le volume RabbitMQ dans un nouveau volume et démarre un broker isolé. La [phase 97.26](phase-97-26.md) regroupe les sauvegardes PostgreSQL et RabbitMQ avec l'image exacte du broker ; la [phase 97.27](phase-97-27.md) restaure toutes les bases, active le broker sur un nouveau volume et vérifie la pile complète dans la CI jetable. La [phase 97.28](phase-97-28.md) fournit un outil prudent pour attribuer l'unique profil historique sans propriétaire ; la [phase 97.29](phase-97-29.md) inventorie les propriétaires par table dans les huit bases sans exposer les données candidat. L'exécution de l'inventaire sur l'hôte local et l'examen sur copie de toute attribution historique restent requis. Un run CI vert sur le SHA livré reste requis.

## Critères d'acceptation

| Domaine | Critère de sortie | Preuve attendue |
| --- | --- | --- |
| Installation locale | Un nouveau poste Windows suit le guide sans secret suivi, base supprimée ou étape implicite. | Parcours d'installation consigné, Compose healthy, liveness/readiness OK. |
| Parcours candidat | Inscription, profil/préférences, recommandations, CV, préparation, revue humaine, candidature et relances fonctionnent de bout en bout. | Chromium CI couvre la création/reconnexion sur pile réelle, la création du profil depuis l'interface et l'isolation de deux comptes ; scénarios du parcours complet verts et validation manuelle sur données synthétiques restent requis. |
| Sécurité locale | Session BFF, antiforgery, authentification des API privées et isolation des données candidat par compte vérifiées. | Tests 401/403, absence de jetons dans Angular et tests d'accès croisé sur les ressources privées. |
| Données | Migrations ordonnées, sauvegarde et restauration démontrées, limites et rétention documentées. | CI sauvegarde et restaure les huit bases et le volume RabbitMQ dans des destinations isolées sur un runner jetable. Le test RabbitMQ démarre le broker à partir de l'archive ; les sauvegardes restent privées et ne sont pas publiées. |
| Qualité | Compilation Release, tests backend, tests Angular, E2E, audit dépendances, build images et démarrage sain de l’infrastructure passent. | CI verte pour le SHA exact qui sera livré, rapports consultables, aucun avis bloquant sans décision documentée. |
| Exploitation | Health checks, logs corrélés, procédures de panne et versions des modèles locaux connues. | Smoke test CI après migrations et readiness des APIs/Gateway, validation complète sur le poste, guide [dépannage](troubleshooting.md), inventaire et sauvegarde des volumes. |
| Contrôle humain | Contenu généré et dossiers sont revus explicitement ; aucune action externe n'est déclenchée silencieusement. | Scénarios de revue et d'approbation, vérification qu'aucun hôte externe n'est contacté dans les tests. |

## Blocages actuels identifiés

- Le Gateway prend en charge plusieurs certificats OpenIddict persistants par usage hors `Development`, publie les clés de signature configurées dans le JWKS, lit les mots de passe PFX depuis des fichiers secrets et échoue sans base Identity ou certificats valides. Le chevauchement est couvert par les tests ; la [phase 97.25](phase-97-25.md) ajoute un générateur local auto-signé pour les essais et un contrôle de génération/readback en CI Windows. Le gestionnaire de secrets de production, un renouvellement sur déploiement réel et le retrait d'une ancienne clé restent à valider. Les phases [97.26](phase-97-26.md) et [97.27](phase-97-27.md) sauvegardent et restaurent les huit bases et RabbitMQ sur un hôte jetable, tout en conservant les anciens volumes et bases. La récupération séparée des secrets et certificats, un run CI vert sur le commit livré et l'exercice complet sur l'hôte local restent à valider. Compose utilise l'environnement `Development` et des clés éphémères.
- Les Bearer statiques de compatibilité Profile, Agents, Applications, Documents et Collection sont refusés hors `Development` et `Testing` ; configurer et vérifier l’émetteur et les certificats OpenIddict avant tout déploiement dans un autre environnement.
- Collection exige une identité OpenIddict pour les lectures et le Gateway lui transmet le jeton courant. Ses imports, collectes, revues et publications restent des opérations d'administration limitées aux propriétaires de `Collection:Admin:OwnerIds`; vérifier cette configuration avant d'accorder des droits d'administration.
- Les APIs privées filtrent les données candidat avec le claim propriétaire du jeton validé, et les suites d'intégration couvrent les accès croisés sur les données de Profile, Documents, Applications et Agents. Matching limite les résultats au profil résolu pour le compte. Les identités statiques restent disponibles uniquement en développement et tests. La [phase 97.30](phase-97-30.md) exerce l'isolation Profile de deux comptes inscrits dans le Gateway et connectés à la pile réelle ; l'exécution de CI sur le SHA livré reste à obtenir. L'inventaire de la [phase 97.29](phase-97-29.md) doit encore être exécuté sur les bases locales et revu avant toute attribution historique.
- La CI construit les images, applique les huit migrations sur un runner jetable, démarre toute la pile, vérifie les endpoints de readiness et crée puis reconnecte un compte synthétique via Chromium. Un run vert sur le commit exact reste à obtenir ; la validation locale et la configuration de protection de branches GitHub restent à faire.
- La soumission externe reste désactivée. La V1 locale doit garder la préparation, l'approbation et la saisie du résultat explicitement contrôlées par l'utilisateur.

Ces points interdisent de considérer cette checklist comme une certification de déploiement Internet. Les critères additionnels pour un déploiement réseau sont dans le [guide de sécurité](security.md).

## Résultat de validation locale — 18 octobre 2026

| Contrôle | Résultat |
| --- | --- |
| Restauration .NET avec NU1902/NU1903/NU1904 en erreurs (solution et projets de test) | Réussie. Le Gateway épingle la dépendance XML corrigée en 10.0.13. |
| Build .NET Release | Réussi, 0 avertissement et 0 erreur. |
| Tests unitaires backend | 1 048 réussis sur 8 projets. |
| Tests d'intégration backend | 610 réussis sur 7 projets ; 2 tests live Ollama ignorés par conception. PostgreSQL a été fourni par Testcontainers. |
| Audit npm en ligne | 0 vulnérabilité. |
| Tests unitaires Angular | 68 réussis sur 13 fichiers. |
| Build Angular | Réussi ; plusieurs budgets SCSS dépassent le seuil d'avertissement configuré. |
| Tests Chromium | 19 réussis, y compris les deux scénarios de soumission contrôlée sans envoi externe. |
| Configuration Compose complète | Valide avec `.env.example`. |
| Build des images Compose | Non conclu : l'accès aux images de base a été très lent et le build local a été interrompu avant la fin. |

Cette validation prouve les contrôles locaux listés, pas une CI verte sur un SHA livré. Les qualifications Ollama restent désactivées ; les images Docker doivent être construites par un runner CI. Les blocages de sécurité et les réglages de protection de branche ci-dessus restent ouverts.

## Commandes de validation candidate

Exécuter les commandes avec Docker Desktop démarré et depuis les chemins du dépôt. Les tests d'intégration peuvent nécessiter l'accès à des images Docker et packages NuGet/npm :

```powershell
dotnet restore JobApplicationAgent.slnx
dotnet build JobApplicationAgent.slnx --configuration Release --no-restore
Get-ChildItem tests -Filter *.csproj -Recurse | ForEach-Object {
    dotnet test $_.FullName --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Échec : $($_.FullName)" }
}

Push-Location src/frontend/job-application-agent-ui
npm ci
npm audit --audit-level=high
npm test -- --watch=false
npx playwright install chromium
npm run test:e2e
npm run build
Pop-Location
```

Valider ensuite la configuration Compose complète avec `.env.example` comme décrit dans [testing.md](testing.md), puis suivre le parcours local dans [windows-install.md](windows-install.md). Appliquer les migrations sur des bases de développement/test seulement après inspection des scripts EF ; la validation ne doit jamais appeler une soumission externe réelle.
