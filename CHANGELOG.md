# Journal des versions de TableMaster API

Toutes les évolutions déployées de l'API sont consignées ici. Chaque release doit
indiquer les incidents corrigés, les migrations éventuelles, le SHA de l'image,
les tests exécutés et la procédure de retour arrière.

## [1.0.2] - Non déployée

### Ajouté

- Réponses JSON versionnées pour les sondes `/health` et `/ready`.
- Supervision externe de la production avec incident GitHub et notification Discord.
- Scripts versionnés de sauvegarde, restauration de contrôle, smoke test et rollback.
- Formulaire structuré de consignation des anomalies et suivi Dependabot.

### Modifié

- Séparation de la liveness de l'API et de la readiness PostgreSQL.
- Déploiement de l'API par SHA d'image immuable.
- Association des événements Sentry à la release déployée.
- Mise à jour ciblée de Sentry ASP.NET Core de 6.6.0 à 6.8.0, sans changement de contrat API.

### Corrigé

- Healthcheck Docker rendu déterministe avec `curl` installé dans l'image runtime.

### Validation attendue

- Tests xUnit, build Docker, `/health`, `/ready`, smoke test et contrôle post-déploiement.
- Rollback automatique vers le SHA précédent si la readiness échoue.

## [1.0.1] - 2026-07-14

### Ajouté

- Sentry ASP.NET Core, contrôles de santé API/PostgreSQL et déploiement Docker automatisé.
- Tests automatisés de l'API et image GHCR taguée par commit.

[1.0.2]: https://github.com/TableMasterProject/TableMasterApi/compare/13bd46489521b04956e98fef3803bb71e6346631...HEAD
[1.0.1]: https://github.com/TableMasterProject/TableMasterApi/commit/13bd46489521b04956e98fef3803bb71e6346631
