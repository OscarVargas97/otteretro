# Otteretro — instrucciones para agentes de IA

App personal, instalada con Velopack y con los datos en una carpeta que elige el usuario (C# ASP.NET Core + SQLite, interfaz Next.js estática en una ventana Photino) para catalogar sagas de videojuegos, gestionar bibliotecas de juegos, emuladores y ports en varios discos, y exportarlo todo a Steam con Steam ROM Manager. A futuro va dentro de PlayOS (NixOS).

## Antes de tocar nada

1. `docs/REQUISITOS.md`: **la única fuente vigente**. Se lee antes de diseñar o programar.
2. `docs/arquitectura.md` (con el stack, §8) y `docs/pantallas.md`, con el [prototipo](https://claude.ai/artifact/Bo8gtfiPdRAumFhkFikHPV) y el [sistema de diseño](https://claude.ai/artifact/DMH4KuU2kkAnb3ApFTCiuu) (tokens y componentes, prefijo `ot-`).
3. El milestone de la fase en curso (qué trae y «Listo cuando») y el issue de la rama: su `## Estado` dice qué está hecho y el próximo paso.
4. Antes de tocar el front: `web/AGENTS.md` (Next 16: leer la guía que corresponda en `web/node_modules/next/dist/docs/`).

Índice de la documentación: `docs/README.md`.

## Cómo se trabaja

Desde un **espacio** de [ia-workspace](https://github.com/OscarVargas97/ia-workspace) con Otteretro y PlayOS, con el plugin `otter`. Su `AGENTS.md` explica los worktrees (`repos/otteretro/<rama>`), el estado de cada tarea en su issue, el guardián y el flujo completo; este repo es `repos/otteretro/` dentro de él. Para armarlo en otra máquina: `make espacio DIR=<carpeta>` desde un clon de ia-workspace y, adentro, `make repo URL=OscarVargas97/otteretro` y `make repo URL=OscarVargas97/playos`. La config del guardián de este repo (`.claude/otter.json`) y sus adaptaciones (`.claude/adaptaciones/otter.md`) viven acá y el espacio las toma solas. Lo propio de este repo:

- **Requerimientos y decisiones**: toda decisión de la persona sale de una encuesta (`otter:requerimientos`). Después de cada una:
  1. Respuestas en el issue «Encuesta NN» de la encuesta, sin contenido personal (#146).
  2. Una entrada por decisión en `docs/historial/decisiones.md`. **Solo crece**: si algo cambia, se agrega una entrada que reemplaza a la anterior.
  3. `docs/REQUISITOS.md` actualizado, con su fecha de "Última actualización".
- **Tareas**: en GitHub. Solicitud = issue con etiqueta `solicitud`; tareas = sus sub-issues; estado en el [Project otteretro](https://github.com/users/OscarVargas97/projects/2); un milestone por fase. **El plan también vive en GitHub** (#143): cada fase es un milestone (qué trae, qué pedir y «Listo cuando»), lo que viene son borradores del Project con el campo `Fase` (vista «Plan») y la fase en curso tiene su issue «Fase N». `docs/plan.md` solo dice el orden de las fases y por qué.
- **Ramas**: cada fase en `fase/N-nombre` desde `dev`; lo demás en `feat|fix|refactor|chore|docs/<nombre>`. Todo entra a `dev` por PR. **Por ahora nada se fusiona en `main`** (#101): cuando se decida publicar, al fusionar en `main` GitHub Actions publica una release y la app se autoactualiza.
- **Commits** pequeños, en Conventional Commits y en español (`feat(catalog): …`), con `Refs #n` de la tarea.
- **Tareas fuera del plan**: sin issue no hay trabajo. Lo que aparece pasa por `otter:requerimientos` y queda como issue (o como criterio o sub-issue de la tarea en curso). El registro anterior está en `docs/historial/tareas-no-planificadas.md`.
- **Revisión**: `docs/review-checklist.md` tiene los errores propios de este stack.
- **Mapa funcional**: `docs/mapa/README.md`. Si un cambio agrega o cambia una pantalla, un endpoint, un comando o una tabla, el mapa se actualiza en el mismo PR.

## Reglas del proyecto

- Claude se usa a través del Claude Code del usuario (`claude -p`), **nunca con la API de Anthropic**.
- Claude decide y los scripts repiten: lo repetitivo y determinista se automatiza para no gastar tokens. Las tareas de automatización llegan como issues con la etiqueta `automatizar`.
- No borrar nunca versiones viejas de emuladores ni actualizaciones de juegos. Mover es copiar, verificar y recién entonces borrar.
- Idioma: **identificadores en inglés** (#125), también las tablas y columnas de SQLite (#139); interfaz, documentación, comentarios, commits, PRs e issues en español neutro. Lo que ve o guarda la persona no se traduce: textos, comandos de la CLI, claves de `config.json` y nombres de carpetas de datos.
- Interfaz en español e inglés (#140): los textos viven en `web/messages/<idioma>.json`, nunca escritos en los componentes; el backend devuelve códigos y el front los traduce (#142). Cada texto nuevo va en los dos catálogos.
- Código mínimo: sin abstracciones de una sola implementación ni andamiaje "para después". Lo propio de cada sistema operativo, detrás de su clase (Windows y Linux/PlayOS).
- Pruebas (A4): lo delicado (mover, verificar hash, respaldar, reescribir rutas internas, regla de cómo jugar) y una prueba por flujo clave. xUnit en `src/Otteretro.Tests`.
- Credenciales y datos del usuario nunca en el repo ni en logs.

## Comandos

| Para | Comando |
|---|---|
| Pruebas | `dotnet test` |
| Compilar todo (incluye el front) | `dotnet build src/Otteretro -c Release` |
| Revisar tipos y catálogos del front | `npm --prefix web run revisar` |
| Pruebas de interfaz (Playwright con el Chrome instalado, en español e inglés) | `npm --prefix web run e2e` |
| Regenerar los tipos del front tras cambiar la API | `cd web && npm run tipos` |
| Ver la interfaz en un navegador sin ventana ni tocar la config real | `OTTERETRO_CONFIG=<ruta>/config.json Otteretro.exe servidor` → abre la URL que imprime |
| Front en caliente | `cd web && npm run dev` (puerto 3000) y la app con `OTTERETRO_DEV_WEB=http://localhost:3000` |
| Instalador local | `./scripts/empaquetar.ps1 -Version 0.0.N` → `artefactos/paquetes` (también es el origen de actualizaciones local) |
| Paquete de Nix (en Linux con Nix) | `nix build` (compila el front y el back y corre `dotnet test`); `nix flake check` |
| Probar en Linux | `nix build` y `OTTERETRO_CONFIG=/tmp/x/config.json ./result/bin/Otteretro servidor` (API) o `./result/bin/Otteretro` (ventana, con `DISPLAY`); `--fullscreen` simula la sesión Play |

### Paquete de Nix

`flake.nix` exporta `packages.x86_64-linux.default`, `nixosModules.default` (`programs.otteretro.enable = true;`) y `checks`. Compila sin red, así que fija las dependencias:

- **Al cambiar un paquete NuGet**: `nix build .#default.fetch-deps -o fetch-deps && ./fetch-deps deps.json` y commitear `deps.json`.
- **Al cambiar `web/package-lock.json`**: poner `npmDepsHash = lib.fakeHash;` en `flake.nix`, correr `nix build .#web` y copiar el hash que dice «got:».
- **Fuentes**: van en `web/app/fonts` (`next/font/local`); `next/font/google` no compila sin red.
- La ventana se abre por XWayland (`GDK_BACKEND=x11` en el wrapper): en Wayland con NVIDIA, WebKitGTK no dibuja (#8).

Desde Windows no hay Nix: se prueba por ssh en el portátil de juego. Para los hashes (`npmDepsHash`, `deps.json`) se usa un `git clone` de la rama, no una copia desde Windows: los saltos CRLF de Windows cambian el hash. Para claves en Linux sin tocar el sistema: `nix shell nixpkgs#gnome-keyring nixpkgs#libsecret -c dbus-run-session -- …` con `HOME` y `XDG_RUNTIME_DIR` en `/tmp` y `gnome-keyring-daemon --unlock --components=secrets`.

Cómo se arma todo en Linux (rutas, ventana, claves, paquete): `docs/arquitectura.md` «Linux y Nix». El contrato con PlayOS está en el repo de docs, `integraciones/otteretro--playos.md`.
