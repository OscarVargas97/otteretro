-- #286 y #287 (decisiones 296 y 299): los ports nativos de PC de cada juego, que publica el catálogo (otteretro-catalogo#69, sus
-- migraciones 0028 y 0029). status: complete (100 % jugable), playable (con fallos), beta, experimental o no-port (sin port todavía).
-- delisted: su fuente ya no lo lista. Sin CHECK en los valores: un catálogo más nuevo con otro estado no deja de aplicarse.
CREATE TABLE catalog.port (
    id INTEGER PRIMARY KEY,
    console TEXT NOT NULL,
    game TEXT NOT NULL,
    project TEXT NOT NULL,
    kind TEXT NOT NULL,
    toolchain TEXT,
    status TEXT NOT NULL,
    abandoned INTEGER NOT NULL DEFAULT 0,
    delisted INTEGER NOT NULL DEFAULT 0,
    link TEXT NOT NULL,
    source TEXT NOT NULL,
    added TEXT NOT NULL,
    igdb_id INTEGER,
    emulator TEXT,
    emulator_level INTEGER,
    game_id INTEGER,
    manual INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX catalog.port_game ON port (game_id);
