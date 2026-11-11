-- #374: cuándo se buscó por última vez el mejor emulador de cada juego, aunque Claude no haya recomendado nada.
-- El selector muestra esa fecha y deja desmarcados los juegos que ya tienen una búsqueda. game_id: del catálogo, sin clave foránea.
CREATE TABLE emulator_search (
    game_id     INTEGER PRIMARY KEY,
    searched_at TEXT NOT NULL
);
