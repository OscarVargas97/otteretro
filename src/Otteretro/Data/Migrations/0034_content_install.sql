-- #280 (decisión 295): lo que se instaló con el instalador de un emulador (Azahar --install, RPCS3 --installpkg), para no repetirlo
-- al volver a preparar el juego. Es del emulador y no de una versión: el contenido vive en su carpeta común (decisión 308). Lo que se
-- apunta sin copiar (Eden, Ryubing) no se anota: se vuelve a escribir cada vez y no se repite.
CREATE TABLE content_install (
    emulator_id  TEXT NOT NULL REFERENCES emulator(id),
    source_id    INTEGER NOT NULL REFERENCES source(id) ON DELETE CASCADE,
    installed_at TEXT NOT NULL,
    PRIMARY KEY (emulator_id, source_id)
);
