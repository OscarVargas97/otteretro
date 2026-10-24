# Otteretro — instrucciones para agentes de IA

App personal, instalada con Velopack y con los datos en una carpeta que elige el usuario (C# ASP.NET Core + SQLite, interfaz Next.js estática en una ventana Photino) para catalogar sagas de videojuegos, gestionar bibliotecas de juegos, emuladores y ports en varios discos, y exportarlo todo a Steam con Steam ROM Manager. A futuro va dentro de PlayOS (NixOS).

## Antes de tocar nada

1. `docs/REQUISITOS.md`: **la única fuente vigente**. Se lee antes de diseñar o programar.
2. `docs/arquitectura.md`, `docs/pantallas.md` y la sección de la fase en curso de `docs/plan-implementacion.md` (dice qué leer después y cómo se ejecuta todo).
3. Si la tarea tiene `work/<rama>/ESTADO.md` en el workspace, ese archivo primero: dice qué está hecho y el próximo paso.

Índice de la documentación: `docs/README.md`.

## Cómo se trabaja

Desde un **espacio** de [ia-workspace](https://github.com/OscarVargas97/ia-workspace) con Otteretro y PlayOS (en esta máquina, una carpeta local), con el plugin `otter`. Su `AGENTS.md` explica los worktrees (`repos/otteretro/<rama>`), `work/<rama>/ESTADO.md`, el guardián y el flujo completo; este repo es `repos/otteretro/` dentro de él. Para armarlo en otra máquina: `make espacio DIR=<carpeta>` desde un clon de ia-workspace y, adentro, `make repo URL=OscarVargas97/otteretro` y `make repo URL=OscarVargas97/playos`. La config del guardián de este repo (`.claude/otter.json`) y sus adaptaciones (`.claude/adaptaciones/otter.md`) viven acá y el espacio las toma solas. Lo propio de este repo:

- **Requerimientos y decisiones**: toda decisión de la persona sale de una encuesta (`otter:requerimientos`). Después de cada una:
  1. Respuestas originales en `docs/historial/respuestas/NN-nombre.json`.
  2. Una entrada por decisión en `docs/historial/decisiones.md`. **Solo crece**: si algo cambia, se agrega una entrada que reemplaza a la anterior.
  3. `docs/REQUISITOS.md` actualizado, con su fecha de "Última actualización".
- **Tareas**: en GitHub. Solicitud = issue con etiqueta `solicitud`; tareas = sus sub-issues; estado en el [Project otteretro](https://github.com/users/OscarVargas97/projects/2); un milestone por fase. `docs/plan-implementacion.md` guarda el detalle técnico de cada fase, no su avance.
- **Ramas**: cada fase en `fase/N-nombre` desde `dev`; lo demás en `feat|fix|refactor|chore|docs/<nombre>`. Todo entra a `dev` por PR. **Por ahora nada se fusiona en `main`** (#101): cuando se decida publicar, al fusionar en `main` GitHub Actions publica una release y la app se autoactualiza.
- **Tareas fuera del plan**: se registran al final de `docs/plan-implementacion.md` (qué, por qué, cómo) y, si quedan pendientes, como issue.
- **Revisión**: `docs/review-checklist.md` tiene los errores propios de este stack.
- **Mapa funcional**: `docs/mapa/README.md`. Si un cambio agrega o cambia una pantalla, un endpoint, un comando o una tabla, el mapa se actualiza en el mismo PR.

## Reglas del proyecto

- Claude se usa a través del Claude Code del usuario (`claude -p`), **nunca con la API de Anthropic**.
- Claude decide y los scripts repiten: lo repetitivo y determinista se automatiza para no gastar tokens. Las tareas de automatización llegan como issues con la etiqueta `automatizar`.
- No borrar nunca versiones viejas de emuladores ni actualizaciones de juegos. Mover es copiar, verificar y recién entonces borrar.
- Idioma: **identificadores en inglés** (#125); interfaz, documentación, comentarios, commits, PRs e issues en español neutro. Los nombres de F0 todavía están en español y se renombran al inglés (issue propio).
- Código mínimo: sin abstracciones de una sola implementación ni andamiaje "para después". Lo propio de cada sistema operativo, detrás de su clase (Windows y Linux/PlayOS).
- Credenciales y datos del usuario nunca en el repo ni en logs.

## Comandos

| Para | Comando |
|---|---|
| Pruebas | `dotnet test` |
| Compilar todo (incluye el front) | `dotnet build src/Otteretro -c Release` |
| Revisar tipos del front | `npm --prefix web run revisar` |
| Regenerar los tipos del front tras cambiar la API | `cd web && npm run tipos` |
| Más (modo servidor, front en caliente, instalador) | `docs/plan-implementacion.md` §0.1b |
