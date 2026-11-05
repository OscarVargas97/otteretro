-- #267 (§8.0, decisiones 286 y 290): emuladores compartidos, sus instalaciones por versión, el base de cada consola, los perfiles
-- por juego y las ROMs modificadas. Dónde está cada instalación no se guarda: se encuentra en `Emuladores\` de los discos por
-- nombre y versión (`Recipes.Installed`), así que mover un disco o una instalación no deja rutas viejas.

-- Un emulador o port, instalado pocas veces y compartido entre consolas. id: el de su receta, o uno propio si no tiene.
-- name: su carpeta, `Emuladores\<name>\<versión>` (o `Ports\…`), una sola aunque sirva a varias consolas (decisión 302).
CREATE TABLE emulator (
    id   TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('emulator', 'port')),
    name TEXT NOT NULL
);

-- Cada versión instalada (§8.4: las versiones conviven, nunca se reemplaza una). origin: recipe (su receta), url (una release ya
-- compilada), upload (una carpeta o un zip subido) o adopted (instalado a mano y copiado a `Emuladores\`). file y sha256: el archivo
-- original que guarda la librería (decisión 293). backup_library_id: el disco de la segunda copia; null mientras no la hay.
CREATE TABLE emulator_install (
    emulator_id       TEXT NOT NULL REFERENCES emulator(id),
    version           TEXT NOT NULL,
    origin            TEXT NOT NULL CHECK (origin IN ('recipe', 'url', 'upload', 'adopted')),
    url               TEXT,
    file              TEXT,
    sha256            TEXT,
    backup_library_id TEXT,
    installed_at      TEXT NOT NULL,
    PRIMARY KEY (emulator_id, version)
);

-- Qué emuladores sirven a cada consola (muchos a muchos), con uno base por consola (decisión 291).
CREATE TABLE console_emulator (
    console_id  TEXT NOT NULL,
    emulator_id TEXT NOT NULL REFERENCES emulator(id) ON DELETE CASCADE,
    is_base     INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (console_id, emulator_id)
);
CREATE UNIQUE INDEX console_emulator_base ON console_emulator (console_id) WHERE is_base = 1;

-- Un perfil: la configuración (settings, JSON) de un juego en un emulador, en una versión fija o, sin emulator_version, la más nueva
-- instalada. Sin game_version_id es el perfil por defecto del emulador (uno por emulador), el que usan los juegos sin perfil propio.
-- game_version_id: la versión del catálogo (el juego en su consola). source_id: la ROM que usa; sin ella, la base. Un solo perfil
-- activo por juego. AUTOINCREMENT: los archivos del perfil (mods) se guardan con su id y un id borrado no se reusa.
CREATE TABLE profile (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    emulator_id      TEXT NOT NULL REFERENCES emulator(id),
    emulator_version TEXT,
    game_version_id  INTEGER,
    name             TEXT NOT NULL,
    settings         TEXT NOT NULL DEFAULT '{}',
    source_id        INTEGER REFERENCES source(id) ON DELETE SET NULL,
    active           INTEGER NOT NULL DEFAULT 0,
    created_at       TEXT NOT NULL,
    CHECK (game_version_id IS NOT NULL OR active = 0)
);
CREATE UNIQUE INDEX profile_default ON profile (emulator_id) WHERE game_version_id IS NULL;
CREATE UNIQUE INDEX profile_active ON profile (game_version_id) WHERE active = 1;
CREATE INDEX profile_game ON profile (game_version_id);

-- Varias ROMs por juego (decisión 290): la base y sus modificaciones (traducciones, hacks), con su nombre.
ALTER TABLE source ADD COLUMN modified INTEGER NOT NULL DEFAULT 0;
ALTER TABLE source ADD COLUMN label TEXT;
