# Arquitectura

- **Fecha:** 2026-10-14
- **Base:** [REQUISITOS.md](REQUISITOS.md), [pantallas.md](pantallas.md) y el [prototipo aprobado](https://claude.ai/artifact/Bo8gtfiPdRAumFhkFikHPV) (#91).
- **Estado:** cerrado con la [encuesta 10](https://claude.ai/artifact/SRr97iFU13mskEjBNiobtv) (decisiones #92 a #99). Las marcas **(An)** indican la pregunta de la que sale cada punto; **(propia)** son decisiones sin alternativa razonable que no cambiaste.

---

## 1. Piezas y cómo se comunican

```
                    ┌────────────────────────── Otteretro.exe (un solo ejecutable) ──────────────────────────┐
  doble clic ──────►│ modo APP: servidor local (Kestrel en 127.0.0.1:<puerto aleatorio>)                      │
                    │           + ventana Photino que carga la interfaz (Next.js estática servida por Kestrel)│
                    │                                                                                       │
  acceso de Steam ─►│ modo JUGAR: `otteretro jugar <id>` → prepara, lanza, espera el cierre, encola respaldo │
                    │                                                                                       │
  terminal ────────►│ modo CLI:  `otteretro <tarea>` → ejecuta un script (escanear, exportar, etc.)          │
                    └───────────────┬───────────────────────────────────────────────────────────────────────┘
                                    │ los tres modos usan los mismos módulos y la misma base de datos
                                    ▼
                    <carpeta de datos>\data\otteretro.db (SQLite en modo WAL: varios procesos a la vez)
```

| Tema | Decisión |
|---|---|
| Un solo ejecutable | La app, el lanzador y la CLI son **el mismo `Otteretro.exe`**, con un argumento que elige el modo. Una sola instalación de Velopack y un solo código. (propia) |
| Una sola ventana | Si la app ya está abierta y se vuelve a abrir, se trae al frente la ventana existente (mutex con nombre en Windows, socket en Linux). (propia) |
| Servidor local protegido | Solo escucha en `127.0.0.1`, en un **puerto aleatorio**. Al arrancar se genera una **clave aleatoria** que solo conoce la ventana; toda llamada a la API debe llevarla, y se rechaza cualquier `Host` que no sea `127.0.0.1`. Así ningún otro programa ni página web del PC puede usar la API. (propia) |
| Interfaz | Next.js exportado como estático (`output: "export"`), servido por Kestrel desde el ejecutable. En desarrollo, `next dev` con proxy a la API. (propia) |
| Progreso en vivo | **Server-Sent Events**: el servidor empuja el progreso de tareas, descargas y escaneos; la interfaz lo recibe con `EventSource`. (A2) |
| Tipos compartidos | **Generados desde la API**: ASP.NET Core publica OpenAPI y un paso de build genera los tipos TypeScript del front. Si cambia un dato en C#, el front no compila hasta adaptarse. (A3) |
| Varios procesos | `otteretro jugar` y la CLI escriben en la misma base mientras la app está abierta: SQLite en modo **WAL** y la cola de tareas con bloqueo por fila evitan choques. (propia) |
| Plataforma | **.NET 10** (LTS) (A8). Windows hoy; todo lo específico de Windows (Job Objects, DPAPI, registro) va detrás de una clase por sistema operativo para Linux después. |

## 2. Estructura del repositorio

```
otteretro/
├── src/
│   ├── Otteretro/                 ← el ejecutable: un solo proyecto con una carpeta por módulo (A7)
│   │   ├── App/                   ← Program.cs, modos APP/JUGAR/CLI, una sola instancia, Photino, carpeta de datos, actualizador
│   │   ├── Api/                   ← endpoints HTTP por módulo
│   │   ├── Data/                  ← Dapper + SQL; migraciones en archivos .sql numerados; exportación JSON por tabla (A1)
│   │   ├── Tasks/                 ← cola persistente, ejecución, progreso, notificaciones del sistema
│   │   ├── Libraries/             ← marcador, registro, escanear, reconectar, mover/migrar
│   │   ├── Catalog/               ← sagas, juegos, versiones, fuentes y prioridad por campo
│   │   ├── Collection/            ← identificar, DAT (libretro, Redump), hashes, BASE/UPD/DLC
│   │   ├── Ingest/                ← JDownloader, megapacks, extraer, convertir, ubicar
│   │   ├── Tools/                 ← recetas, instalar/actualizar, rutas internas, archivos de sistema
│   │   ├── Play/                  ← regla "cómo jugar", lanzador, espera del proceso
│   │   ├── Stores/                ← Steam (Web API, SteamKit2), Epic, Xbox, Battle.net, precios
│   │   ├── Steam/                 ← exportación con Steam ROM Manager, categorías, arte
│   │   ├── Backup/                ← repo otteretro-datos, Git LFS
│   │   ├── Claude/                ← `claude -p`, prompts, lotes, pausa por límite, aprendizajes, registro
│   │   ├── Credentials/           ← DPAPI / libsecret
│   │   └── Evolution/             ← issues "automatizar" con gh
│   └── Otteretro.Tests/           ← pruebas de lo delicado y de cada flujo clave (A4)
├── web/                           ← Next.js (pantallas del prototipo), usa el CSS del sistema de diseño
├── recetas/                       ← JSON de emuladores, ports, complementos y tiendas (§5)
├── prompts/                       ← prompts de `claude -p`, uno por tarea
├── docs/
└── .github/workflows/release.yml  ← compila y publica la release con Velopack al fusionar en main (A6)
```

Las carpetas de módulo se crean **cuando la fase las necesita**, no antes.

## 3. Módulos

| Módulo | Responsabilidad | Usa |
|---|---|---|
| **Tareas** | Cola en SQLite: en cola, en curso, pausada (con hora de reanudación), terminada, error. Un trabajador ejecuta las tareas; publica progreso; notificación del sistema al terminar. Las tareas de la cola sobreviven a cerrar la app (respaldo tras jugar, carga de Claude). | Datos |
| **Bibliotecas** | `otteretro-biblioteca.json`, registro, crear, adoptar (incluida la estructura antigua de `E:\Consolas`), renombrar, quitar, reconectar (busca en 2 niveles), mover/migrar (copiar → hash → BD → rutas internas → borrar). Rutas siempre como id de biblioteca + ruta relativa. | Tareas, Herramientas |
| **Catálogo** | Saga → Juego → Versión. Carga inicial por scripts desde IGDB, Wikidata, RAWG, SteamGridDB y HowLongToBeat (opcional). **Prioridad fija por campo**; lo corregido a mano no se sobrescribe. Nombre oficial en español si existe. | Tareas, Claude |
| **Colección** | Origen = archivo o tienda. Escaneo por nombre y TitleID/serial; verificación por hash contra DAT de libretro (No-Intro) y Redump; ScreenScraper para lo que no coincide. Detecta BASE/UPD/DLC. Cola "Revisar". | Catálogo, Bibliotecas |
| **Ingesta** | Proceso único: extraer → identificar → convertir → ubicar → marcar. JDownloader (My.JDownloader.Api o HTTP directo), megapacks con vista previa, 1G1R, duplicados, traducciones de fans. Conversión con DolphinTool (RVZ) y chdman (CHD); PS4 extrae el `.pkg`. | Colección, Herramientas |
| **Herramientas** | Lee las recetas, descarga de la fuente oficial, instala versiones lado a lado, copia previa al actualizar, migra partidas y configuración, reescribe rutas internas, instala contenido dentro del emulador, detecta lo instalado a mano, gestiona BIOS/keys/firmware. | Tareas, Respaldo |
| **Jugar** | Regla automática (PC oficial > port 100 % > emulador perfecto > lo más jugable), elección manual por juego, `otteretro jugar`: prepara (instala UPD, regenera ISO), lanza, **espera al proceso lanzado** y, si deja hijos, Job Object con comprobación cada 60 s; encola el respaldo. Ventana pequeña solo si prepara o falla. | Herramientas, Tareas |
| **Tiendas** | Steam Web API (lo comprado y horas), SteamKit2 (préstamo familiar), Epic (OAuth no oficial; si falla, solo lo instalado), Xbox y Battle.net (lo instalado). Precios con IsThereAnyDeal, GamerPower y Game Pass. Avisos de precio según #84. Solo con la app abierta. | Catálogo |
| **Steam** | Genera manifiestos "Manual" para Steam ROM Manager con `otteretro jugar <id>`, categorías por saga ("Emulación" sin saga), nombre "Juego (Consola)", arte de SteamGridDB, diferencias con la última exportación, por equipo. | Herramientas, Jugar |
| **Respaldo** | Repositorio privado `otteretro-datos`: BD como JSON por tabla, configuración de emuladores, partidas, BIOS/keys. Git LFS (> 50 MB, presupuesto 0). Conflictos: gana lo más reciente y lo otro se guarda aparte. | Tareas, Credenciales |
| **Claude** | Ejecuta `claude -p` con prompts del repo; la respuesta es **JSON validado** (solo datos, nunca código); herramientas permitidas limitadas a búsqueda web y lectura. Lotes de varias sagas por llamada. Detecta el límite de la suscripción → pausa la tarea con hora de reanudación. Guarda aprendizajes reversibles y el registro de uso. | Tareas |
| **Credenciales** | Guarda claves y tokens cifrados con DPAPI (Windows) o libsecret (Linux). | — |
| **Evolución** | Cuando una tarea de Claude se repite, propone crear un issue "automatizar" con `gh`. | Claude |

## 4. Modelo de datos (SQLite)

Tablas principales. `id` entero salvo donde se indica. Tablas, columnas y valores guardados van en inglés, como el código (#139); entre paréntesis, los valores posibles.

| Grupo | Tabla | Campos clave |
|---|---|---|
| Equipo y ajustes | `machine` | id (GUID), name, os |
| | `setting` | name, value, machine_id (vacío = para todos los equipos) |
| | `credential` | service, encrypted_value, machine_id, expires_at |
| Bibliotecas | `library` | id (GUID del marcador), name, kind (games/tools), location (local/network, #136), last_path, connected, layout |
| Catálogo | `console` | id, name, company, emulation_status |
| | `saga` | id, name, igdb_id, favorite, following, iconic, chosen_route |
| | `game` | id, saga_id (vacío = Emulación), name_es, original_name, year, igdb_id, on_pc |
| | `version` | id, game_id, platform (consola o PC), kind (original/remaster/remake), year, export_to_steam |
| | `version_criterion` | version_id, criterion (content/gameplay/technical/original), value, text |
| | `field_value` | entity, entity_id, field, value, source, manual (lo manual nunca se sobrescribe) |
| Colección | `source` | id, version_id, kind (file/store), library_id, relative_path, component (BASE/UPD/DLC), title_id, size, sha1, verified, store, store_id, family_shared |
| | `dat_entry` | system, name, sha1, crc, region, languages, source (libretro/redump) |
| | `alias` | text, game_id, origin (script/claude), undone |
| Rutas y progreso | `route` | id, saga_id, kind (release/story/community), rationale, sources, approved |
| | `route_step` | route_id, position, game_id, recommended_version_id, reason |
| | `progress` | game_id, status (pending/playing/finished/completed/dropped), started_at, finished_at, rating (1–10), notes, favorite (#133) |
| | `play_session` | id, version_id, machine_id, launcher (steam/heroic/lutris/itch/otteretro), started_at, ended_at, duration (#133) |
| Herramientas | `tool` | id, kind (emulator/port/addon/system), name, recipe, console_id, install_path |
| | `tool_version` | tool_id, version, relative_path, installed |
| | `assignment` | game_id o console_id, tool_version_id, custom_config (predeterminado por consola o específico por juego) |
| | `system_file` | console_id, name, version, sha1, relative_path |
| Tiendas | `store_price` | game_id, store, price, lowest, free, game_pass, updated_at |
| | `price_watch` | game_id |
| | `dismissal` | saga_id, until (6 meses, S8) |
| Steam | `steam_export` | machine_id, version_id, name, category, art, exported_at |
| Tareas y Claude | `task` | id, kind, status, progress, detail, data (JSON), resume_at, attempts, created_at |
| | `review_item` | id, kind (doubtful/unidentified/pending_approval), entity, data, status |
| | `learning` | id, kind (alias/rule/recipe/route), content, created_at, undone |
| | `claude_usage` | id, task, duration, result, created_at |
| Caché | `api_cache` | key, response, expires_at |

- Las respuestas de IGDB y demás fuentes se guardan en `api_cache`, para no repetir llamadas y respetar sus límites.
- El respaldo exporta **cada tabla como JSON**, salvo `api_cache` y `credential` (S3).
- `migration` (name, applied_at) registra las migraciones aplicadas. Las bases creadas con F0 tenían las tablas en español; `0002_english_names.sql` y `Migrations.cs` las renombran.

## 5. Recetas

Un JSON por emulador, port, complemento o tienda en `recetas/`. Ejemplo resumido:

```json
{
  "id": "dolphin",
  "tipo": "emulador",
  "consolas": ["gamecube", "wii"],
  "fuente": { "github": "dolphin-emu/dolphin" },
  "versiones": {
    "2509": {
      "windows": { "url": "https://…/dolphin-2509-x64.7z", "hash": "sha256-…" },
      "linux": { "nixpkgs": "dolphin-emu" }
    }
  },
  "portable": { "crear": ["portable.txt"] },
  "partidas": ["User/GC/**", "User/Wii/title/**"],
  "configuracion": ["User/Config/*.ini"],
  "rutas_internas": [{ "archivo": "User/Config/Dolphin.ini", "formato": "ini", "clave": "General.ISOPath0", "valor": "{biblioteca}/GameCube" }],
  "formatos": { "acepta": ["rvz", "iso", "gcz", "wbfs"], "preferido": "rvz" },
  "lanzar": { "args": ["-b", "-e", "{rom}"] }
}
```

Claude escribe la primera receta de algo nuevo; desde ahí la usa el script.

**Versiones fijas y compartidas con Nix (#135):** cada versión lleva, por sistema, la URL y el hash (SRI), o el paquete de nixpkgs si ya está ahí. En Windows Otteretro descarga y verifica con eso; en PlayOS, Nix arma un paquete por versión desde las mismas recetas, y todas las versiones quedan instaladas. Una versión nueva es una entrada más: nunca se borra una vieja.

## 6. Flujos clave

| Flujo | Pasos |
|---|---|
| **Jugar desde Steam** | Steam abre `otteretro jugar 123` → busca la opción elegida → prepara si hace falta (ventana pequeña) → lanza → espera el cierre sin consumir CPU → encola "respaldar partidas" → termina, y Steam deja de contar horas. |
| **Escanear** | Botón Escanear → tarea → recorre la biblioteca → identifica por nombre/TitleID → lo dudoso va a Revisar → progreso en vivo → notificación al terminar. |
| **Importar megapack** | Elegir carpeta → identificar en lote → vista previa (1G1R, duplicados, extras) → importar: por cada juego, mover tras verificar el hash → convertir → marcar. |
| **Carga inicial** | Scripts bajan todos los datos de las APIs a `cache_api` y al catálogo → tareas de Claude por lotes de sagas (Nintendo → Sony → Microsoft) → al llegar al límite, pausa con hora → sigue sola. |
| **Exportar a Steam** | Calcular diferencias → avisar si Steam está abierto → manifiestos para SRM → SRM escribe los accesos y el arte. |

## 7. Lo demás

| Tema | Decisión |
|---|---|
| Registro de errores | Archivo en `<carpeta de datos>\logs\`, uno por día, se guardan **30 días**. (A5) |
| Releases y versiones | **Cada fusión en `main` publica una release** y la versión se calcula sola (0.1.0, 0.2.0…). (A6) |
| Pruebas | **Lo delicado** (mover, verificar hashes, respaldar, reescribir rutas internas, la regla de cómo jugar) **más una prueba de cada flujo clave** de §6. (A4) |
| Front: datos | Las pantallas piden datos a la API con TanStack Query (caché y refresco). (propia) |
| Front: estilo | El CSS del sistema de diseño (`bundle.css` y tokens) se copia a `web/` tal cual; los componentes del prototipo pasan a componentes React con las mismas clases `ot-`. (propia) |
| Idioma del código | Identificadores (clases, carpetas, métodos), tablas y columnas en inglés; interfaz, documentación, comentarios y commits en español. Los nombres de F0 pasaron al inglés en P0.1. (#125, #139) |

## 8. Stack fijado

| Pieza | Elección |
|---|---|
| Runtime | .NET 10 (LTS), un solo proyecto `src/Otteretro` (A7, A8) |
| Servidor | ASP.NET Core minimal API en Kestrel, `127.0.0.1`, puerto aleatorio, clave por sesión |
| Ventana | Photino.NET (WebView2 / WebKitGTK) |
| Base de datos | SQLite (`Microsoft.Data.Sqlite`) en WAL + Dapper; migraciones `.sql` numeradas (A1) |
| Progreso en vivo | Server-Sent Events (A2) |
| Contrato | OpenAPI de ASP.NET Core → tipos TS con `openapi-typescript` (A3) |
| Front | Next.js con `output: "export"`, React, TanStack Query; CSS del sistema de diseño |
| Logs | Archivo diario en `<carpeta de datos>\logs\`, 30 días (A5) |
| Instalación | Velopack (`vpk`), paquetes locales mientras no haya `main` (#101) |
| Pruebas | xUnit |
