-- #217: un archivo cuya versión no está en el catálogo actual (por ejemplo, el de un fork) guarda aquí a qué versión apuntaba,
-- para volver a identificarse solo cuando un catálogo la tenga de nuevo.
ALTER TABLE source ADD COLUMN lost_version_id INTEGER;
