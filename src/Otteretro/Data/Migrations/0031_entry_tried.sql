-- #237: los paquetes de `Entrada\` que ya se descomprimieron sin encontrar juegos. Con el mismo tamaño no se vuelven a descomprimir
-- solos (cada intento eran minutos de disco); «Importar carpeta» los intenta igual.
CREATE TABLE entry_tried (
    library_id    TEXT NOT NULL,
    relative_path TEXT NOT NULL,
    size          INTEGER NOT NULL,
    PRIMARY KEY (library_id, relative_path)
);
