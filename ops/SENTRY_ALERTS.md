# Configuration des alertes Sentry

Les règles d'alerte sont créées dans les projets Sentry API et Flutter. Les
DSN, tokens et webhooks ne doivent jamais être copiés dans ce dépôt.

## Règles minimales

1. **Nouvelle erreur critique** : nouvelle issue en production, niveau `error`
   ou `fatal`, notification immédiate.
2. **Régression** : issue marquée résolue qui réapparaît dans une nouvelle
   release, notification immédiate.
3. **Hausse du taux d'erreur** : taux d'échec supérieur à 5 % sur cinq minutes,
   notification immédiate ; avertissement à 2 %.

## Preuve de bon fonctionnement

- Vérifier que l'événement contient `environment=production` et une release de
  la forme `tablemaster-api@1.0.2+<run>` ou
  `tablemaster-mobile@1.0.2+<run>`.
- Déclencher l'événement de test uniquement avec les mécanismes Development
  existants.
- Conserver une capture anonymisée de l'événement, de la règle et de la
  notification reçue.
- Résoudre l'issue de test après la capture afin de pouvoir démontrer une
  régression contrôlée si nécessaire.
