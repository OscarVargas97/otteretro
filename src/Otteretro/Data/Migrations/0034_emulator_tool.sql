-- #296 (decisión 311): una herramienta (Steam ROM Manager) se instala y se guarda en la librería como un emulador, en
-- `Herramientas\<Herramienta>\<versión>`: `emulator.kind` suma 'tool'. SQLite no cambia un CHECK: se rehace la tabla.
-- Las demás tablas la nombran como `emulator`, así que siguen apuntando a la nueva. Las restricciones se miran al final
-- (defer_foreign_keys) y console_emulator, que se borraría en cascada al quitar la vieja, se guarda y se repone.
PRAGMA defer_foreign_keys = ON;

CREATE TEMP TABLE emulator_old AS SELECT * FROM emulator;
CREATE TEMP TABLE console_emulator_old AS SELECT * FROM console_emulator;

DROP TABLE emulator;
CREATE TABLE emulator (
    id   TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('emulator', 'port', 'tool')),
    name TEXT NOT NULL
);
INSERT INTO emulator SELECT id, kind, name FROM temp.emulator_old;
INSERT OR IGNORE INTO console_emulator SELECT console_id, emulator_id, is_base FROM temp.console_emulator_old;

DROP TABLE temp.emulator_old;
DROP TABLE temp.console_emulator_old;
