-- #357 (decisión 349): las recetas de emuladores que publica el catálogo (otteretro-catalogo#94). `json` es la receta entera, con el formato
-- de `recetas/*.json`. Un catálogo sin estas tablas deja la tabla vacía y la app usa las recetas que trae incrustadas.
CREATE TABLE catalog.recipe (
    id TEXT PRIMARY KEY,
    kind TEXT NOT NULL,
    json TEXT NOT NULL
);
CREATE TABLE catalog.recipe_console (
    recipe_id TEXT NOT NULL,
    console_id TEXT NOT NULL,
    PRIMARY KEY (recipe_id, console_id)
);
