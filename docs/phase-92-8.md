# Phase 92.8 — traces distribuées RabbitMQ

Les relays Job et Collection ouvrent une activité producteur autour de la publication RabbitMQ. Ils injectent `traceparent` et, si présent, `tracestate` dans les propriétés AMQP, à côté de l’identifiant de corrélation métier. Le consommateur Matching extrait ce contexte et ouvre une activité de traitement liée à la trace productrice.

Les activités utilisent `JobApplicationAgent.EventTransport` et les conventions W3C de .NET. Elles seront exportées par un SDK compatible lorsqu’un exporter sera configuré. Aucun identifiant d’événement ou d’utilisateur n’est ajouté aux tags.

Vérification : compilation des APIs Job, Collection et Matching et de leurs projets de tests ; tests compilés, non exécutés.
