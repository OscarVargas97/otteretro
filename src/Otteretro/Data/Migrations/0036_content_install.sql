-- Numerada 0036: 0034 y 0035 quedan para las ramas de ports (#286) y Herramientas (#296), que salieron con 0034.
-- #280 (decisión 295): lo que se instaló con el instalador de un emulador (Azahar --install, RPCS3 --installpkg), para no repetirlo
-- al volver a preparar el juego. Es del emulador y no de una versión: el contenido vive en su carpeta común (decisión 308). Lo que se
-- apunta sin copiar (Eden, Ryubing) no se anota: se vuelve a escribir cada vez y no se repite.
CREATE TABLE content_install (
    emulator_id  TEXT NOT NULL REFERENCES emulator(id),
    source_id    INTEGER NOT NULL REFERENCES source(id) ON DELETE CASCADE,
    installed_at TEXT NOT NULL,
    PRIMARY KEY (emulator_id, source_id)
);

-- La licencia (zRIF) de un juego de Vita, que Vita3K pide para instalar su .pkg (`--pkg --zrif`). La escribe la persona en la ficha del
-- juego. Va solo en esta base privada: nunca en el catálogo ni en un perfil compartido. version_id: la versión del catálogo (sin clave
-- foránea: el catálogo es otra base).
CREATE TABLE version_license (
    version_id INTEGER PRIMARY KEY,
    zrif       TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
