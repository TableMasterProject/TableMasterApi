# Journal des versions de TableMaster API

Toutes les évolutions déployées de l'API sont consignées ici. Chaque release doit
indiquer les incidents corrigés, les migrations éventuelles, le SHA de l'image,
les tests exécutés et la procédure de retour arrière.

## [1.0.2+41] - 2026-08-09

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

### Validation

- 184 tests xUnit, audit NuGet, build et publication de l'image Docker réussis.
- Sauvegarde PostgreSQL créée avant déploiement, `/health` et `/ready` sains.
- Smoke test de production réussi sur le commit `7ff401e64f80`.
- Incident de supervision #30 fermé automatiquement et notification Discord de rétablissement envoyée.
- Mécanisme de rollback automatique disponible vers le SHA précédent si la readiness échoue.

## [1.0.1] - 2026-07-14

### Ajouté

- Sentry ASP.NET Core, contrôles de santé API/PostgreSQL et déploiement Docker automatisé.
- Tests automatisés de l'API et image GHCR taguée par commit.

[1.0.2+41]: https://github.com/TableMasterProject/TableMasterApi/releases/tag/api-v1.0.2%2B41
[1.0.1]: https://github.com/TableMasterProject/TableMasterApi/commit/13bd46489521b04956e98fef3803bb71e6346631
