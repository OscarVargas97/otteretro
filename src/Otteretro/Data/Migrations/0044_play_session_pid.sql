-- #452: el pid del `otteretro jugar` que espera al juego. «Jugando…» solo mientras ese proceso siga vivo: si muere (una
-- actualización, un cierre a la fuerza), la sesión abierta deja de contar en vez de quedarse hasta el día siguiente.
ALTER TABLE play_session ADD COLUMN pid INTEGER;
