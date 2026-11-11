-- #398 (§8.3): cómo se juega cada versión del juego, elegido a mano; manda sobre la regla automática. «Volver a la automática»
-- borra la fila. option: pc:<versión de PC>, port:<id del port> o emulator:<id del emulador>. version_id: del catálogo, sin clave foránea.
CREATE TABLE play_choice (
    version_id INTEGER PRIMARY KEY,
    option     TEXT NOT NULL,
    chosen_at  TEXT NOT NULL
);
