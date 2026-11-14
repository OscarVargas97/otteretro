-- #407 (decisión 380): el mejor emulador lo gestiona solo el catálogo (`recommendedFor` de las recetas). Se quita la búsqueda local:
-- lo elegido a mano en `emulator_pick` (por juego y consola) pasa a `play_choice` en cada versión del juego en esa consola, sin pisar
-- una elección que ya tenga. Lo que encontró Claude y la fecha de las búsquedas se pierden, igual que sus tareas.
INSERT INTO play_choice (version_id, option, chosen_at)
SELECT v.id, 'emulator:' || p.recipe_id, p.searched_at FROM emulator_pick p JOIN version v ON v.game_id = p.game_id AND v.platform = p.console
WHERE p.origin = 'manual' AND p.recipe_id IS NOT NULL
ON CONFLICT (version_id) DO NOTHING;
DROP TABLE emulator_pick;
DROP TABLE emulator_search;
DELETE FROM task_log WHERE task_id IN (SELECT id FROM task WHERE kind = 'claude.bestEmulator');
DELETE FROM task WHERE kind = 'claude.bestEmulator';
