# Exploitation et maintien en condition opérationnelle

Ce dossier contient les procédures exécutées par la CI et utilisables manuellement
sur le VPS. Elles ne doivent jamais afficher le contenu de `.env`.

## Déploiement API

Le workflow fournit obligatoirement `API_IMAGE_TAG`, `APP_VERSION` et
`SENTRY_RELEASE` à `deploy-api.sh`. Le script :

1. identifie l'image actuellement déployée ;
2. sauvegarde PostgreSQL ;
3. écrit uniquement les métadonnées non sensibles dans `.deployment.env` ;
4. déploie l'image correspondant au SHA ;
5. attend `/ready` et exécute le smoke test ;
6. restaure l'image précédente si la validation échoue.

Un rollback applicatif ne permet pas d'annuler une migration destructive. Toute
migration de ce type exige une sauvegarde vérifiée et une stratégie spécifique.

## Sauvegarde et restauration de contrôle

```bash
./ops/backup-postgres.sh
./ops/restore-postgres-test.sh /opt/tablemaster/backups/postgres/tablemaster-<date>.dump
```

Les sauvegardes sont privées, conservées 14 jours par défaut et accompagnées
d'une somme SHA-256. Le test crée une base temporaire, restaure le dump, vérifie
la présence des tables puis supprime cette base, sans toucher à la production.

## Supervision externe

Le workflow `production-monitoring.yml` vérifie le web, `/health`, `/ready` et
le certificat TLS. Trois tentatives sont réalisées avant l'ouverture d'un
incident. Une seule issue portant le label `monitoring` reste ouverte et la
reprise du service la ferme.

Secret GitHub à créer : `DISCORD_MONITORING_WEBHOOK`. Le webhook ne doit jamais
être ajouté à un fichier, une issue ou une capture.

Le déclenchement manuel avec `simulate_failure=true` permet de produire les
preuves d'une alerte et d'un rétablissement sans arrêter la production.

## Preuves à conserver

- run de tests et audit de dépendances ;
- image et release associées au SHA ;
- réponse JSON de `/health` et `/ready` ;
- issue automatique et messages Discord DOWN/UP ;
- événement Sentry avec environnement et release ;
- résultat du smoke test, du rollback contrôlé et de la restauration.

## nginx-api.reference.conf

Configuration Nginx de référence pour l'API, notamment le bloc
`location /reservationHub`. La configuration en service sur le VPS n'est pas
versionnée : ce fichier sert de point de comparaison.

Le hub SignalR exige `proxy_http_version 1.1`, les en-têtes `Upgrade` /
`Connection`, et un `proxy_read_timeout` supérieur au `KeepAliveInterval` du
serveur. Sans l'upgrade WebSocket le temps réel ne fonctionne pas : le client
Flutter utilise `skipNegotiation: true`, donc aucun repli n'est possible.

Vérifier la conformité du proxy en cas d'incident sur le temps réel :

```bash
# Doit répondre 101 Switching Protocols
curl -i -N \
  -H "Connection: Upgrade" -H "Upgrade: websocket" \
  -H "Sec-WebSocket-Version: 13" -H "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==" \
  https://api.tablemaster.lmpe.ovh/reservationHub
```
