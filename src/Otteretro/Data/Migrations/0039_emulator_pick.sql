-- #373 (#369): el emulador recomendado para cada juego y consola, solo local: lo que encontró «Buscar el mejor emulador» (Claude)
-- o lo que eligió la persona a mano. Uno por juego y consola; una búsqueda nueva reemplaza lo de Claude y no toca lo manual.
-- recipe_id: la receta (sin clave foránea: es del catálogo); sin receta, repo dice cuál es. game_id: del catálogo, sin clave foránea.
CREATE TABLE emulator_pick (
    game_id     INTEGER NOT NULL,
    console     TEXT NOT NULL,
    recipe_id   TEXT,
    repo        TEXT,
    source      TEXT,
    reason      TEXT,
    origin      TEXT NOT NULL CHECK (origin IN ('claude', 'manual')),
    searched_at TEXT NOT NULL,
    PRIMARY KEY (game_id, console)
);
