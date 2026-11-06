-- #334 (decisión 339, reemplaza a la 152): Switch 2 tiene emuladores experimentales (Lichii, Eglantine, Eden NX2). La 0008 ya está
-- aplicada y no se edita: acá cambia su estado en la copia local del catálogo. La fuente es otteretro-catalogo: una versión nueva del
-- catálogo trae el valor que publique.
UPDATE catalog.console SET emulation_status = 'early' WHERE id = 'switch2';
