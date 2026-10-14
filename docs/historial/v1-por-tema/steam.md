# Requisito: exportación a Steam

- **Estado:** decidido
- **Fecha:** 2026-10-14

| Tema | Decisión |
|---|---|
| Herramienta | **Steam ROM Manager**. Otteretro genera manifiestos "Manual" (título, ejecutable, argumentos y directorio de inicio). SRM crea los accesos y descarga las carátulas de SteamGridDB. |
| Cuenta | Se **detecta sola** como la última sesión iniciada (`Steam/config/loginusers.vdf`, `MostRecent`). Hay dos cuentas en `userdata`: 112223909 y 314205476. |
| Port y emulador a la vez | Por cada versión se exporta **la opción elegida** (port o emulador), y puedes marcar **varias versiones** del mismo juego. Ver [catalogo.md](catalogo.md#versiones-remasters-y-remakes). |
| Juegos que ya tienes en Steam | **No se exportan**, porque ya están en tu biblioteca. Solo se usan para marcarlos como "lo tengo · versión PC". |
| Categorías | **Por saga**: "The Legend of Zelda", "Xenoblade Chronicles", etc. Lo que no tiene saga va a la categoría **"Emulación"**. |
| Nombre del acceso | **Con la consola**: "The Legend of Zelda: Twilight Princess (GameCube)". |
| Bibliotecas desconectadas | Sus juegos no se exportan y la app avisa. |
