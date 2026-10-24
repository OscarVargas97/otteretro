# Otteretro

App personal y portable (C# ASP.NET Core + SQLite, interfaz Next.js estática en una ventana Photino) para catalogar sagas de videojuegos, gestionar bibliotecas de juegos, emuladores y ports en varios discos, y exportarlo todo a Steam con Steam ROM Manager.

## Requisitos

- `docs/REQUISITOS.md` es **la única fuente vigente**. Se lee antes de diseñar o programar cualquier cosa.
- Después de cada encuesta o decisión del usuario:
  1. Guardar las respuestas originales en `docs/historial/respuestas/NN-nombre.json`.
  2. Añadir una entrada en `docs/historial/decisiones.md`. Ese registro solo crece; si algo cambia, se agrega una entrada que reemplaza a la anterior.
  3. Actualizar `docs/REQUISITOS.md` y su fecha de "Última actualización".
- Las encuestas largas se hacen como artifact para completar (capability `db`), no con el selector de opciones.

## Ramas y publicación

- Se trabaja en `dev`. `main` es lo publicado: al fusionar en `main`, GitHub Actions publica una release y la app se autoactualiza.
- Las tareas de automatización llegan como GitHub Issues con la etiqueta `automatizar`.

## Reglas del proyecto

- Claude se usa a través del Claude Code del usuario (`claude -p`), **nunca con la API de Anthropic**.
- Claude decide y los scripts repiten: lo repetitivo y determinista se automatiza para no gastar tokens.
- No borrar nunca versiones viejas de emuladores ni actualizaciones de juegos. Mover es copiar, verificar y recién entonces borrar.
- Todo en español: interfaz, documentación y comentarios.
