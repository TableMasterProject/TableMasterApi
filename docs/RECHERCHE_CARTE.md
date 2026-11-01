# Recherche de restaurants sur la carte

`GET /api/Restaurant` accepte quatre paramètres de requête optionnels :
`SouthLatitude`, `NorthLatitude`, `WestLongitude` et `EastLongitude`. Les quatre
doivent être fournis ensemble. Les valeurs doivent être des coordonnées valides ;
une zone traversant le méridien 180° est prise en charge.

`Latitude` et `Longitude` restent le centre utilisé pour trier les résultats.
`Offset` et `PageSize` conservent la pagination existante. Sans bornes, la
recherche garde son comportement précédent. La réponse JSON et l'obligation
d'authentification ne changent pas.
