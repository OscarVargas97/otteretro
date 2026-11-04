-- #214: generar el catálogo pasó a otteretro-catalogo. Lo que quedó esperando en la cola ya no tiene quién lo haga.
UPDATE task SET status = 'canceled', finished_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
WHERE status IN ('queued', 'running', 'paused')
  AND (kind LIKE 'igdb.%' OR kind IN ('catalog.extra', 'claude.cleanup', 'claude.sagas', 'claude.load'));
