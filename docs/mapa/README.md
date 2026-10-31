# Mapa funcional de Otteretro

Qué pantalla o comando dispara cada función, qué endpoint llama, qué módulo la resuelve y qué tablas o archivos toca. Rutas relativas a `src/Otteretro/` (backend) y `web/` (front).

**Vivo**: se actualiza en el mismo PR que agrega o cambia una pantalla, un endpoint, un comando o una tabla. Al día con F0 renombrado al inglés (#4) y con los idiomas (#16), 2026-10-23.

## Arranque y modos del ejecutable

| Comando | Qué hace | Módulo |
|---|---|---|
| `otteretro` · `otteretro --fullscreen` | Abre la app: una sola instancia, servidor local y ventana (a pantalla completa con `--fullscreen` o dentro de gamescope) | `App/Program.cs` → `App/Startup.cs`, `App/SingleInstance.cs`, `App/LocalServer.cs`, `App/MainWindow.cs` |
| `otteretro servidor` | API e interfaz sin ventana, para el navegador (desarrollo) | `App/Modes.cs` → `App/LocalServer.cs` |
| `otteretro jugar <id>` | Todavía no implementado (F4): registra el aviso y sale con código 2 | `App/Modes.cs` |
| `otteretro catalogo cargar [--consola X \| --saga X \| --pc-populares]` | Encola la carga desde IGDB: sin opciones, una tarea `igdb.platform` por consola (PC y PC VR no: van por saga) y al final `igdb.popular`; con `--consola`, esa; con `--saga`, `igdb.saga` (la saga puede ir en varias palabras); con `--pc-populares`, `igdb.popular`: los 1.500 juegos de PC con más calificaciones en IGDB, cada uno en su saga | `App/Modes.cs` (`CliMode.EnqueueLoad`) → `Catalog/IgdbImport.cs` |
| `otteretro steam sincronizar` | Encola `steam.sync`: lo que tienes en Steam con horas y última vez; lo que no está en el catálogo se busca en IGDB por su id de Steam (`igdb.steam`) | `App/Modes.cs` → `Catalog/Steam.cs`, `Catalog/IgdbImport.cs` |
| `otteretro catalogo depurar [--contar]` | Encola `claude.cleanup`: Claude (Haiku; lo dudoso, con Sonnet) revisa por lotes las sagas que el script marca como dudosas (nombres parecidos, juegos que no comparten palabras con la saga, datos faltantes, sueltos que empiezan con el nombre de una saga de 2 juegos o más) y aplica solo mover, unir y renombrar; primero tus sagas. Lo que no cambió no se vuelve a pedir. `--contar` solo cuenta y estima los tokens | `App/Modes.cs` → `Claude/CatalogCleanup.cs`, prompt `prompts/depurar-sagas.md` |
| `otteretro catalogo deshacer <n>` | Deshace el cambio `n` de Claude (fila de `learning`): vuelve la saga anterior, recrea la ficha unida con sus versiones o quita el nombre | `Claude/CatalogCleanup.cs` (`Undo`) |
| `otteretro claude cargar [--parte X]` | Encola `claude.load`: la carga inicial con Claude por partes (Nintendo → Sony → Microsoft → PC; cada saga va en la primera parte donde tiene un juego). En cada parte, primero la depuración y después `claude.sagas`, en una sola tarea con su punto de control: se pausa en el límite y sigue sola. `--parte` corre solo esa | `App/Modes.cs` → `Claude/InitialLoad.cs` |
| (tarea `claude.sagas`, también dentro de `claude cargar`) | Claude prepara por lotes, para cada saga: las tres rutas con fuentes (`ruta-saga`, Sonnet), lo icónico (`iconico`, Sonnet), la mejor versión con los 4 criterios (`mejor-version`, Sonnet), las versiones mal clasificadas (`clasificar-version`, Haiku) y la descripción en español (`traducir-descripcion`, Haiku). Todo queda en `review_item` por aprobar; lo que no cambió no se vuelve a pedir | `Claude/SagaAnalysis.cs`, prompts en `prompts/` |
| `otteretro claude ahora` | Deja que Claude trabaje fuera de su horario (00:00 a 08:00 por defecto) hasta que empiece el siguiente; lo que esperaba el horario sale ya. El límite de la suscripción y el tope por ventana siguen valiendo | `App/Modes.cs` → `Claude/ClaudeBudget.cs` (`RunNow`) |
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
| Todas (`layout.tsx` → `components/Shell.tsx`) | Barra lateral, barra superior con panel de tareas, aviso de actualización; con mando (`components/Gamepad.tsx`, `lib/spatial.ts`): foco espacial, barra de botones, menú rápido con Guía y teclado en pantalla, y `data-input` en `<html>`; lleva al asistente si falta la carpeta de datos | `GET /api/state`, `GET /api/events` (SSE), `GET /api/update`, `GET /api/data-folder/move`, `GET /api/tasks`, evento `task` | `Api/Endpoints.cs`, `Api/Events.cs`, `App/TaskQueue.cs` (`components/Tasks.tsx` en el front) | `config.json`, `task` |
| `welcome/` | Asistente pasos 1 y 2: idioma y carpeta de datos (pasos 3 a 7 deshabilitados) | `GET /api/drives`, `POST /api/data-folder`, `PUT /api/language` | `App/DataFolder.cs`, `App/AppConfig.cs` | `config.json`; crea `data/`, `cache/imagenes/`, `Herramientas/`, `logs/` |
| `settings/` · carpeta de datos (`components/DataFolder.tsx`) | Ver la carpeta, usar otra o mudarla con progreso | `GET /api/data-folder`, `GET /api/drives`, `POST /api/data-folder`, `POST /api/data-folder/move`, evento `move` | `App/FolderMove.cs` (copiar, verificar, cambiar `config.json`, borrar el origen) | carpeta de datos completa, `config.json` |
| `settings/` · cuentas y claves (`components/Credentials.tsx`) | Tarjetas de los servicios de la fase en curso (`CURRENT_PHASE`) con su estado; «Configurar» abre un diálogo para pegar, cambiar o borrar y abrir dónde se consigue; los de fases posteriores, plegados (`phase` de cada clave). Nunca se muestra el valor | `GET /api/credentials`, `PUT` · `DELETE /api/credentials/{id}`, `POST /api/credentials/{id}/open` | `App/Credentials.cs` (catálogo de servicios; DPAPI en Windows, AES-GCM con llave maestra en el llavero vía `secret-tool` en Linux) | `credential`; en Linux, la llave maestra en gnome-keyring |
| `settings/` · idioma (`components/LanguagePicker.tsx`) | Elegir el idioma; la interfaz cambia sin reiniciar | `PUT /api/language` | `App/AppConfig.cs` | `language` de `config.json` |
| `settings/` · versión (`components/Update.tsx`) | Versión, buscar actualización y reiniciar para aplicarla | `GET /api/update`, `POST /api/update/check`, `POST /api/update/restart`, evento `update` | `App/Updater.cs` (Velopack; en Linux queda en «se actualiza con el sistema», `withSystem`) | `origenActualizaciones` de `config.json` |
| `page.tsx` (Inicio) | Avisos de las claves que faltan (IGDB, SteamGridDB, Steam) con «Abrir Ajustes»; el resto, estado vacío (`ComingSoon`) | `GET /api/notices` | `Api/CatalogEndpoints.cs` | `credential` |
| `sagas/` | Cuadrícula de sagas (`components/Catalog.tsx`) con filtros (favoritas, que sigo, icónicas, con juegos que tengo, empresa, con PC) y orden; «Emulación» (id 0) al final | `GET /api/sagas`, `GET /api/consoles`, `GET /api/images/…` | `Catalog/CatalogQueries.cs` | `saga`, `game`, `version`, `source`, `console` |
| `saga/?id=` | Cabecera con ★ favorita y seguir la saga, y la tabla de todos sus juegos (las rutas llegan en F2) | `GET` · `PUT /api/sagas/{id}` | `Catalog/CatalogQueries.cs` | `saga` (`favorite`, `following`) |
| `collection/?console=` | Consolas con su número de juegos, filtros (lo tengo, me falta, versión PC), orden, cuadrícula o lista; en PC, tienda y horas, y lo de Steam que aún no está en el catálogo | `GET /api/consoles`, `GET /api/collection/{platform}` | `Catalog/CatalogQueries.cs` | `game`, `version`, `source` |
| `game/?id=` | Ficha: cabecera, Favorito, seguimiento (estado, fechas, nota de 1 a 10, notas; se guarda al cambiar), dónde lo tienes (PC), versiones y «Datos y fuentes» con corregir a mano o volver al de las fuentes | `GET /api/games/{id}`, `PUT /api/games/{id}/progress`, `PUT` · `DELETE /api/games/{id}/fields/{field}` | `Catalog/CatalogQueries.cs`, `Catalog/FieldValues.cs` | `progress`, `field_value`, `game` |
| Barra superior · búsqueda (`components/TopBar.tsx`) | Ctrl+K (o X con mando): sagas y juegos por nombre en español u original; Enter abre el primero | `GET /api/search?q=` | `Catalog/CatalogQueries.cs` | `saga`, `game` |
| `discover/`, `review/`, `downloads/`, `tools/`, `steam/` | Estado vacío con la nutria y la fase que la llena (`ComingSoon`) | — | — | — |

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
| `GET /api/images/{gameId}/{kind}` | Portada (`cover`, 2:3), héroe (`hero`) o logo (`logo`) de un juego desde `cache/imagenes/`; 404 si no hay. La clave va en `?key=` porque se usa en `<img>` |
| `GET /api/tasks` | Tareas de la cola: lo pendiente y lo de las últimas 24 horas (hasta 20). La mudanza de la carpeta de datos responde `move.tasksRunning` mientras corre alguna |
| `GET /api/events` | SSE: `heartbeat` cada 15 s, `move`, `update`, `task` |
| `GET /api/sagas?filter&platform&company&sort` · `GET` · `PUT /api/sagas/{id}` | Sagas con sus plataformas y conteos; detalle con sus juegos; marcar favorita o seguir (`Api/CatalogEndpoints.cs`) |
| `GET /api/consoles` · `GET /api/collection/{platform}?filter&sort` | Las 26 consolas con cuántos juegos hay y cuántos tienes; los juegos de una consola (en PC, también lo de Steam sin catálogo) |
| `GET /api/games/{id}` · `PUT …/progress` · `PUT` · `DELETE …/fields/{field}` | Ficha; guardar el seguimiento (`progress.badStatus`, `badDate`, `badRating`, `notesTooLong`); corregir `name_es`, `original_name` o `year` a mano (`fields.empty`, `fields.badYear`) o quitar la corrección |
| `GET /api/search?q=` | Sagas y juegos por nombre (2 letras como mínimo) |
| `GET /api/notices` | Avisos de Inicio: `notices.missingKey` con el servicio |

## Datos

| Tabla o archivo | Qué guarda | Quién escribe |
|---|---|---|
| `%LocalAppData%\Otteretro\config.json` (en Linux, `~/.config/otteretro/config.json`) | Carpeta de datos, origen de actualizaciones, id del equipo e idioma (`language`; sin él, el del sistema). Escritura atómica. Las claves de F0 siguen en español (`carpetaDatos`, `origenActualizaciones`, `equipoId`) para leer los de antes | `App/AppConfig.cs` |
| `data/otteretro.db` · `machine` | Equipos donde se usa la app | `Data/Migrations.cs` al arrancar |
| `data/otteretro.db` · `credential` | Claves cifradas de cada equipo (`id` = servicio.campo) | `App/Credentials.cs` |
| `data/otteretro.db` · `console` | Las 24 consolas, PC y PC VR (`id` = nombre corto: `snes`, `pc`, `pcvr`…) con su estado de emulación | Semilla en `Data/Migrations/0004_catalog.sql` y `0008_switch2_pcvr.sql` |
| `data/otteretro.db` · `saga`, `game`, `version`, `version_criterion`, `field_value` | El catálogo: la saga sin plataforma, la plataforma en la versión (#131); cada dato con su fuente, y en la columna el ganador (lo manual siempre gana) | Tareas `igdb.saga` e `igdb.platform` (`Catalog/IgdbImport.cs`, `Catalog/Igdb.cs`); después de cada carga, `catalog.extra` (`Catalog/ExtraSources.cs`): Wikidata pone en su saga los juegos que IGDB dejó sin ella y RAWG trae el año que falta (solo con su clave). La descripción de IGDB se guarda como `field_value` (`summary`, sin columna). `Catalog/FieldValues.cs` elige el ganador: lo manual, después la fuente con dato de más prioridad (IGDB, Wikidata, RAWG, HowLongToBeat; en el nombre de una saga, Claude va antes que IGDB). `catalogo depurar` (`Claude/CatalogCleanup.cs`) mueve juegos entre sagas, une fichas repetidas y renombra sagas |
| `data/otteretro.db` · `progress`, `play_session`, `price_watch`, `dismissal` | Seguimiento de cada juego (#133), sesiones de juego (F4), precios a vigilar y sagas descartadas | `progress`: la ficha (`PUT /api/games/{id}/progress`); el resto, todavía nadie |
| `data/otteretro.db` · `task` | Cola de tareas en segundo plano: estado, progreso, detalle (código para el front) y punto de control para seguir tras un reinicio | `App/TaskQueue.cs` (la CLI encola) |
| `data/otteretro.db` · `api_cache` | Respuestas de IGDB y demás fuentes, con su vencimiento (clave: fuente, método, URL y consulta, sin las claves). No va en el respaldo | `App/ApiClient.cs`, que además respeta el ritmo de cada fuente (IGDB: una cada 260 ms) |
| `data/otteretro.db` · `source` | Lo que tienes: por ahora tu cuenta de Steam (`kind = store`), con horas (`playtime`, minutos) y última vez; sin versión mientras no esté en el catálogo | `Catalog/Steam.cs`; cada carga de IGDB enlaza lo suelto |
| `cache/imagenes/{juego}-{cover,hero,logo}.webp` | Imágenes de SteamGridDB (o la carátula de IGDB), en WebP y reducidas al tope de cada tipo: portada 600×900, héroe 1920×620, logo 800 de ancho (#64). Lo que no se puede leer como imagen queda con su extensión original | Tarea `sgdb.images` (`Catalog/SteamGridDb.cs`), que encola cada carga de IGDB; al empezar encola `sgdb.webp` si quedan imágenes en otro formato, que las convierte sin volver a bajarlas |
| `data/otteretro.db` · `claude_usage` | Una fila por llamada a `claude -p`: tarea, prompt, modelo, duración, resultado (`ok`, `invalid`, `failed`, `limit`), tokens y costo (`cost_usd`, con él se estima el uso de cada ventana) | `Claude/ClaudeRunner.cs` |
| `data/otteretro.db` · `claude_fingerprint` | Huella de los datos con los que se pidió cada resultado de Claude (por prompt y clave, «saga:12»): si no cambió, no se vuelve a pedir; borrarla es «recalcular» | `Claude/ClaudeFingerprint.cs` |
| `data/otteretro.db` · `review_item` | Lo que espera tu aprobación en Revisar: los resultados de Claude (`pending_approval`, con `data = { prompt, result }`; uno abierto por prompt y entidad, el nuevo reemplaza al anterior) | `Claude/SagaAnalysis.cs` |
| `data/otteretro.db` · `learning` | Lo que aplicó Claude, con lo que había antes para deshacerlo (`kind = rule`: mover, unir o renombrar de `catalogo depurar`) | `Claude/CatalogCleanup.cs` |
| `data/otteretro.db` · `route`, `route_step`, `review_item` | Rutas de cada saga con sus pasos y lo pendiente de revisar (F2) | Todavía nadie (llegan con los prompts de F2.5) |
| `data/otteretro.db` · `setting` | Ajustes globales (`machine_id` vacío) o por equipo. Claude (F2.2): `claude.scheduleStart` y `claude.scheduleEnd` (horario, `HH:mm`, por defecto 00:00 y 08:00), `claude.windowShare` (tope por ventana de 5 h para lo lanzado de día con «ahora», por defecto 50; de noche no hay tope, #157) y el estado que lleva la app: `claude.windowStart`, `claude.windowCost` (lo que entró en una ventana, aprendido al tocar el límite de sesión), `claude.pausedUntil` y `claude.runNowUntil` | `Claude/ClaudeBudget.cs` |
| `data/otteretro.db` · `migration` | Migraciones aplicadas | `Data/Migrations.cs` |
