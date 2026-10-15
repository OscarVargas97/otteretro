# Plan de implementación (para la IA que programa)

> **Dónde vamos:** 🟡 **F0 · Esqueleto — en curso** (rama `fase/0-esqueleto`). Última actualización: 2026-10-14.
>
> Leyenda: ⬜ pendiente · 🟡 en curso · ✅ hecho · ⏸️ bloqueado (dice por qué).
> Al terminar una tarea, márcala aquí en el mismo commit. Al cambiar de fase, actualiza la línea de arriba.

---

## 0. Antes de tocar código

### 0.1 Qué leer (en este orden)
1. `CLAUDE.md` — reglas del proyecto.
2. `docs/REQUISITOS.md` — única fuente vigente de requisitos.
3. `docs/arquitectura.md` — piezas, módulos, modelo de datos, recetas, flujos.
4. `docs/pantallas.md` y el prototipo https://claude.ai/artifact/Bo8gtfiPdRAumFhkFikHPV — qué muestra cada pantalla.
5. El sistema de diseño https://claude.ai/artifact/DMH4KuU2kkAnb3ApFTCiuu — tokens y componentes (`bundle.css`, prefijo `ot-`).
6. `docs/plan.md` — el plan para el usuario (resumen); **este archivo** es el detalle.

### 0.2 Reglas de trabajo
- **Ramas:** cada fase en `fase/N-nombre` desde `dev`. Al terminar: PR a `dev` con `gh pr create --base dev`. **Nunca** fusionar en `main` (#101).
- **Commits** pequeños, en español, terminados con las líneas de atribución que indique la sesión.
- **Decisiones nuevas:** si algo no está en los documentos y tiene alternativas reales, **preguntar al usuario antes** y registrar en `docs/historial/decisiones.md` (solo crece) y en `REQUISITOS.md`.
- **Tareas fuera del plan:** registrarlas al final de este archivo (§ Registro de tareas no planificadas).
- **Claude en runtime:** solo con `claude -p`; **nunca** la API de Anthropic.
- **Datos:** nunca borrar versiones viejas de emuladores ni actualizaciones de juegos; mover = copiar → verificar hash → borrar.
- **Idioma:** interfaz, comentarios, nombres de clases/carpetas y documentación en español; términos técnicos estándar (API, token, hash) tal cual.
- **Código mínimo:** sin abstracciones de una sola implementación ni andamiaje "para después". Lo específico de un sistema operativo va detrás de una clase por SO solo donde ya hay dos implementaciones previstas (Windows/Linux).
- **Pruebas (A4):** lo delicado (mover, verificar hash, respaldar, reescribir rutas internas, regla de cómo jugar) y una prueba por flujo clave. xUnit en `src/Otteretro.Tests`.
- **Credenciales y datos del usuario** nunca en el repo ni en logs.

### 0.3 Stack fijado
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

### 0.4 Estructura objetivo
Ver `docs/arquitectura.md` §2. Resumen:
```
src/Otteretro/{Inicio,Api,Datos,Tareas,Bibliotecas,Catalogo,Coleccion,Ingesta,Herramientas,Jugar,Tiendas,Steam,Respaldo,Claude,Credenciales,Evolucion}/
src/Otteretro.Tests/
web/            recetas/            prompts/            .github/workflows/
```
Las carpetas de módulo se crean **cuando la fase las necesita**, no antes.

---

## F0 · Esqueleto 🟡

**Objetivo:** la app se instala, abre su ventana con la navegación del prototipo, guarda en la carpeta de datos elegida y se actualiza con Velopack (paquetes locales).

### Tareas
- ⬜ **F0.1 Entorno.** Verificar .NET 10 SDK, Node LTS, npm. Instalar `vpk` (`dotnet tool install -g vpk`) si falta. Anotar versiones en este archivo.
- ⬜ **F0.2 Solución.** `Otteretro.slnx` (o `.sln`) con `src/Otteretro` (web) y `src/Otteretro.Tests` (xUnit). `Directory.Build.props` con `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` para nuevos avisos. `.gitignore` de .NET y Node. `global.json` fijando el SDK 10.
- ⬜ **F0.3 Modos del ejecutable** (`Inicio/Program.cs`): sin argumentos → APP; `jugar <id>` → JUGAR (por ahora: escribe en log "no implementado" y sale con código 2); `<tarea>` → CLI (por ahora solo `version` y `ayuda`). Parseo con `System.CommandLine` o a mano si son pocos casos (preferir a mano).
- ⬜ **F0.4 Una sola instancia** (`Inicio/UnaInstancia.cs`): mutex con nombre `Local\Otteretro`; si ya existe, avisa a la instancia viva por una tubería con nombre para que traiga su ventana al frente y sale. Solo en modo APP.
- ⬜ **F0.5 Carpeta de datos** (`Inicio/CarpetaDatos.cs`): en `%LocalAppData%\Otteretro\config.json` se guarda solo `{ "carpetaDatos": "E:\\Otteretro" }`. Si no existe o la ruta no está, el front muestra el asistente. Crear `data\`, `cache\imagenes\`, `Herramientas\`, `logs\` al elegirla.
- ⬜ **F0.6 Logs** (`Inicio/Registro.cs`): `Microsoft.Extensions.Logging` + proveedor de archivo mínimo propio (o Serilog si ahorra código), un archivo por día, borrar > 30 días al arrancar. Antes de elegir carpeta de datos: log en `%LocalAppData%\Otteretro\logs\`.
- ⬜ **F0.7 Servidor local** (`Inicio/Servidor.cs`): Kestrel en `127.0.0.1:0` (puerto aleatorio). Clave aleatoria de 32 bytes por sesión. Middleware que exige la clave (cabecera `X-Otteretro-Clave` o cookie `otteretro` puesta al cargar la página con `?clave=`) y rechaza `Host` distinto de `127.0.0.1:<puerto>`. Sirve los estáticos del front desde `wwwroot` embebido. Prueba: petición sin clave → 401; con `Host` falso → 400.
- ⬜ **F0.8 Ventana** (`Inicio/Ventana.cs`): Photino con título "Otteretro", icono (nutria), tamaño mínimo 1100×700, carga `http://127.0.0.1:<puerto>/?clave=<clave>`. Al cerrar la ventana se detiene el servidor.
- ⬜ **F0.9 Base de datos** (`Datos/`): `Bd.cs` abre `data\otteretro.db` con `journal_mode=WAL`, `foreign_keys=ON`, `busy_timeout=5000`. `Migraciones.cs` aplica `Datos/Migraciones/NNNN_nombre.sql` (recursos embebidos) en orden y registra en tabla `migracion`. Primera migración: `equipo`, `ajuste`. Registrar el equipo actual (GUID + nombre de máquina + SO) al arrancar. Prueba: aplicar migraciones dos veces no falla.
- ⬜ **F0.10 SSE** (`Api/Eventos.cs`): `GET /api/eventos` con `text/event-stream`; un `Canal` en memoria donde cualquier módulo publica `{tipo, datos}`. Evento de prueba `latido` cada 15 s para mantener viva la conexión.
- ⬜ **F0.11 API base y OpenAPI**: `GET /api/estado` → `{ version, carpetaDatos, configurada }`; `POST /api/carpeta-datos` `{ ruta }` → valida, crea subcarpetas, guarda config; `GET /api/discos` → unidades con espacio libre (para el asistente). OpenAPI en `/openapi/v1.json`.
- ⬜ **F0.12 Front** (`web/`): Next.js (App Router) con `output: "export"`, `trailingSlash: true`. Copiar `bundle.css` y tokens del sistema de diseño a `web/app/estilos/`. Componentes React con las clases `ot-`: `Barra` (barra lateral con contadores), `BarraSuperior` (título, búsqueda Ctrl+K sin función aún, botón tareas), `EstadoVacio` (nutria), `PanelTareas` (vacío, recibe SSE). Rutas: `/`, `/sagas`, `/sagas/[id]` (generada vacía), `/coleccion`, `/juego/[id]`, `/descubrir`, `/revisar`, `/descargas`, `/herramientas`, `/steam`, `/ajustes`, `/bienvenida`. Cada pantalla: título y estado vacío con la acción que la llenará. La nutria en `web/public/nutria.svg`.
- ⬜ **F0.13 Tipos generados**: script `npm run tipos` → `openapi-typescript http://.../openapi/v1.json -o web/lib/api.d.ts` (o desde el archivo exportado en build). Cliente `web/lib/api.ts` con `fetch` que añade la clave.
- ⬜ **F0.14 Asistente pasos 1–2**: `/bienvenida` (paso 1) y elección de carpeta de datos con la lista de discos y espacio libre (paso 2). Pasos 3–7 visibles pero deshabilitados con "Llega en la fase N".
- ⬜ **F0.15 Build integrado**: `npm run build` en `web/` deja `out/`; el `.csproj` copia `web/out` a `wwwroot` en Release (target MSBuild que ejecuta `npm ci && npm run build` si falta `out/`). En desarrollo: `dotnet run` + `next dev` con proxy (`/api` → puerto del servidor fijado por variable `OTTERETRO_PUERTO`).
- ⬜ **F0.16 Velopack**: `VelopackApp.Build().Run()` al inicio de `Main`. Script `scripts/empaquetar.ps1` que publica `win-x64` self-contained y ejecuta `vpk pack` con versión pasada por parámetro, salida en `artefactos/` (ignorado por git). Actualización desde una carpeta local (`UpdateManager` con `SimpleFileSource`) configurable en `config.json` (`origenActualizaciones`), más tarde GitHub.
- ⬜ **F0.17 GitHub Actions (escrito, no activo)**: `.github/workflows/release.yml` disparado por push a `main`: build, pruebas, `vpk pack`, `vpk upload github`. Queda en el repo; no se ejecuta porque no hay pushes a `main` (#101).
- ⬜ **F0.18 Pruebas**: servidor (401/400), migraciones idempotentes, carpeta de datos (crea subcarpetas, rechaza ruta inválida), modos del ejecutable.
- ⬜ **F0.19 Cierre de fase**: actualizar este archivo y `docs/plan.md`; PR `fase/0-esqueleto` → `dev`.

### Listo cuando
- [ ] `scripts/empaquetar.ps1 -Version 0.0.1` genera instalador; al instalarlo se abre la ventana.
- [ ] El asistente pide la carpeta de datos, la crea con sus subcarpetas y no vuelve a aparecer.
- [ ] Se navega por todas las pantallas con la barra lateral; cada una muestra su estado vacío.
- [ ] Abrir la app dos veces trae la ventana existente al frente.
- [ ] Una llamada a la API sin clave da 401.
- [ ] Con `0.0.2` empaquetado en la carpeta local de actualizaciones, la app 0.0.1 se actualiza sola.
- [ ] `dotnet test` pasa.

---

## F1 · Catálogo ⬜

**Objetivo:** Sagas, fichas y Colección con datos reales; lo que tienes en Steam como "lo tengo · versión PC".
**Pedir al usuario al empezar:** claves de IGDB (Twitch client id/secret), SteamGridDB, Steam Web API, RAWG.

- ⬜ F1.1 `Credenciales/`: guardar y leer cifrado con DPAPI (`ProtectedData`, ámbito usuario) en tabla `credencial`. Ajustes › Cuentas y claves (estado válida/falta/vence). Prueba: cifra y descifra; el valor en la BD no es el texto plano.
- ⬜ F1.2 `Tareas/`: tabla `tarea`, trabajador en segundo plano (un `BackgroundService` con concurrencia 1 por tipo), estados, progreso publicado por SSE, notificación del sistema al terminar tareas largas. Panel de tareas real en el front.
- ⬜ F1.3 Tabla `cache_api` y cliente HTTP con caché y límite de ritmo por fuente (IGDB: 4 req/s).
- ⬜ F1.4 Migración del catálogo: `consola`, `saga`, `juego`, `version`, `version_criterio`, `dato`, `progreso`, `seguir_precio`, `descarte` (ver arquitectura §4). Semilla de las 23 consolas + PC.
- ⬜ F1.5 Importador IGDB (`Catalogo/Igdb.cs`): franquicias/colecciones → sagas; juegos por plataforma de las 23 consolas; `version_parent`, remakes, remasters, ports → versiones; nombres alternativos (español); ids externos de Steam.
- ⬜ F1.6 Wikidata (relación juego–saga que IGDB no tenga), RAWG (complemento), HowLongToBeat opcional (scraper; si falla, "sin datos"). Prioridad fija por campo en `Catalogo/Prioridad.cs`; lo `manual` nunca se sobrescribe. Prueba de la prioridad.
- ⬜ F1.7 SteamGridDB: portadas 2:3, héroes y logos a `cache/imagenes/`; endpoint de imagen local.
- ⬜ F1.8 Steam Web API: `GetOwnedGames` (con horas) de la cuenta detectada en `loginusers.vdf` → `origen` tipo tienda; enlazar por id de Steam de IGDB.
- ⬜ F1.9 Pantallas con datos: Sagas (filtros, orden), detalle de saga (sin rutas), Colección por consola, ficha (versiones, datos y fuentes, corregir campo a mano), búsqueda global Ctrl+K, favoritas, "sigo", progreso, nota 1–10.
- ⬜ F1.10 CLI: `otteretro catalogo cargar [--consola X]`, `otteretro steam sincronizar`.
- ⬜ F1.11 Pruebas y cierre de fase.

**Listo cuando:** saga Zelda completa con versiones y portadas; tus juegos de Steam como "lo tengo".

## F2 · Claude ⬜

- ⬜ F2.1 `Claude/Ejecutor.cs`: lanza `claude -p` con `--output-format json`, prompt de `prompts/<tarea>.md`, entrada como JSON, herramientas permitidas solo búsqueda y lectura web; valida la salida contra un esquema; reintenta 1 vez si no valida.
- ⬜ F2.2 Detección del límite de la suscripción en la salida/código de `claude` → tarea `pausada` con `reanudar_en`; el trabajador la retoma sola.
- ⬜ F2.3 Prompts: `ruta-saga`, `iconico`, `mejor-version`, `clasificar-version`, `traducir-descripcion`. Lotes de varias sagas por llamada.
- ⬜ F2.4 Tablas `ruta`, `ruta_paso`, `revision`, `aprendizaje`, `uso_claude`. Registro de uso por llamada.
- ⬜ F2.5 Recalcular cuando cambian los datos de los que depende un resultado (hash de entradas guardado con el resultado) o cuando se pide. Tope por ejecución tras la carga inicial (Ajustes › Claude).
- ⬜ F2.6 Pantallas: línea de tiempo con selector de ruta, "Por qué este orden", comparativa de 4 criterios, Revisar › Resultados por aprobar y Aprendizajes (deshacer), Inicio › Continuar, Ajustes › Claude.
- ⬜ F2.7 Carga inicial Nintendo → Sony → Microsoft; CLI `otteretro claude cargar`.
- ⬜ F2.8 Pruebas (ejecutor con un `claude` falso) y cierre.

## F3 · Bibliotecas ⬜

- ⬜ F3.1 Tabla `biblioteca`; marcador `otteretro-biblioteca.json` `{id, nombre, tipo, creada, formato}`.
- ⬜ F3.2 Crear, adoptar (incluida la estructura antigua de `E:\Consolas`), renombrar, quitar sin borrar, reconectar (buscar en raíces de discos a 2 niveles).
- ⬜ F3.3 Mover/migrar: copiar → hash → actualizar BD → borrar origen; reanudable si se corta. **Pruebas obligatorias.**
- ⬜ F3.4 Escaneo por nombre y TitleID/serial; BASE/UPD/DLC (Switch por TitleID).
- ⬜ F3.5 DAT: descargar No-Intro de `libretro-database` (GitHub) y Redump; tabla `dat_entrada`; verificación por hash bajo demanda.
- ⬜ F3.6 Revisar › Dudosas y Sin identificar; tabla `alias`.
- ⬜ F3.7 Ajustes › Bibliotecas; asistente paso 4. Pruebas y cierre.

## F4 · Emuladores, ports y Jugar ⬜

- ⬜ F4.1 Formato de receta (JSON Schema en `recetas/receta.schema.json`) y lector.
- ⬜ F4.2 Recetas: Dolphin, Dusklight, Ryujinx SS4K (instalación propia), Eden, Ryubing, Citron, y los predeterminados de cada consola (Claude investiga las recomendaciones de GordoFriki al escribirlas).
- ⬜ F4.3 Instalar desde la fuente oficial, versiones lado a lado, copia previa, migrar partidas y configuración, reescribir rutas internas (ini/json/xml). **Pruebas de reescritura.**
- ⬜ F4.4 Mover emuladores, port y texturas de `E:\Consolas` a `Herramientas\`; Dusklight en modo portable en lugar de la junction.
- ⬜ F4.5 Herramientas › Sistema (BIOS/keys/firmware con versiones) y avisos.
- ⬜ F4.6 Regla de cómo jugar + elección manual. **Pruebas de la regla.**
- ⬜ F4.7 `otteretro jugar`: preparar (UPD/DLC en el emulador, regenerar ISO), lanzar, esperar al proceso lanzado (`WaitForSingleObject`), Job Object + comprobación cada 60 s si deja hijos; ventana pequeña solo si prepara o falla.
- ⬜ F4.8 Portar `E:\Consolas\_Herramientas\Catalogo` (Node) a C#: ports nuevos (Recompendium) y compatibilidad → Inicio › Novedades.
- ⬜ F4.9 Pruebas y cierre. **Pedir:** prod.keys y firmware (A2).

## F5 · Respaldo ⬜

- ⬜ F5.1 Repo privado `otteretro-datos` (crearlo con `gh` si no existe); token de GitHub cifrado (#68). **Pedir el token aquí.**
- ⬜ F5.2 Export de cada tabla a JSON (salvo `cache_api` y `credencial`), al cambiar y al cerrar.
- ⬜ F5.3 Partidas al cerrar el juego (cola), configuración al cambiar, BIOS/keys; Git LFS > 50 MB, presupuesto 0.
- ⬜ F5.4 Conflictos (#77) y restaurar; Ajustes › Respaldo; asistente paso 3. **Pruebas obligatorias.**

## F6 · Steam ⬜

- ⬜ F6.1 Receta de Steam ROM Manager y su CLI.
- ⬜ F6.2 Manifiestos "Manual" con `otteretro jugar <id>`, categorías por saga / "Emulación", nombre "Juego (Consola)", arte de SteamGridDB, por equipo, diferencias.
- ⬜ F6.3 Pantalla Steam; asistente paso 5. Pruebas y cierre.

## F7 · Tiendas y precios ⬜

- ⬜ F7.1 SteamKit2: inicio de sesión con Steam Guard una vez, refresh token cifrado, `GenerateAccessTokenForApp`, `IFamilyGroupsService/GetSharedLibraryApps`. **Comprobar T1.**
- ⬜ F7.2 Epic (OAuth tipo Legendary; si falla, manifiestos de lo instalado); Xbox y Battle.net instalados.
- ⬜ F7.3 IsThereAnyDeal, GamerPower, Game Pass; `tienda_precio`; avisos (#84) solo con la app abierta (#75).
- ⬜ F7.4 Ficha › Tiendas, Inicio › Novedades. Pruebas y cierre.

## F8 · Ingesta ⬜

- ⬜ F8.1 MyJDownloader (librería `My.JDownloader.Api` o HTTP directo); enlaces desde la ficha y en lista.
- ⬜ F8.2 Megapacks: vista previa, 1G1R (español → Europa → USA → Japón), duplicados, solo traducciones de fans.
- ⬜ F8.3 Conversión DolphinTool/chdman; formato que pide el port; PS4 extrae `.pkg`.
- ⬜ F8.4 ScreenScraper para lo que no coincide (credenciales propias).
- ⬜ F8.5 Pantalla Descargas; asistente paso 6. Pruebas y cierre.

## F9 · Descubrir, logros y pulido ⬜

- ⬜ F9.1 Descubrir (Claude), "no me interesa" 6 meses.
- ⬜ F9.2 RetroAchievements en la ficha y en los emuladores compatibles.
- ⬜ F9.3 Evolución: sugerir issues "automatizar" con `gh`.
- ⬜ F9.4 Asistente paso 7; repaso de estados vacíos, errores y la nutria. Cierre.

---

## Registro de tareas no planificadas

Cada entrada: fecha, fase en curso, qué se hizo, por qué, cómo (archivos/commits) y si generó decisión.

| Fecha | Fase | Qué | Por qué | Cómo | Decisión |
|---|---|---|---|---|---|
| — | — | (sin entradas todavía) | | | |
