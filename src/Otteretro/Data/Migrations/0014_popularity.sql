-- F2.17 (#89): popularidad de cada juego en IGDB (total_rating_count), para ordenar por «más reconocidas» sin Claude.
-- NULL = todavía no se pidió; la tarea igdb.popularity la completa (la encola esta migración si hay juegos).
ALTER TABLE catalog.game ADD COLUMN popularity INTEGER;
INSERT INTO task (kind, status, created_at)
SELECT 'igdb.popularity', 'queued', strftime('%Y-%m-%dT%H:%M:%fZ', 'now') WHERE EXISTS (SELECT 1 FROM game WHERE igdb_id IS NOT NULL);
