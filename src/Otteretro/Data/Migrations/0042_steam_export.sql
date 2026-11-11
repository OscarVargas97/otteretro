-- #401 (§10): lo que se lleva a Steam. steam_mark: las versiones marcadas (valen en todos los equipos). steam_export: lo que se
-- exportó la última vez en cada equipo, para las diferencias y para jugar por `steam://rungameid/…` (#130).
-- version_id: del catálogo, sin clave foránea. app_id: el id corto (32 bits) del acceso no-Steam, calculado como SRM.
CREATE TABLE steam_mark (
    version_id INTEGER PRIMARY KEY
);
CREATE TABLE steam_export (
    machine_id  TEXT NOT NULL REFERENCES machine(id),
    version_id  INTEGER NOT NULL,
    title       TEXT NOT NULL,
    category    TEXT NOT NULL,
    app_id      INTEGER NOT NULL,
    exported_at TEXT NOT NULL,
    PRIMARY KEY (machine_id, version_id)
);
