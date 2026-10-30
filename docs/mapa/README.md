# Mapa funcional de Otteretro

Qué pantalla o comando dispara cada función, qué endpoint llama, qué módulo la resuelve y qué tablas o archivos toca. Rutas relativas a `src/Otteretro/` (backend) y `web/` (front).

**Vivo**: se actualiza en el mismo PR que agrega o cambia una pantalla, un endpoint, un comando o una tabla. Al día con F0 renombrado al inglés (#4) y con los idiomas (#16), 2026-10-23.

## Arranque y modos del ejecutable

| Comando | Qué hace | Módulo |
|---|---|---|
| `otteretro` · `otteretro --fullscreen` | Abre la app: una sola instancia, servidor local y ventana (a pantalla completa con `--fullscreen` o dentro de gamescope) | `App/Program.cs` → `App/Startup.cs`, `App/SingleInstance.cs`, `App/LocalServer.cs`, `App/MainWindow.cs` |
| `otteretro servidor` | API e interfaz sin ventana, para el navegador (desarrollo) | `App/Modes.cs` → `App/LocalServer.cs` |
| `otteretro jugar <id>` | Todavía no implementado (F4): registra el aviso y sale con código 2 | `App/Modes.cs` |
| `otteretro tareas prueba` | Encola una tarea de prueba (10 pasos de 3 s) para ver el panel de tareas; la toma la app abierta | `App/Modes.cs` → `App/TaskQueue.cs` |
| `otteretro version` · `ayuda` · `openapi <archivo>` | Versión, ayuda y contrato OpenAPI para `npm run tipos` | `App/Modes.cs` |

Los comandos de la CLI siguen en español: son la interfaz que usa la persona (y Steam, con `jugar`).

Al arrancar: migraciones de `Data/Migrations/` (`Data/Migrations.cs`) y registro del equipo actual en `machine`. Logs: `App/FileLog.cs`, en `logs/` de la carpeta de datos (antes de elegirla, junto a `config.json`).

## Idiomas (#140 a #142)

Los textos de la interfaz están en `web/messages/es.json` (base) y `en.json`, con claves y formato ICU; `web/messages/index.ts` los junta y `global.d.ts` les da tipos. `components/Shell.tsx` carga el catálogo del idioma de `GET /api/state` (`language`). El backend no manda textos: errores, avisos y pasos llegan como código (`ApiError { code, params }`, `MoveState.step`, `UpdateState.message`) y `web/lib/i18n.ts` los traduce. `npm run revisar` falla si los catálogos no tienen las mismas claves (`scripts/check-messages.mjs`).

Para sumar un idioma: su `.json` en `web/messages/`, una línea en `messages/index.ts` y su código en `AppConfig.Languages`.

## Pantallas

| Pantalla (`web/app/`) | Qué muestra o hace | Endpoints | Backend | Datos |
|---|---|---|---|---|
| Todas (`layout.tsx` → `components/Shell.tsx`) | Barra lateral, barra superior con panel de tareas, aviso de actualización; lleva al asistente si falta la carpeta de datos | `GET /api/state`, `GET /api/events` (SSE), `GET /api/update`, `GET /api/data-folder/move`, `GET /api/tasks`, evento `task` | `Api/Endpoints.cs`, `Api/Events.cs`, `App/TaskQueue.cs` (`components/Tasks.tsx` en el front) | `config.json`, `task` |
| `welcome/` | Asistente pasos 1 y 2: idioma y carpeta de datos (pasos 3 a 7 deshabilitados) | `GET /api/drives`, `POST /api/data-folder`, `PUT /api/language` | `App/DataFolder.cs`, `App/AppConfig.cs` | `config.json`; crea `data/`, `cache/imagenes/`, `Herramientas/`, `logs/` |
| `settings/` · carpeta de datos (`components/DataFolder.tsx`) | Ver la carpeta, usar otra o mudarla con progreso | `GET /api/data-folder`, `GET /api/drives`, `POST /api/data-folder`, `POST /api/data-folder/move`, evento `move` | `App/FolderMove.cs` (copiar, verificar, cambiar `config.json`, borrar el origen) | carpeta de datos completa, `config.json` |
| `settings/` · cuentas y claves (`components/Credentials.tsx`) | Cada servicio de §5.6 con «guardada» o «falta», pegar, cambiar o borrar, y abrir dónde se consigue. Nunca se muestra el valor | `GET /api/credentials`, `PUT` · `DELETE /api/credentials/{id}`, `POST /api/credentials/{id}/open` | `App/Credentials.cs` (catálogo de servicios; DPAPI en Windows, AES-GCM con llave maestra en el llavero vía `secret-tool` en Linux) | `credential`; en Linux, la llave maestra en gnome-keyring |
| `settings/` · idioma (`components/LanguagePicker.tsx`) | Elegir el idioma; la interfaz cambia sin reiniciar | `PUT /api/language` | `App/AppConfig.cs` | `language` de `config.json` |
| `settings/` · versión (`components/Update.tsx`) | Versión, buscar actualización y reiniciar para aplicarla | `GET /api/update`, `POST /api/update/check`, `POST /api/update/restart`, evento `update` | `App/Updater.cs` (Velopack; en Linux queda en «se actualiza con el sistema», `withSystem`) | `origenActualizaciones` de `config.json` |
| `page.tsx` (Inicio), `sagas/`, `saga/?id=`, `collection/`, `game/?id=`, `discover/`, `review/`, `downloads/`, `tools/`, `steam/` | Estado vacío con la nutria y la fase que la llena (`ComingSoon`) | — | — | — |

## API (`Api/Endpoints.cs`)

Todo bajo `/api`, solo en `127.0.0.1` y con la clave de la sesión (cabecera `X-Otteretro-Key` o `?key=`, `App/LocalServer.cs`). El front llama siempre con `web/lib/client.ts`; los tipos salen de `web/lib/api.d.ts`.

| Endpoint | Para qué |
|---|---|
| `GET /api/state` | Versión, carpeta de datos, si está configurada e idioma |
| `PUT /api/language` | Cambiar el idioma (`es` o `en`) |
| `GET /api/data-folder` · `POST /api/data-folder` | Ver la carpeta o usar otra (sin mover nada) |
| `GET /api/data-folder/move` · `POST /api/data-folder/move` | Estado de la mudanza o empezarla (una a la vez) |
| `GET /api/drives` | Discos para la carpeta de datos, con su espacio libre, la carpeta que propone cada uno y cuál se sugiere (en Linux `~/Otteretro`, sin montajes en memoria; en Windows el de más espacio libre), `App/Mounts.cs` |
| `GET /api/credentials` · `PUT` · `DELETE /api/credentials/{id}` | Estado de cada clave de este equipo, guardarla o borrarla (`credentials.noKeyring` si en Linux no hay llavero, o si ya hay claves guardadas y el llavero no devuelve la llave maestra: nunca se crea otra encima) |
| `POST /api/steam/big-picture` | «Ir a Steam»: abre `steam://open/bigpicture` (el botón sale solo a pantalla completa, `AppState.fullscreen`; `steam.notInstalled` si el sistema no sabe abrirlo) |
| `POST /api/credentials/{id}/open` | Abrir en el navegador dónde se consigue esa clave (`link.openFailed` si no hay navegador) |
| `GET /api/update` · `POST …/check` · `POST …/restart` | Actualización de la app |
| `GET /api/tasks` | Tareas de la cola: lo pendiente y lo de las últimas 24 horas (hasta 20). La mudanza de la carpeta de datos responde `move.tasksRunning` mientras corre alguna |
| `GET /api/events` | SSE: `heartbeat` cada 15 s, `move`, `update`, `task` |

## Datos

| Tabla o archivo | Qué guarda | Quién escribe |
|---|---|---|
| `%LocalAppData%\Otteretro\config.json` (en Linux, `~/.config/otteretro/config.json`) | Carpeta de datos, origen de actualizaciones, id del equipo e idioma (`language`; sin él, el del sistema). Escritura atómica. Las claves de F0 siguen en español (`carpetaDatos`, `origenActualizaciones`, `equipoId`) para leer los de antes | `App/AppConfig.cs` |
| `data/otteretro.db` · `machine` | Equipos donde se usa la app | `Data/Migrations.cs` al arrancar |
| `data/otteretro.db` · `credential` | Claves cifradas de cada equipo (`id` = servicio.campo) | `App/Credentials.cs` |
| `data/otteretro.db` · `console` | Las 23 consolas y PC (`id` = nombre corto: `snes`, `pc`…) con su estado de emulación | Semilla en `Data/Migrations/0004_catalog.sql` |
| `data/otteretro.db` · `saga`, `game`, `version`, `version_criterion`, `field_value` | El catálogo: la saga sin plataforma, la plataforma en la versión (#131); cada dato con su fuente, y en la columna el ganador (lo manual siempre gana) | Tareas `igdb.saga` e `igdb.platform` (`Catalog/IgdbImport.cs`, `Catalog/Igdb.cs`); `Catalog/FieldValues.cs` elige el ganador |
| `data/otteretro.db` · `progress`, `play_session`, `price_watch`, `dismissal` | Seguimiento de cada juego (#133), sesiones de juego (F4), precios a vigilar y sagas descartadas | Todavía nadie |
| `data/otteretro.db` · `task` | Cola de tareas en segundo plano: estado, progreso, detalle (código para el front) y punto de control para seguir tras un reinicio | `App/TaskQueue.cs` (la CLI encola) |
| `data/otteretro.db` · `api_cache` | Respuestas de IGDB y demás fuentes, con su vencimiento (clave: fuente, método, URL y consulta, sin las claves). No va en el respaldo | `App/ApiClient.cs`, que además respeta el ritmo de cada fuente (IGDB: una cada 260 ms) |
| `data/otteretro.db` · `setting` | Ajustes globales o por equipo (todavía sin uso) | — |
| `data/otteretro.db` · `migration` | Migraciones aplicadas | `Data/Migrations.cs` |
