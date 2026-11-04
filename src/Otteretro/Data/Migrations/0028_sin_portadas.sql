-- #221 (decisión 258): Otteretro ya no busca ni baja portadas; las imágenes salen de lo que publica el catálogo.
-- Lo que quedó en la cola ya no tiene quién lo haga, y la clave de SteamGridDB ya no se usa.
UPDATE task SET status = 'canceled', finished_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
WHERE status IN ('queued', 'running', 'paused') AND kind IN ('sgdb.images', 'sgdb.webp', 'catalog.update');
DELETE FROM credential WHERE id = 'steamgriddb.apiKey';
