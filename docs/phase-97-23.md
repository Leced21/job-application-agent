# Phase 97.23 — Compatibilité Windows PowerShell pour la migration Identity

Le script de migration Identity ne dépend que de fonctionnalités disponibles dans Windows PowerShell 5.1. Son prérequis est abaissé à cette version pour permettre la première migration depuis le shell intégré à Windows, sans installer PowerShell 7. Il lit les labels Docker sous forme de chaîne pour éviter la perte de guillemets lors de l'appel natif depuis Windows PowerShell 5.1. Les autres scripts locaux conservent leurs prérequis individuels.

Un job Windows dans la CI analyse `tools/Update-LocalIdentityDatabase.ps1` avec Windows PowerShell et vérifie le prérequis déclaré. Ce contrôle protège la compatibilité syntaxique ; l'application de la migration à la base du poste reste une opération locale avec Docker et le SDK .NET.

## Utilisation

Depuis Windows PowerShell 5.1 ou PowerShell 7, à la racine du dépôt :

```powershell
.\tools\Update-LocalIdentityDatabase.ps1 -ComposeProjectName job-application-agent
```

La migration est réussie lorsque le script affiche qu'elle a été appliquée au projet Compose indiqué. En cas d'erreur, ne poursuivre pas en se fiant à `$LASTEXITCODE` d'une commande Docker précédente ; corriger d'abord l'erreur affichée par le script.

## Vérification

Le script passe l'analyse syntaxique de Windows PowerShell 5.1 et la lecture des labels utilise un format Docker sans argument de template cité. La configuration Compose Identity et `git diff --check` passent également. L'exécution contre la base locale reste dépendante de la disponibilité du moteur Docker et du SDK .NET.
