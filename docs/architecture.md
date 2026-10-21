# Architecture locale

JobApplicationAgent est une application locale composée d'une interface Angular, d'un Gateway BFF/OIDC et de microservices métier. Chaque service persistant garde sa propre base PostgreSQL. Le Gateway est l'unique entrée du navigateur en mode Compose complet.

```mermaid
flowchart LR
    Browser[Navigateur] --> UI[Angular / Nginx :4200]
    UI -->|/api et /auth| GW[Gateway YARP + OpenIddict :5000]
    GW --> P[Profile API]
    GW --> J[Job API]
    GW --> M[Matching API]
    GW --> A[Applications API]
    GW --> D[Documents API]
    GW --> C[Collection API]
    GW --> G[Agents API]
    GW --> ID[(Identity PostgreSQL)]
    P --> PDB[(Profile PostgreSQL)]
    J --> JDB[(Job PostgreSQL)]
    M --> MDB[(Matching PostgreSQL)]
    A --> ADB[(Applications PostgreSQL)]
    D --> DDB[(Documents PostgreSQL)]
    C --> CDB[(Collection PostgreSQL)]
    G --> GDB[(Agents PostgreSQL)]
    C -->|publication approuvée| J
    J -->|job.published.v1, outbox| MQ[RabbitMQ]
    MQ -->|inbox idempotente| M
    G -->|sources minimisées| P
    G --> J
    G --> M
    G --> A
```

Le navigateur conserve une session cookie HttpOnly gérée par le Gateway. Les API valident les jetons d'accès OIDC et appliquent leur propre contrôle de propriétaire. Les workers RabbitMQ et les traitements Agents restent soumis à leur configuration et sont désactivés par défaut lorsque le déploiement ne les active pas. En développement Compose, les clés OIDC sont éphémères : un redémarrage du Gateway invalide les jetons existants, et l'utilisateur doit se reconnecter.

Le démarrage Compose complet publie uniquement des ports liés à `127.0.0.1`. La topologie et ses variables sont définies dans les fichiers `docker-compose*.yml`; le guide [installation Windows](windows-install.md) décrit leur démarrage.
