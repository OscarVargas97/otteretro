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
│   │   ├── Inicio/                ← Program.cs, modos APP/JUGAR/CLI, una sola instancia, Photino
│   │   ├── Api/                   ← endpoints HTTP por módulo
│   │   ├── Datos/                 ← Dapper + SQL; migraciones en archivos .sql numerados; exportación JSON por tabla (A1)
│   │   ├── Tareas/                ← cola persistente, ejecución, progreso, notificaciones del sistema
│   │   ├── Bibliotecas/           ← marcador, registro, escanear, reconectar, mover/migrar
│   │   ├── Catalogo/              ← sagas, juegos, versiones, fuentes y prioridad por campo
│   │   ├── Coleccion/             ← identificar, DAT (libretro, Redump), hashes, BASE/UPD/DLC
│   │   ├── Ingesta/               ← JDownloader, megapacks, extraer, convertir, ubicar
│   │   ├── Herramientas/          ← recetas, instalar/actualizar, rutas internas, archivos de sistema
│   │   ├── Jugar/                 ← regla "cómo jugar", lanzador, espera del proceso
│   │   ├── Tiendas/               ← Steam (Web API, SteamKit2), Epic, Xbox, Battle.net, precios
│   │   ├── Steam/                 ← exportación con Steam ROM Manager, categorías, arte
│   │   ├── Respaldo/              ← repo otteretro-datos, Git LFS
│   │   ├── Claude/                ← `claude -p`, prompts, lotes, pausa por límite, aprendizajes, registro
│   │   ├── Credenciales/          ← DPAPI / libsecret
│   │   └── Evolucion/             ← issues "automatizar" con gh
│   └── Otteretro.Tests/           ← pruebas de lo delicado y de cada flujo clave (A4)
├── web/                           ← Next.js (pantallas del prototipo), usa el CSS del sistema de diseño
├── recetas/                       ← JSON de emuladores, ports, complementos y tiendas (§5)
├── prompts/                       ← prompts de `claude -p`, uno por tarea
├── docs/
└── .github/workflows/release.yml  ← compila y publica la release con Velopack al fusionar en main (A6)
```

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

Tablas principales. `id` entero salvo donde se indica.

| Grupo | Tabla | Campos clave |
|---|---|---|
| Equipo y ajustes | `equipo` | id (GUID), nombre, so |
| | `ajuste` | clave, valor, equipo_id (vacío = para todos los equipos) |
| | `credencial` | servicio, valor_cifrado, equipo_id, vence |
| Bibliotecas | `biblioteca` | id (GUID del marcador), nombre, tipo (juegos/herramientas), ultima_ruta, conectada, formato |
| Catálogo | `consola` | id, nombre, empresa, estado_emulacion |
| | `saga` | id, nombre, igdb_id, favorita, sigo, iconica, ruta_elegida |
| | `juego` | id, saga_id (vacío = Emulación), nombre_es, nombre_original, anio, igdb_id, existe_pc |
| | `version` | id, juego_id, plataforma (consola o PC), tipo (original/remaster/remake), anio, exportar_steam |
| | `version_criterio` | version_id, criterio (contenido/jugabilidad/tecnico/original), valor, texto |
| | `dato` | entidad, entidad_id, campo, valor, fuente, manual (lo manual nunca se sobrescribe) |
| Colección | `origen` | id, version_id, tipo (archivo/tienda), biblioteca_id, ruta_relativa, componente (BASE/UPD/DLC), title_id, tamano, sha1, verificado, tienda, tienda_id, prestamo_familiar |
| | `dat_entrada` | sistema, nombre, sha1, crc, region, idiomas, fuente (libretro/redump) |
| | `alias` | texto, juego_id, origen (script/Claude), deshecho |
| Rutas y progreso | `ruta` | id, saga_id, tipo (lanzamiento/historia/comunidad), justificacion, fuentes, aprobada |
| | `ruta_paso` | ruta_id, orden, juego_id, version_recomendada_id, porque |
| | `progreso` | juego_id, estado, nota (1–10), notas, favorito |
| Herramientas | `herramienta` | id, tipo (emulador/port/complemento/sistema), nombre, receta, consola_id, instalacion |
| | `herramienta_version` | herramienta_id, version, ruta_relativa, instalada |
| | `asignacion` | juego_id o consola_id, herramienta_version_id, config_especifica (predeterminado por consola o específico por juego) |
| | `archivo_sistema` | consola_id, nombre, version, sha1, ruta_relativa |
| Tiendas | `tienda_precio` | juego_id, tienda, precio, minimo, gratis, gamepass, actualizado |
| | `seguir_precio` | juego_id |
| | `descarte` | saga_id, hasta (6 meses, S8) |
| Steam | `exportacion` | equipo_id, version_id, nombre, categoria, arte, exportado |
| Tareas y Claude | `tarea` | id, tipo, estado, progreso, detalle, datos (JSON), reanudar_en, intentos, creada |
| | `revision` | id, tipo (dudosa/sin_identificar/por_aprobar), entidad, datos, estado |
| | `aprendizaje` | id, tipo (alias/regla/receta/ruta), contenido, fecha, deshecho |
| | `uso_claude` | id, tarea, duracion, resultado, fecha |
| Caché | `cache_api` | clave, respuesta, expira |

- Las respuestas de IGDB y demás fuentes se guardan en `cache_api`, para no repetir llamadas y respetar sus límites.
- El respaldo exporta **cada tabla como JSON**, salvo `cache_api` y `credencial` (S3).

## 5. Recetas

Un JSON por emulador, port, complemento o tienda en `recetas/`. Ejemplo resumido:

```json
{
  "id": "dolphin",
  "tipo": "emulador",
  "consolas": ["gamecube", "wii"],
  "fuente": { "github": "dolphin-emu/dolphin", "archivo": { "windows": "*-x64.7z", "linux": "*.AppImage" } },
  "portable": { "crear": ["portable.txt"] },
  "partidas": ["User/GC/**", "User/Wii/title/**"],
  "configuracion": ["User/Config/*.ini"],
  "rutas_internas": [{ "archivo": "User/Config/Dolphin.ini", "formato": "ini", "clave": "General.ISOPath0", "valor": "{biblioteca}/GameCube" }],
  "formatos": { "acepta": ["rvz", "iso", "gcz", "wbfs"], "preferido": "rvz" },
  "lanzar": { "args": ["-b", "-e", "{rom}"] }
}
```

Claude escribe la primera receta de algo nuevo; desde ahí la usa el script.

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
| Idioma del código | Nombres de clases y carpetas en español, como en este documento; los términos técnicos estándar (API, token, hash) se quedan como son. (propia) |
