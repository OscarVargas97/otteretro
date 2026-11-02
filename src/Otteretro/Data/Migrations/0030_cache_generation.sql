-- #225: Sagas se arma una vez y queda guardado (`cache/sagas.json`). Este número dice si lo guardado sigue al día: sube solo cuando cambia
-- algo tuyo que Sagas muestra (archivos y tiempos, favoritas y seguidas, consolas ocultas). Aplicar una versión del catálogo también lo sube.
CREATE TABLE cache_generation (value INTEGER NOT NULL);
INSERT INTO cache_generation (value) VALUES (0);

CREATE TRIGGER source_cache_insert AFTER INSERT ON source BEGIN UPDATE cache_generation SET value = value + 1; END;
CREATE TRIGGER source_cache_delete AFTER DELETE ON source BEGIN UPDATE cache_generation SET value = value + 1; END;
CREATE TRIGGER source_cache_update AFTER UPDATE OF version_id, playtime, last_played ON source
WHEN OLD.version_id IS NOT NEW.version_id OR OLD.playtime IS NOT NEW.playtime OR OLD.last_played IS NOT NEW.last_played
BEGIN UPDATE cache_generation SET value = value + 1; END;

CREATE TRIGGER saga_user_cache_insert AFTER INSERT ON saga_user BEGIN UPDATE cache_generation SET value = value + 1; END;
CREATE TRIGGER saga_user_cache_update AFTER UPDATE ON saga_user BEGIN UPDATE cache_generation SET value = value + 1; END;
CREATE TRIGGER saga_user_cache_delete AFTER DELETE ON saga_user BEGIN UPDATE cache_generation SET value = value + 1; END;

CREATE TRIGGER console_hidden_cache_insert AFTER INSERT ON console_hidden BEGIN UPDATE cache_generation SET value = value + 1; END;
CREATE TRIGGER console_hidden_cache_delete AFTER DELETE ON console_hidden BEGIN UPDATE cache_generation SET value = value + 1; END;
