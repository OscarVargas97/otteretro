# Investigación técnica previa al desarrollo

- **Fecha:** 2026-10-14
- **Objetivo:** cerrar los pendientes técnicos T1 a T7 de [REQUISITOS.md §13.2](REQUISITOS.md) para que el desarrollo tenga que tomar la menor cantidad posible de decisiones.
- **Fiabilidad:** casi todo sale de fuentes comunitarias, porque Valve, Microsoft, Epic y las demás tiendas no documentan estas APIs. Cada punto indica lo que **falta comprobar** en el primer prototipo.

| # | Tema | ¿Es viable? | Decisión |
|---|---|---|---|
| T1 | Préstamo familiar de Steam | 🟡 Sí, con un paso manual | Un botón "Actualizar préstamo familiar" que pide iniciar sesión en Steam |
| T2 | Disponibilidad en tiendas | ✅ | IsThereAnyDeal (precios y tiendas) + GamerPower (juegos gratis) + catálogo de Game Pass (no oficial) |
| T3 | Partidas de cada port | ✅ por receta | Se investiga al escribir la receta de cada port. Dusklight ya está resuelto |
| T4 | Formato que exige cada port | ✅ por receta | **Dusklight acepta RVZ**, así que la ISO de Twilight Princess puede comprimirse |
| T5 | Autoactualización | ✅ | Actualizador propio y mínimo, que reemplaza los archivos al reiniciar. Velopack queda descartado porque no es portable |
| T6 | Lo que tienes en otras tiendas | 🟡 Por etapas | Steam, Epic y GOG con la cuenta completa. EA, Ubisoft y Xbox, al principio solo lo instalado |
| T7 | Saber cuándo se cierra un juego | ✅ | Job Objects en Windows y grupo de procesos en Linux, comprobando cada pocos segundos si quedan procesos vivos |

---

## T1 · Préstamo familiar de Steam 🟡

- **Cómo se obtiene:** `IFamilyGroupsService/GetSharedLibraryApps/v1` es un endpoint **no documentado**. Necesita el `family_groupid`, que se obtiene con `GetFamilyGroupForUser`.
- **El problema:** **no acepta la clave de la Web API.** Pide un *access token* de sesión del navegador, que dura unas 24 h. Por eso no puede ejecutarse solo, sin que intervengas.
- **Cobertura:** devuelve solo los juegos que se pueden compartir, así que pueden faltar títulos que sí ves en la vista de familia.
- **Decisión:**
  - Lo comprado por ti se lee con la Web API y su clave, de forma automática.
  - El préstamo familiar se actualiza con un botón. Ese botón abre una ventana para iniciar sesión en Steam, la app toma el token y lo usa en ese momento sin guardarlo.
  - Lo que devuelve se guarda en caché y se marca como "préstamo familiar".
- **Falta comprobar:** de qué página de Steam se puede leer el token después de iniciar sesión.
- Fuentes: [Issue de PlayniteExtensions #396](https://github.com/JosefNemec/PlayniteExtensions/issues/396) · [AugmentedSteam #1907](https://github.com/IsThereAnyDeal/AugmentedSteam/issues/1907) · [ludarium #62](https://github.com/Halaburda00/ludarium/issues/62) · [steam-go familygroupsservice](https://pkg.go.dev/github.com/gofurry/steam-go/api/familygroupsservice) · [Steamworks: Families](https://partner.steamgames.com/doc/features/families)

## T2 · Disponibilidad en tiendas ✅

| Dato | Fuente | Acceso |
|---|---|---|
| En qué tiendas está, precio actual y precio mínimo histórico | **IsThereAnyDeal API** (`/games/prices/v3`, `/games/overview`) | Clave gratuita. Admite filtro por país y por tienda. Hay un cliente para .NET |
| Juegos gratis (Epic, GOG, Steam…) | **GamerPower API** (`/v1/giveaways?platform=epic-games-store`) | Gratis, sin clave. Pide atribución |
| Catálogo de PC Game Pass | `catalog.gamepass.com/sigls` + `displaycatalog.mp.microsoft.com` | **No oficial**: puede cambiar sin aviso. Si falla, se muestra "sin datos" y no se interrumpe nada |

- Las tres fuentes las consultan scripts, sin Claude, y se refrescan periódicamente (§11.2).
- **Falta comprobar:** el GUID de la lista de PC Game Pass (está en el código de *catabox* o *xodus*) y si GamerPower informa también de los juegos que estuvieron gratis en el pasado.
- Fuentes: [IsThereAnyDeal API](https://docs.isthereanydeal.com/) · [IsThereAnyDealApi (NuGet)](https://packages.nuget.org/packages/IsThereAnyDealApi) · [GamerPower Giveaways API](https://apis.io/apis/gamerpower/gamerpower-giveaways-api/) · [catabox](https://github.com/nanhopper/catabox) · [xodus PR #126](https://github.com/xodus-gaming/xodus/pull/126)

## T3 y T4 · Ports: partidas y formatos ✅

- **Regla general:** cada receta de port declara sus **formatos aceptados** y **dónde guarda las partidas**. Claude lo investiga una vez, al escribir la receta, y después lo aplica el script.
- **Dusklight**, que es el único port que tienes hoy:
  - **Formatos:** ISO, GCM, CISO, GCZ, NFS, **RVZ**, WBFS, WIA y TGC. Twilight Princess **se puede guardar en RVZ** y lo leen tanto Dusklight como Dolphin.
  - **Partidas:** `…\Dusklight\<REGIÓN>\Card A\*.gci`. Tú tienes la región `EUR`. Es el **mismo formato `.gci` que Dolphin**, así que las partidas se pueden pasar de uno a otro.
  - **Modo portable:** viene incluido (Ajustes → Data Folder → Portable Mode, o `--user-dir`). Sustituye a la *junction* que se usa hoy.
  - Al primer arranque migra solo las partidas de la carpeta antigua `TwilitRealm\Dusk\`.
- Fuentes: [Dusklight (GitHub)](https://github.com/TwilitRealm/dusklight) · [Guía de instalación en Windows](https://duskport.com/dusk/install/windows/) · [Emulation King](https://emulationking.com/nintendo/gamecube/games/legendofzeldatwilightprincess/ports/dusklight/)

## T5 · Autoactualización ✅

- **Velopack** es el actualizador estándar en .NET y funciona con GitHub Releases en Windows, Linux y macOS. **Pero en Windows instala en su propia carpeta y reemplaza el directorio entero**, y eso **choca con P1**, porque la app debe ser portable y llevar `data\` y `Herramientas\` dentro.
- **Decisión: actualizador propio y mínimo.**
  1. La app consulta la última release en GitHub.
  2. Descarga el zip a `data\actualizacion\`.
  3. Al cerrarse, un pequeño ayudante (`Otteretro.Updater`) reemplaza **solo los archivos de la app**, nunca `data\` ni `Herramientas\`, y la vuelve a abrir.
  - **En Linux** se puede renombrar el ejecutable aunque esté en uso, así que el reemplazo es inmediato. **En Windows** hay que esperar a que la app se cierre, que es lo que hace el ayudante.
- **Falta comprobar:** en el primer prototipo, una actualización completa en Windows.
- Fuentes: [Velopack: integración](https://docs.velopack.io/integrating/overview) · [Velopack: Windows](https://docs.velopack.io/packaging/operating-systems/windows)

## T6 · Otras tiendas de PC 🟡

| Tienda | Qué tienes | Cómo se lanza | Etapa |
|---|---|---|---|
| Steam | Web API (todo lo comprado) + préstamo familiar (T1) | `steam://rungameid/<appid>` | 1 |
| Epic | Inicio de sesión con OAuth2 (*device flow*), como Playnite o Legendary/Heroic, y además los manifiestos locales de lo instalado | `com.epicgames.launcher://apps/<id>?action=launch&silent=true` | 1 |
| GOG | Inicio de sesión con OAuth2 + PKCE, y la detección local | El ejecutable directo, o `goggalaxy://` | 1 |
| EA app | **Solo lo instalado**, más el historial local de instalaciones. No hay API de cuenta | `origin2://game/launch?offerIds=…` | 2 |
| Ubisoft Connect | **Solo lo instalado** al principio | `uplay://launch/<id>/0` | 2 |
| Xbox para PC | **Solo lo instalado** (paquetes AppX) al principio. Para leer la cuenta completa hace falta autenticarse en Xbox Live | `shell:AppsFolder\<AUMID>` | 2 |

- **Decisión:** se hace por etapas.
  - **Etapa 1:** Steam, Epic y GOG, con todo lo que tienes en la cuenta.
  - **Etapa 2:** EA, Ubisoft y Xbox, al principio solo con lo instalado. Leer toda la cuenta de esas tiendas queda como mejora posterior.
- **Falta comprobar:** los esquemas exactos de lanzamiento. Salen de lo conocido y se validan juego por juego en las recetas de cada tienda.
- Fuentes: [Playnite: comparación con otros programas](https://api.playnite.link/docs/manual/gettingStarted/comparisonWithOtherSoftware.html) · [Playnite: añadir juegos](https://api.playnite.link/docs/manual/library/games/addingGames.html) · [Notas de versión de Playlite](https://github.com/playlite-app/playlite/releases)

## T7 · Lanzador `otteretro jugar` ✅

- **Por qué importa:** Steam cuenta el tiempo de juego mientras vive el proceso que lanzó. Por eso `otteretro jugar` tiene que **seguir abierto hasta que el juego termine**. Así Steam mide bien el tiempo y Otteretro sabe cuándo respaldar las partidas.
- **En Windows:** el emulador o port se mete en un **Job Object** (con *kill-on-close*). Para saber cuándo termina, cada pocos segundos se mira **si queda algún proceso dentro del job**. No se confía solo en las notificaciones del job, porque Microsoft no garantiza que lleguen.
- **En Linux:** se usa un grupo de procesos propio y se recorre `/proc` para ver si quedan descendientes.
- **Juegos de tiendas,** que se abren a través de su launcher: el proceso del juego no queda dentro del job. Se vigila por el **nombre o la ruta del ejecutable** que indica la receta de la tienda o el manifiesto del juego.
- Fuentes: [Raymond Chen: esperar a que terminen todos los procesos de un job](https://devblogs.microsoft.com/oldnewthing/20130405-00/?p=4743) · [Ejemplo de Job Objects](https://github.com/jarlostensen/winjobobjects)
