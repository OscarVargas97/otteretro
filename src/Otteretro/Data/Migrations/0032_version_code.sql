-- #249 (decisión 278): el TitleID o el serial de cada versión, que publica el catálogo (otteretro-catalogo#46). Otteretro reconoce
-- primero por ellos; las tablas de #240 y #241 quedan de respaldo.
CREATE TABLE catalog.version_code (
    version_id INTEGER NOT NULL,
    code TEXT NOT NULL,
    PRIMARY KEY (version_id, code)
);
CREATE INDEX catalog.version_code_code ON version_code (code);
