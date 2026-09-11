# Sécurité des sessions, réservations et notifications

Ce document décrit les contrats utilisés conjointement par l'API 1.0 et l'application Flutter.

## Erreurs HTTP

Les réponses d'erreur JSON ont la forme suivante :

```json
{
  "error": "Message exploitable par l'utilisateur",
  "traceId": "identifiant de diagnostic"
}
```

Les anciens messages texte restent lisibles par l'application pendant la transition. `401` indique une authentification absente ou invalide, `403` un droit insuffisant, `404` une ressource inexistante et `409` un conflit métier ou concurrent. Les exceptions inattendues sont journalisées côté serveur ; la réponse `500` reste générique et son `traceId` permet la recherche dans les journaux.

## Sessions

- Le JWT dure cinq minutes. Avec la tolérance de validation d'une minute, un JWT déjà émis cesse d'être accepté en six minutes au maximum.
- Chaque refresh consomme le token présenté et crée son remplaçant dans une même transaction PostgreSQL.
- La rotation, la déconnexion, le changement et la réinitialisation de mot de passe verrouillent le même utilisateur. Un refresh en concurrence ne peut donc pas recréer une session révoquée.
- Un changement ou une réinitialisation de mot de passe supprime tous les refresh tokens et tous les liens de réinitialisation de l'utilisateur.
- Les hashes de refresh token sont uniques. La migration `009_AtomicSessions.sql` révoque tous les exemplaires d'un hash dupliqué avant de créer l'index unique.

## SignalR

Le hub `/reservationHub` exige un JWT. Sur navigateur, le client SignalR transmet ce JWT avec le paramètre `access_token`, car l'API WebSocket du navigateur ne permet pas de définir librement l'en-tête `Authorization`. L'API ne lit ce paramètre que pour ce chemin. La connexion est fermée à l'expiration du JWT et le client se reconnecte avec un token renouvelé.

Les groupes sont :

| Groupe | Accès | Contenu |
| --- | --- | --- |
| `user_{id}` | uniquement l'utilisateur correspondant | détails de ses réservations |
| `restaurant_{id}` | uniquement le propriétaire | détails des réservations du restaurant |
| `availability_{id}` | tout utilisateur authentifié, pour un restaurant existant | `{ "restaurantId": id }` uniquement |

Le groupe de disponibilité est invalidé après une mutation de réservation, de table, de salle, d'horaires, de fermeture ou de restaurant. Le client recharge les données après reconnexion et au retour au premier plan.

La production doit exposer l'API en HTTPS ; SignalR utilise alors WSS. Le paramètre `access_token` est filtré avant l'envoi d'un événement à Sentry. Le reverse proxy doit aussi omettre la query string de ses journaux d'accès sur `/reservationHub` ou masquer ce paramètre.

## Disponibilités et dates

`GET /api/Reservation/availability` attend `restaurantId`, `minDate` et `maxDate`, avec la même date pour les deux bornes. La réponse n'est pas paginée ; les paramètres historiques `Offset` et `PageSize` sont ignorés par le binding. La recherche inclut les réservations des jours voisins qui entrent dans la marge de conflit stricte de 90 minutes.

Les dates de réservation sont stockées sans décalage comme heures locales `Europe/Paris`. Une entrée portant un décalage est convertie vers Paris avant stockage. Une heure locale ambiguë ou inexistante lors d'un changement d'heure est refusée.

Une réservation client doit :

- être future avec plus de 30 minutes de préavis ;
- commencer sur un pas de 30 minutes calculé depuis le début d'une plage d'ouverture ;
- commencer strictement avant la fin de cette plage ;
- respecter fermeture, capacité, restaurant de la table et absence de conflit.

Une réservation rapide du propriétaire peut déroger aux horaires et au préavis. Elle respecte encore la date future, les fermetures, la capacité, le restaurant de la table et les conflits.

## Notifications différées

Chaque mutation validée écrit ses tâches dans `ReservationNotificationOutbox` dans la transaction de la réservation. L'API répond après le commit sans attendre Firebase ou Brevo. Le worker réserve une tâche avec un bail récupérable et appelle le service externe hors transaction SQL.

Il effectue six tentatives au total, puis classe la tâche en `dead`. Les reprises ont lieu après 1, 5, 15, 30 et 60 minutes. Les succès push sont mémorisés par destinataire ; un token FCM définitivement invalide est supprimé. La livraison est au moins une fois : `EventId` permet à Flutter de remplacer ou ignorer une notification déjà affichée.

Pour remettre manuellement une tâche `dead` en attente, utiliser `scripts/replay-reservation-notification.sh UUID` avec un `PGSERVICE` local et un fichier d'identifiants libpq protégé.

Requêtes de supervision utiles :

```sql
SELECT "State", "Channel", count(*)
FROM "ReservationNotificationOutbox"
GROUP BY "State", "Channel";

SELECT "Id", "Channel", "Attempts", "DueAt", "LastError"
FROM "ReservationNotificationOutbox"
WHERE "State" IN ('pending', 'leased', 'dead')
ORDER BY "DueAt";
```

Les métriques du worker sont publiées par le meter `TableMaster.ReservationNotifications` sous le nom `reservation_notification_outcomes`. Les journaux HTTP permettent de suivre les refus de refresh, négociation SignalR et conflits `409` sans enregistrer les tokens.

## Déploiement et retour arrière

Construire et valider d'abord la version Flutter qui connaît `availability_{id}`. Appliquer ensuite les migrations additives `009`, `010` et `011`, déployer l'API sécurisée, puis publier la version Flutter coordonnée. Les anciennes versions gardent les appels HTTP, mais leur temps réel public de disponibilité n'est pas disponible.

Le retour arrière applicatif conserve les migrations additives et la table d'outbox. Il ne doit jamais réouvrir le hub anonyme. Désactiver temporairement le worker avec `Features__RunNotificationWorker=false` permet d'accumuler les tâches sans perdre les notifications pendant une intervention.
