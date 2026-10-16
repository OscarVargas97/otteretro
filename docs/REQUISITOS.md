# Otteretro: requisitos consolidados

> **Este es el único documento vigente.** Si algo de aquí contradice el historial, manda este documento.
> **Última actualización:** 2026-10-16, después de la encuesta 12, "PlayOS · refinamiento" (#129 a #138).

## Cómo se mantiene

- Después de cada encuesta o decisión:
  1. Se guardan las respuestas originales en [historial/respuestas/](historial/respuestas/).
  2. Se añade una entrada en [historial/decisiones.md](historial/decisiones.md), el registro cronológico de qué cambió y cuándo.
  3. Se **actualiza este documento** para que refleje siempre el estado actual.
- [historial/v1-por-tema/](historial/v1-por-tema/) guarda la primera versión de los requisitos, separada por tema, antes de consolidarla.

| Estado | Significado |
|---|---|
| ✅ | Decidido |
| 🔬 | Decidido, pero falta comprobar que se puede hacer técnicamente (ver §13) |
| 🟡 | Decidido en lo general, con detalles abiertos (ver §13) |

---

## 1. Visión

Otteretro es una app **personal** para:

1. **Catalogar** las sagas icónicas y los juegos interesantes de las consolas elegidas, desde lo retro hasta la generación de PS5 y Switch, incluida la versión de PC.
2. **Saber qué tienes**: archivos en tus bibliotecas y juegos en tu cuenta de Steam.
3. **Elegir cómo jugar** cada juego: versión de PC, port o emulador, y cuál es la mejor versión.
4. **Conseguir y organizar** juegos con la mínima intervención, ya sea con descargas o con megapacks.
5. **Llevarlo todo a Steam**, organizado por saga.
6. **Descubrir** sagas que no conoces y **recorrerlas** siguiendo la ruta que elijas.
7. **Ser tu centro de entretenimiento** (#107): la pantalla principal del modo juego de PlayOS, con tus juegos y sagas **de consola y de PC**, el seguimiento de lo que juegas (§7.4) y el paso fácil a Steam Big Picture. Se maneja completo con mando (#108, §12.5). **Envuelve, no reemplaza:** muestra lo útil para jugar y cada juego lo abre su tienda o lanzador dueño (#130).

## 2. Principios ✅

| # | Principio |
|---|---|
| P1 | **Instalada, con los datos donde tú elijas.** La app se instala con Velopack (en AppData; *en Linux, reemplazado por la #114: flake de Nix y se actualiza con el sistema*). Todo lo pesado o valioso (base de datos, herramientas, caché de imágenes) va en una **carpeta de datos** que eliges tú. *Reemplaza a "Portable" (decisión #55).* |
| P2 | **Las bibliotecas de juegos van separadas del sistema.** Pueden estar en cualquier disco y la app las encuentra. |
| P3 | **Multiplataforma**: Windows hoy y Linux en el futuro. El destino es **PlayOS**, un mod de WorkOS (tu sistema NixOS) orientado a juegos, con Otteretro, Steam y configuraciones de juego, para legioncita (Lenovo Legion 5 Pro, NVIDIA, Hyprland); se diseña pensando en él desde ya y se trabaja después (#103). |
| P4 | **Claude decide y los scripts repiten.** Claude se usa solo para juzgar o la primera vez que aparece un caso. Lo repetitivo pasa a scripts y no gasta tokens (§11). |
| P5 | **Claude se usa a través de tu Claude Code** (`claude -p`), **nunca de la API** de Anthropic. |
| P6 | **Intervención mínima.** La app solo pregunta cuando no puede decidir. Si duda, asigna y marca "revisar". |
| P7 | **No se pierden datos.** Nunca se borran versiones viejas de emuladores ni actualizaciones de juegos. Mover siempre es copiar, verificar y recién entonces borrar. |
| P8 | **Solo enlaces tuyos.** La app nunca busca de dónde descargar juegos. Emuladores, ports y complementos sí los descarga de su fuente oficial. |
| P9 | **La app evoluciona.** Cuando detecta tareas que conviene automatizar, crea issues. Claude Code las implementa en el repositorio y la app se actualiza sola (§11.5). |
| P10 | **Se puede migrar.** Formatear, pasar a Linux o cambiar de PC no hace perder nada (§7.6). |

## 3. Stack y repositorio ✅

- **Backend:** C# (.NET 10) con ASP.NET Core y SQLite (Dapper). Detalle en **[arquitectura.md](arquitectura.md)** (#92 a #99). **Interfaz:** Next.js exportado como estático (React). **Ventana:** Photino, que usa WebView2 en Windows y WebKitGTK en Linux.
- **Idioma y uso:** interfaz en **español**, pensada para escritorio con ratón y teclado. Para la tele se usa Steam Big Picture.
- **Idioma del código** (#125): identificadores en **inglés**; documentación, comentarios, commits, PRs e issues en español. Los nombres de F0 se renombran al inglés.
- **Repositorio:** `D:\dev\personal\otteretro`, con copia en **GitHub, privado**. Ramas: **`dev`**, donde se trabaja, y **`main`**, que es lo publicado.
- **Releases:** al fusionar `dev` en `main`, GitHub Actions compila y publica una release.
- **Instalación y actualización:** con **Velopack**, desde las releases de GitHub. En Windows se instala en `%LocalAppData%\Otteretro`. *En Linux, reemplazado por la #114:* paquete y módulo de Nix, que se actualiza con el sistema. Se actualiza sola y nunca toca la carpeta de datos.

## 4. Consolas ✅

**Incluidas: 23 sistemas.**

| Empresa | Sistemas |
|---|---|
| Nintendo (12) | NES · Game Boy (incluye GBC) · SNES · Virtual Boy · N64 (incluye 64DD) · GBA · GameCube · DS (incluye DSi) · Wii · 3DS (incluye New 3DS) · Wii U · Switch |
| Sony (7) | PS1 · PS2 · PSP · PS3 · PS Vita · PS4 · PS5 (emulación temprana: SharpEmu, KytyPS5) |
| Microsoft (4) | Xbox · Xbox 360 · Xbox One y Series X\|S (estas dos solo se catalogan) |
| **PC** | Versiones de PC oficiales y ports (§6.6) |

**Reglas**
- Los add-ons van dentro de su consola base y las variantes, junto con su consola.
- Las consolas sin emulación también se catalogan.
- El estado de la emulación es un dato que se actualiza, no un valor fijo.

**Fuera: 49 sistemas.** Game & Watch, FDS, Satellaview, Pokémon Mini, Switch 2 y todo lo de Sega, NEC, SNK, Atari, otras marcas, arcade y computadoras retro. Detalle en [historial/v1-por-tema/consolas.md](historial/v1-por-tema/consolas.md).

## 5. Bibliotecas

### 5.1 Tipos ✅

```
%LocalAppData%\Otteretro\       ← la app instalada con Velopack (solo binarios; se actualiza sola)
                                   guarda una sola cosa: dónde está la carpeta de datos

<carpeta de datos>\             ← la ELIGES tú la primera vez que abres la app (ej. E:\Otteretro)
├── data\otteretro.db           ← registro + catálogo
├── cache\imagenes\             ← portadas y arte de SteamGridDB
└── Herramientas\               ← la ÚNICA biblioteca de herramientas
    ├── Emuladores\<Consola>\<Instalación>\<versión>\
    ├── Ports\<Consola>\<Port>\<versión>\
    ├── Complementos\<Emulador o port>\<Complemento>\<versión>\   ← texturas, shaders, ReShade, mods
    └── Sistema\<Consola>\<archivo>\<versión>\                     ← BIOS, keys, firmware

E:\Consolas\  ·  D:\Juegos\…    ← bibliotecas de JUEGOS (las que quieras, en cualquier disco)
├── otteretro-biblioteca.json
├── _Entrada\                   ← descargas y megapacks en proceso
└── <Consola>\<Juego>\…
```

| Tipo | Cuántas | Dónde |
|---|---|---|
| **Herramientas** | **Una sola** | Dentro de la **carpeta de datos** que eliges (supuesto S7). Se puede mover desde la app, igual que una biblioteca. |
| **Juegos** | Las que quieras | En cualquier carpeta o disco |

- **En red** (#136): una biblioteca puede estar en una carpeta de red (SMB/NFS), pensando en el NAS con todas las ROMs (#115). En la ficha, **«traer a este equipo»** copia el juego por la red y lo verifica por hash.

### 5.2 Marcador y registro ✅
- Cada biblioteca de juegos tiene en su raíz un `otteretro-biblioteca.json` con `{id, nombre, tipo, creada, formato}`. El `id` no cambia nunca.
- `data\otteretro.db` guarda el registro de bibliotecas y la última ruta conocida de cada una.

### 5.3 Operaciones ✅
| Operación | Qué hace |
|---|---|
| Crear | Elige una carpeta, escribe el marcador y registra la biblioteca. Las carpetas de cada consola se crean cuando hacen falta. |
| Adoptar | Registra una carpeta con marcador, o una con la estructura antigua. En ese caso crea el marcador y mueve sus emuladores y ports a `Herramientas\`. |
| Renombrar / Quitar | Quitar saca la biblioteca del registro. **Nunca borra archivos.** |
| Reconectar | Si una biblioteca no está en su última ruta, la marca como desconectada y la busca en las raíces de los discos y puntos de montaje (2 niveles), o te deja indicar la ruta. |
| Mover o migrar | Funciona con una biblioteca, una consola, un juego o un emulador o port. Copia, verifica el hash, actualiza la base de datos, **reescribe las rutas internas** y por último borra el origen. |

### 5.4 Rutas ✅
- En la base de datos se guarda **id de biblioteca + ruta relativa**. La ruta absoluta se arma solo al lanzar o exportar un juego.
- Las rutas guardadas dentro de emuladores y ports, como `isoPath` o `ISOPath0`, **se reescriben solas** según la receta de cada uno.
- Los juegos de una biblioteca desconectada aparecen como "no disponibles" y no se exportan.

### 5.5 Migración de lo actual ✅
- `E:\Consolas` se adopta como primera biblioteca de juegos. Sus emuladores (Dolphin, Ryujinx SS4K), su port (Dusklight) y los packs de texturas pasan a `<carpeta de datos>\Herramientas\`.
- `_Herramientas\Catalogo` se integra al repositorio (§11.6).
- Carpetas sobrantes: `E:\Roms`, `E:\Emuladores` y `D:\Roms` están aprobadas para borrar, pero lo tienes que hacer tú, porque Claude Code bloquea los borrados en la raíz de un disco.

### 5.6 Credenciales ✅
- Las claves de Steam Web API, IGDB, MyJDownloader, ScreenScraper, SteamGridDB, RetroAchievements y las tiendas se guardan en `data\`, **cifradas con el almacén seguro del sistema operativo**: DPAPI en Windows, libsecret en Linux.
- **Un solo token de GitHub** sirve para descargar las actualizaciones (releases del repositorio privado) y para escribir en `otteretro-datos` (#68).
- **Bitwarden** es tu **respaldo manual**: la app no lo lee. En un PC nuevo, las claves se vuelven a escribir desde ahí.

## 6. Catálogo

### 6.1 Alcance ✅
- **La mayor cantidad de juegos posible**, siempre con su **saga identificada**. También entran juegos destacados sin saga.
- **También juegos y sagas de PC**, no solo de consolas (#107): las sagas icónicas de PC y todo lo que tienes (#131).
- **Una saga no pertenece a un sistema** y no se repite: sus juegos se asocian a los sistemas, y PC es uno más (#131).
- **La carga es por partes** (scripts y Claude): nunca todas las sagas de PC de golpe, para que termine y no agote la suscripción. PC va después de Microsoft (#69, #131).
- **Todo lo que tienes aparece siempre.**
- Para cada juego se indica **si existe versión de PC**.

### 6.2 Modelo ✅
**Saga → Juego → Versión** (consola o PC; original, remaster o remake) **→ Origen** (un archivo en una biblioteca o tu cuenta en una tienda de PC).
- Los datos se guardan por consola, pero **se muestran, se gestionan y se exportan por saga**.
- Lo que no tiene saga va a "Emulación".

### 6.3 Versiones ✅
- **Todo es una versión del mismo juego**, también los remakes (por ejemplo, Pokémon Ruby y Omega Ruby).
- IGDB clasifica cada versión, Claude la revisa y tú puedes corregirla.
- En pantalla hay una tarjeta por juego, con sus versiones desplegables. Puedes marcar **varias versiones** para Steam.

### 6.4 Mejor versión ✅
Se comparan todas las versiones con cuatro criterios, que se muestran **por separado**: **contenido**, **jugabilidad hoy**, **técnico** y **original**.

### 6.5 Rutas por saga ✅
- Cada saga tiene tres rutas: **orden de lanzamiento**, **orden de la historia** y **ruta recomendada por la comunidad**.
- Las prepara **Claude**, citando fuentes (Reddit, wikis de la saga) que lee con búsqueda web, y tú las apruebas o las editas.
- En cada paso se indica **qué versión recomienda la comunidad** (original o remaster) y por qué.
- **Eliges qué ruta seguir** en cada saga.
- **Progreso**: cada juego puede estar pendiente, en curso, terminado o abandonado. La app te dice **cuál es el siguiente juego** de la ruta que elegiste.

### 6.6 Versión de PC ✅ (lectura de cada tienda 🔬)
- PC es una plataforma más dentro de la saga.
- **Lo que tienes en cualquier tienda de PC cuenta como "lo tengo · versión PC"**, y ese juego no hace falta conseguirlo para un emulador. **Esto reemplaza lo decidido en la #31** ("solo cuenta Steam").
  - **Steam:** se lee con la **Steam Web API** e incluye todo lo comprado, esté instalado o no, y las horas jugadas. Se usa la cuenta **detectada**, y también cuentan los juegos del **préstamo familiar**. 🔬 T1
  - **Epic, Xbox para PC y Battle.net**, que son las tiendas que usas, también cuentan como "lo tengo" y **se pueden lanzar desde la app**. Etapa 1: Steam y Epic, con todo lo que tienes en la cuenta. Etapa 2: Xbox y Battle.net, al principio solo con lo instalado (§13.2, T6). Epic se lee con un método no oficial; si se rompe, se cae a solo lo instalado (#73).
  - **En PlayOS** (#132), lo que tienes se lee de **Steam**, **Heroic** (Epic, GOG, Amazon), **Lutris** (Battle.net, EA, Ubisoft) e **itch.io**; Xbox/Game Pass para PC solo en Torrecita. También cuentan los **juegos sueltos** sin tienda (una carpeta).
  - **Game Pass:** se muestra como **"en Game Pass"**, aparte de "lo tengo", porque depende de la suscripción (supuesto S5).
- **Prioridad:** si tienes la versión de PC oficial, **va primero** (§8.3).
- **Si existe versión de PC pero no la tienes:** se indica que existe, con un enlace a la tienda, si está en Game Pass y si está o estuvo gratis en Epic. 🔬 T2
- Esta información **se actualiza periódicamente** (§11.2).

### 6.7 Icónico, gustos y Descubrir ✅
| Eje | De dónde sale |
|---|---|
| Icónico | Claude junta, con búsqueda web, el consenso de Reddit y ResetEra, rankings, notas y ventas, y lo justifica con fuentes. Tú lo apruebas. No se usa ninguna API de Reddit (#62). |
| Mis gustos | Sagas favoritas, juegos puntuados, horas en Steam y tu progreso en las rutas |
| Descubrir | Claude recomienda por afinidad con lo que te gusta y explica por qué |

### 6.8 Fuentes ✅
| Fuente | Uso |
|---|---|
| IGDB | Base: sagas, versiones, plataformas, ids de Steam |
| ScreenScraper | Identificar lo que no coincide con los DAT, y medios. Con credenciales de desarrollador propias de Otteretro, como ES-DE (#65, #76) |
| libretro-database · Redump | DAT para verificar por hash sin conexión: No-Intro (cartuchos) y Redump (discos) (#76) |
| Wikidata | Relación entre juegos y sagas |
| IGDB · Metacritic / OpenCritic | Notas: la agregada de IGDB; en las sagas icónicas, Claude cita además Metacritic y OpenCritic (#63) |
| HowLongToBeat | Duración. **Opcional**: no tiene API y el scraper puede fallar; entonces se muestra "sin datos" (#66) |
| RAWG | Complemento |
| SteamGridDB | **Imágenes** de la app y de Steam |
| Steam Web API | Lo que tienes y horas jugadas |
| IsThereAnyDeal · GamerPower · catálogo de Game Pass | Disponibilidad en tiendas, precios y juegos gratis (T2) |

Si dos fuentes no coinciden, se usa una **prioridad fija por campo**. Lo que corriges a mano no se sobrescribe nunca.

## 7. Colección

### 7.1 Qué cuenta como "lo tengo" ✅
- Un archivo en una biblioteca de juegos.
- Un juego en cualquiera de tus tiendas de PC (Steam con el préstamo familiar, Epic, Xbox, Battle.net). Cuenta como versión de PC.
- En PlayOS, además, lo que está en **Heroic** (Epic, GOG, Amazon), **Lutris** (Battle.net, EA, Ubisoft) e **itch.io**, y los **juegos sueltos** sin tienda (#132).

### 7.2 Detección ✅
1. Escaneo rápido por nombre y TitleID o serial.
2. Verificación por hash cuando la pides, **sin conexión**: los **cartuchos** con los DAT de No-Intro que publica libretro en GitHub (`libretro-database`) y los **discos** con los DAT de Redump. Ambos se descargan solos (#76). Los hashes quedan guardados.

### 7.3 Actualizaciones y DLC ✅
Se detectan y se muestran BASE, UPD y DLC. **Nunca se borran las versiones viejas.**

### 7.4 Tiempo de juego ✅
*Reemplazado por la #133:* Otteretro lleva el **seguimiento** de cada juego:
- **Tiempo jugado:** el de Steam y, en lo que lanza Otteretro, medido por él.
- **Historial de sesiones** y **última vez** jugado.
- **Estado:** pendiente, jugando, terminado, 100 %, abandonado; con **fechas** de inicio y fin.
- **Logros** de Steam y RetroAchievements; **nota** de 1 a 10 y notas.
- Sin capturas ni estadísticas. Del historial actual se importa lo de Steam (horas y última vez).

### 7.5 Respaldo de datos y partidas ✅
- **Esto reemplaza lo que se había dicho antes** ("las partidas no tienen copia de seguridad").
- **Dónde:** en un repositorio **privado** de GitHub, `otteretro-datos`. Las partidas de más de 50 MB van con Git LFS, con **presupuesto 0**: si se supera la cuota gratis (10 GiB), LFS se bloquea hasta el mes siguiente y la app avisa (#70).
- **Qué se respalda:**

  | Qué | Cuándo |
  |---|---|
  | Base de datos de la app, exportada como texto (JSON por tabla) | Cuando cambia algo y al cerrar la app |
  | Configuración de emuladores y ports | **Cada vez que cambia** |
  | Partidas guardadas | **Al cerrar el juego o el emulador** |
  | BIOS, keys y firmware | Cuando cambian (siempre en repositorio privado) |

- **No** se respaldan los packs de texturas, los binarios de emuladores ni los juegos: se vuelven a conseguir, los emuladores con sus recetas (#78). Alguna versión vieja de un emulador podría no estar ya disponible.
- Antes de actualizar un emulador o un port se sigue haciendo además una copia puntual (§8.4).
- Para saber cuándo se cierra un juego, **todo se lanza a través de Otteretro** (§10).

### 7.6 Migración entre equipos ✅
- Es probable que formatees, que pases a Linux o que tengas otro PC. **Todo tiene que poder moverse de un equipo a otro**:
  - Se reinstala la app con Velopack y se le indica la carpeta de datos que ya existe (P1).
  - Los datos y las partidas se recuperan desde el repositorio de respaldo (§7.5).
  - Las credenciales se recuperan según §5.6.
- Lo que depende de cada equipo, como la exportación a Steam o las rutas de Steam, se guarda **por equipo**. La app reconoce en qué PC está.
- **Un solo PC activo a la vez** (#77). Si aparece un conflicto en el respaldo, gana lo más reciente y lo otro se guarda aparte, sin perderlo.

## 8. Emuladores, ports y complementos

### 8.1 Instalación ✅
- La app los **descarga de su fuente oficial** y también **detecta** los que instalas a mano.
- Cada emulador, port y complemento tiene una **receta** en el repositorio (JSON) que indica:
  - de dónde se descarga,
  - cómo se deja en modo portable,
  - dónde guarda partidas y configuración,
  - qué rutas internas tiene,
  - qué pasos extra necesita (los ejecuta la app),
  - qué formato de juego acepta o exige.

- **Recetas compartidas** (#135): la misma receta lleva, por versión y por sistema, la URL y el hash. En Windows la app descarga con ella; en PlayOS, Nix arma con ella los paquetes que no están en nixpkgs, y **todas las versiones** de la receta quedan instaladas. Lo de base lo deciden las recetas por consola, pero se puede elegir por juego otra variante (por ejemplo, un emulador con packs de texturas o ajustado para el rendimiento de un juego).

### 8.2 Ubicación ✅
Todo va en `<carpeta de datos>\Herramientas\` (§5.1). Los **complementos** (packs de texturas, shaders, ReShade, mods) van junto a su emulador o port y se **instalan y actualizan igual que ellos**.

### 8.3 Asignación ✅
- Cada consola tiene un emulador **predeterminado**. Cada juego puede usar un emulador o port **específico**, con su propia configuración.
- Puede haber **varias instalaciones** del mismo emulador.
- **Varios emuladores por consola** (#72): uno predeterminado y otros alternativos instalables. La referencia son las recomendaciones de **GordoFriki**, que Claude revisa al escribir las recetas. **Switch:** Eden (predeterminado), Ryubing, Citron y Ryujinx SS4K.
- **Regla automática** para elegir cómo se juega:
  1. **Versión de PC oficial que tienes.**
  2. Port al 100 %.
  3. Emulador perfecto.
  4. Lo más jugable.
- Puedes cambiar la elección a mano en cada juego, y los valores que usa la regla son editables.

### 8.4 Actualización ✅
- La actualización la lanzas tú. La app **avisa** qué emuladores, ports y complementos tienen versión nueva, y tú eliges qué instalar.
- La versión nueva se instala **al lado** de la anterior. **La vieja nunca se reemplaza** y puedes volver a ella.
- **Migración:** antes de actualizar se hace una copia de seguridad puntual. Después se copian las partidas y la configuración según la receta. Cómo guarda sus partidas cada port se investiga al escribir su receta.

### 8.5 Archivos de sistema ✅
- Las BIOS, keys y firmware **los subes desde la interfaz**. La app no los descarga.
- Quedan en `Herramientas\Sistema\`, compartidos por todos los emuladores, **con versiones**.
- La app indica dónde va cada uno, comprueba que estén y avisa si falta alguno.

### 8.6 Contenido instalado dentro del emulador ✅
- Actualizaciones y DLC de Switch, juegos de Wii U (Cemu), paquetes `.pkg` de PS3 y `.cia` de 3DS: **la app los instala sola** en el emulador, según la receta. Los archivos originales siguen en la biblioteca.
- **PS4:** shadPS4 ya no instala `.pkg`, así que la ingesta lo **extrae** con una herramienta externa (receta) y guarda el juego extraído (#71).
- Si no lo consigue, **avisa de qué hay que instalar a mano**.

### 8.7 Herramientas que gestiona la app ✅
- **Steam ROM Manager** es una herramienta más: tiene su receta y vive en `Herramientas\`.
- **RetroAchievements**: inicias sesión y la app muestra los logros de cada juego y los activa en los emuladores compatibles.

### 8.8 Mandos ✅
*Reemplazado por la #108:* Otteretro se maneja **completo con mando** sin cambiar la distribución de la interfaz (Xbox, adaptador inalámbrico de Xbox, DualSense/DualShock 4, Switch Pro/Joy-Con). Dentro de los juegos los mandos los sigue gestionando Steam Input o cada emulador.

### 8.9 Linux ✅
*Reemplazado por la #113 y la #114:* Linux es **PlayOS** (§14). Ahí Otteretro se instala con su flake y se actualiza con el sistema, y los emuladores vienen **instalados de base** (nixpkgs y lo que no esté, gestionado aparte). Las recetas siguen teniendo una variante por sistema operativo.

## 9. Ingesta (descargas y megapacks)

### 9.1 Proceso único ✅
```
Origen ─► Extraer ─► Identificar ─► Convertir ─► Ubicar ─► Marcar "lo tengo"
 · enlaces (JDownloader)   · megapack o carpeta local
```

### 9.2 JDownloader ✅
- Se conecta por la **API de MyJDownloader**.
- Los enlaces se pegan en la ficha de un juego o se importan como lista. Para una lista, primero un script los asocia a sus juegos y Claude resuelve solo los que el script no pudo.
- Eliges la biblioteca de destino viendo los **GB libres**. La descarga, la descompresión y la organización se hacen ahí.

### 9.3 Megapacks ✅
- **Se mueven** los archivos después de verificarlos, así el pack se va vaciando y no se duplica espacio.
- **Vista previa:** la app muestra lo que identificó, todo marcado por defecto, con filtros por saga, consola o "está en el catálogo", y tú eliges qué importar.
- **Regiones:** se importa una versión por juego (1G1R). El orden de regiones es configurable y por defecto **va primero la que tiene español**.
- **Extras:** solo se importan **traducciones de fans**. No se importan hacks, betas, demos ni juegos sin licencia.
- **Duplicados:** si ya tienes el juego, se salta, **salvo que el nuevo esté verificado y el tuyo no**. En ese caso se reemplaza.

### 9.4 Identificación ✅
Se prueba en este orden:
1. El origen, por ejemplo la ficha donde pegaste el enlace.
2. El nombre y el TitleID o serial.
3. El hash contra los DAT (No-Intro de libretro y Redump). Si no coincide, se consulta ScreenScraper.
4. Los alias aprendidos.
5. **Claude**: si no está seguro, **asigna el juego y lo marca "revisar"**. Hay una lista de pendientes de confirmar.
6. Si Claude tampoco puede proponer nada, el archivo va a "Sin identificar" y la app avisa.

### 9.5 Formatos ✅
- La regla general es **el formato más liviano**: se descomprimen zip, 7z y rar; GameCube y Wii pasan a RVZ; PS1 y PS2 pasan a CHD.
- **Lo que pide el port manda sobre el emulador.** Si un port exige el formato original, se guarda **solo el original**, sin recortes, y esa única copia la usan el port y el emulador. Si lo que tienes es RVZ o CHD, se regenera el ISO completo.
- El comprimido se borra después de verificar.

## 10. Steam ✅

| Tema | Decisión |
|---|---|
| Herramienta | Steam ROM Manager, a partir de manifiestos "Manual" que genera Otteretro. El arte sale de SteamGridDB. |
| Quién lanza | **El dueño del juego** (#130): el botón Jugar de Otteretro abre por Steam (`steam://rungameid/…`) lo que está en Steam, incluido lo exportado; lo de Heroic, Lutris o itch, con su lanzador |
| Lanzador | Cada acceso de Steam abre **`otteretro jugar <id>`**, no el emulador directamente. Así Otteretro sabe cuándo se cierra el juego, respalda las partidas (§7.5) e instala lo que haga falta antes de abrirlo. El botón "Jugar" de la app usa el mismo camino. |
| Cuenta | La detectada (`loginusers.vdf`) |
| Qué se exporta | La opción elegida (port o emulador) de cada versión marcada. Puede haber varias versiones del mismo juego. |
| Juegos que ya están en Steam | **No se exportan.** Solo cuentan como "lo tengo". |
| Categorías | **Por saga**. Lo que no tiene saga va a **"Emulación"**. |
| Nombre | "Juego (Consola)", con el mismo nombre que en la app: el oficial en español si existe (#88, #89) |

## 11. Automatización y Claude

### 11.1 Claude decide y los scripts repiten ✅
- **Claude** se encarga de lo que requiere **criterio**: qué es icónico, la mejor versión, las rutas, Descubrir, los casos ambiguos y la primera receta de cada emulador o port.
- **Los scripts**, en C# dentro de la app, hacen lo **repetitivo y determinista**. Se usan desde la interfaz o desde la línea de comandos: `otteretro <tarea>`.
- **El script va siempre primero.** Claude entra solo cuando el script no puede resolver algo.
- **Aprendizaje automático:** lo que resuelve Claude se guarda solo como dato (un alias, una regla, una receta o una ruta). Hay un **registro** donde puedes revisarlo o deshacerlo.
- **Mientras la app funciona, Claude solo genera datos**, nunca código. El código nuevo entra por el repositorio (§11.5).

| Función | Script (sin tokens) | Claude (solo cuando hace falta) |
|---|---|---|
| Ports nuevos | Descarga las fuentes y calcula qué cambió | Clasifica lo que no encaja en ningún patrón |
| Compatibilidad de emuladores | Todo | — |
| Identificar juegos | TitleID, hash, DAT, alias | Casos ambiguos, que se guardan como alias |
| Enlaces de una lista | Coincidencia aproximada por nombre y alias | Lo que no coincide, que se guarda como alias |
| Recetas | Instalar, actualizar, migrar | Escribir la primera receta |
| Icónico, mejor versión, rutas | Reunir notas, ventas y datos | Ponderar y justificar; el resultado se guarda |
| Descubrir | Leer gustos, horas y progreso | Recomendar; el resultado se guarda |
| Tiendas y versión de PC | Todo 🔬 | — |

### 11.2 Cuándo se ejecuta ✅
- **Actualizaciones** de emuladores, ports y complementos: las lanzas tú, porque pueden implicar migrar partidas. Una vez lanzadas, todo es automático.
- **Resultados que requieren criterio** (icónico, rutas, mejor versión, Descubrir): se recalculan **cuando cambian los datos de los que dependen**, algo que detecta un script, y **cuando lo pides**.
- **Información de tiendas y versión de PC, precios y versiones nuevas**: se comprueban **solo con la app abierta**, al abrirla y cada cierto intervalo configurable. Con la app cerrada no corre nada; los avisos (como una bajada de precio) llegan la próxima vez que la abras (#75).
- **Bibliotecas**: se escanean **solo cuando pulsas "Escanear"**. La app no vigila las carpetas.
- **Carga inicial del catálogo**: **completa**, todas las sagas y juegos, en orden **Nintendo → Sony → Microsoft** (#69). Los scripts bajan primero todos los datos de las APIs. Claude solo juzga, **por lotes de varias sagas por llamada**, para gastar los mínimos tokens.
- **Tope de Claude**: la carga inicial no tiene tope propio, pero **dura días** por los límites de la suscripción (ventanas de 5 h y semanales): la cola se pausa al llegar al límite y **continúa sola** cuando se renueva. Después, cada ejecución tiene un tope configurable de tareas: al llegar a él, la app se pausa y avisa.
- **Descripciones**: se **traducen al español con Claude una sola vez** y quedan guardadas.

### 11.3 Claude Code ✅
Otteretro ejecuta `claude -p` con prompts guardados en el repositorio. Cada prompt fija qué puede tocar Claude, cuántos cambios puede hacer como máximo y que debe citar fuentes.

### 11.4 Registro de uso de Claude ✅
- Cada vez que se usa Claude se registra **la tarea, la duración y el resultado**.
- Cuando una tarea se repite, la app **avisa que conviene automatizarla** (§11.5).

### 11.5 La app evoluciona ✅
```
App detecta una tarea repetida ─► crea un GitHub Issue documentado (gh, etiqueta "automatizar")
  ─► tú lo ejecutas con Claude Code en la rama dev ─► se fusiona dev en main
  ─► GitHub Actions publica una release ─► la app se actualiza sola
```
Así, cada tarea que Claude resolvía pasa a un script y la app depende cada vez menos de los tokens.

### 11.6 Generador actual ✅
`E:\Consolas\_Herramientas\Catalogo` (Node, unas 350 líneas) se integra **portándolo a C#**, como scripts de la app.

## 12. Interfaz ✅

### 12.1 Proceso
- **Primero se diseña y después se programa.** Se diseña en **Claude Design**: primero el **sistema de diseño** (colores, tipografía, componentes) y luego cada pantalla. Tú lo apruebas y recién entonces se programa el front.
- **Sistema de diseño** (aprobado, #91): https://claude.ai/artifact/DMH4KuU2kkAnb3ApFTCiuu
- **Pantallas** (prototipo navegable, aprobado, #91): https://claude.ai/artifact/Bo8gtfiPdRAumFhkFikHPV
- La **especificación de pantallas** está en **[pantallas.md](pantallas.md)**: qué muestra cada una, qué acciones tiene y sus estados. Sus decisiones (#79 a #90) se cerraron en la encuesta 9.

### 12.2 Aspecto
| Tema | Decisión |
|---|---|
| Estilo | Oscuro, con portadas grandes. Referencias: **la biblioteca de Steam y Playnite** |
| Densidad | **Mixta**: portadas medianas con los datos clave a la vista (estado, consola, versión) |
| Acento | **Se adapta al color de la portada** de la saga o el juego que estás viendo |
| Identidad | **Logo y una pequeña mascota: una nutria retro**: icono, pantallas vacías, avisos, animación de carga, cuando Claude trabaja y al terminar un juego o una ruta (#90) |
| Imágenes | SteamGridDB |

### 12.3 Navegación
- **Barra lateral izquierda** con las pantallas principales: **Inicio, Sagas, Colección y Descubrir**.
- Abajo, una sección secundaria: **Descargas, Herramientas, Steam y Ajustes**.
- **Panel de tareas en segundo plano** (escaneos, descargas, Claude, respaldos) con su progreso, y una **notificación del sistema** cuando terminan.

### 12.4 Pantallas
> Se rediseñan en **D1** para el centro de entretenimiento (Inicio con consola y PC, seguimiento, mando) (#138); hasta entonces vale esta tabla.

| Pantalla | Contenido |
|---|---|
| **Inicio** | El siguiente juego de cada ruta que sigues · lo pendiente de revisar (identificaciones dudosas, sin identificar) · novedades (ports nuevos, cambios de compatibilidad, versiones nuevas de emuladores) · descargas y tareas en curso |
| **Sagas** | Cuadrícula de sagas. Al entrar en una saga: **línea de tiempo horizontal** con las portadas de la ruta elegida, un selector de ruta y tu progreso marcado en cada juego |
| **Colección** | **Por consola**, con filtros (lo tengo, me falta, verificado, revisar) y orden por nombre, saga o año |
| **Descubrir** | Sagas recomendadas por afinidad, cada una con su porqué |
| **Ficha de juego** | **A la vista:** cómo jugarlo (opción elegida, botón Jugar, cambiar de opción), versiones y comparativa con los 4 criterios, archivos (dónde están, BASE/UPD/DLC, si están verificados), y tu progreso, puntuación y notas. **En secciones desplegables o pestañas secundarias:** logros de RetroAchievements, tiempo jugado, disponibilidad en tiendas, conseguirlo (enlaces o megapack) y datos con sus fuentes |
| Descargas · Herramientas · Steam · Ajustes | Secundarias. Su contenido se detalla en la especificación de pantallas |

### 12.5 Mando (#108, #134)
- **La misma interfaz**, con navegación espacial: la cruceta salta entre elementos, el foco se ve claro y abajo aparece una barra con los botones (A elegir, B volver, X buscar, Menú). Con el ratón, foco y barra se ocultan.
- **Escribir:** el teclado en pantalla de Steam; fuera de la sesión de Steam, uno propio.
- **Botón Guía:** menú rápido de Otteretro (volver a Otteretro, ir a Steam, cerrar el juego, capturar). En la sesión de Steam ese botón es de Steam, así que se usa una combinación (por ejemplo Guía + A).
- Desde F1, **cada pantalla nueva se hace navegable con mando** (#138).

## 13. Pendientes

### 13.1 Decisiones del usuario
**No queda ninguna.** La encuesta 6 ([Interfaz y cierre](https://claude.ai/artifact/GHKEE7GuPTgWjKGopbKHwN)) cerró las que quedaban.

### 13.2 Pendientes técnicos 🔬
**Resueltos** con la investigación ([investigacion.md](investigacion.md)) y la encuesta 7, **[Decisiones técnicas](https://claude.ai/artifact/Jt8wyddvjggYVypLwgruWz)**. Lo marcado con 🔬 se comprueba en el primer prototipo.

| # | Decisión |
|---|---|
| T1 | **Préstamo familiar con SteamKit2.** Inicias sesión **una vez**, con Steam Guard. Queda un token que dura meses, se renueva solo y se guarda cifrado. Después todo es automático. 🔬 Hay que confirmar que esa sesión no cierra la del cliente de Steam mientras juegas. |
| T2 | **Las tres fuentes:** IsThereAnyDeal, GamerPower y el catálogo de Game Pass, que no es oficial. Se muestra en qué tiendas está el juego, el precio actual, el **mínimo histórico**, si está o estuvo gratis, si está en Game Pass, y llega un **aviso cuando baja de precio**: de los juegos de las sagas que sigues que no tienes y de los marcados "seguir precio", al llegar al mínimo, quedar gratis o tener un 40 % o más de descuento (#84). |
| T3 / T4 | Dónde guarda las partidas cada port y qué formatos acepta **se averigua al instalar ese port por primera vez**. Dusklight ya está resuelto: acepta RVZ. |
| T5 | **Velopack**, aunque la app deje de ser portable. Lo pesado va en la **carpeta de datos** que eliges (P1, §5.1). |
| T6 | Tiendas que usas: **Steam, Epic, Xbox y Battle.net**. Etapa 1: Steam y Epic, con todo lo que tienes en la cuenta. Etapa 2: Xbox y Battle.net, al principio solo con lo instalado. No se integran GOG, EA, Ubisoft ni Amazon. |
| T7 | **Sin procesos vigilando de fondo.** Ver el detalle debajo. |

**T7 · Cómo se sabe que terminaste de jugar, sin gastar recursos**
- `otteretro jugar` **solo existe mientras juegas**. Cuando no hay ningún juego abierto, no corre nada.
- **Emuladores y ports:** como ES-DE, se espera a que termine **el proceso que se lanzó** (#61).
  - En Windows, `WaitForSingleObject`; en Linux, `pidfd` o `waitpid`. Esa espera está garantizada y no consume CPU.
  - Si ese proceso termina dejando otros vivos (un lanzador que abre el juego y se cierra), se sigue el Job Object y, como red de seguridad, se cuentan sus procesos cada 60 s. Microsoft no garantiza que llegue el aviso `ACTIVE_PROCESS_ZERO`.
- **Juegos de tienda:** se lanzan con su enlace, por ejemplo `steam://`.
  - Al arrancar, se busca su ejecutable **durante los primeros segundos y nada más**.
  - Cuando aparece, se espera su cierre igual que en el caso anterior.
- **Al cerrar el juego** se deja la tarea "respaldar partidas" en una **cola** persistente, guardada en SQLite.
  - El respaldo se hace **en segundo plano, sin avisarte**.
  - Si falla, lo ves en el panel de tareas.
  - Si la app no está abierta, la tarea espera en la cola hasta la próxima vez que se abra.

### 13.2.1 Revisión de viabilidad ✅
[viabilidad.md](viabilidad.md) revisó todo este documento. Sus ajustes V1 a V6 se aprobaron en la **[encuesta 8](https://claude.ai/artifact/REx65yraTo9AMN7tW6UCZa)** (decisiones #61 a #74) y ya están incorporados arriba.

### 13.3 Acciones tuyas fuera de la app
No son requisitos, pero siguen pendientes:

| # | Acción |
|---|---|
| A1 | Borrar `E:\Roms`, `E:\Emuladores` y `D:\Roms`. Están aprobadas, pero Claude Code no puede borrarlas. |
| A2 | Poner `prod.keys` y el firmware en Ryujinx SS4K. Hoy no arranca ningún juego de Switch. Cuando exista, la app lo hará desde `Herramientas\Sistema\` (§8.5). |
| A3 | Crear las cuentas y claves cuando toque: Steam Web API, IGDB (Twitch), MyJDownloader, ScreenScraper, SteamGridDB, RetroAchievements y las tiendas de PC. |

### 13.4 Supuestos que tomé sin preguntarte
**Confirmados** en la encuesta 8 (#67):

| # | Supuesto |
|---|---|
| S1 | El generador `_Herramientas\Catalogo` se porta a C#. Lo deduje de que elegiste scripts en C# dentro de la app (#36). |
| S2 | En 1G1R solo dijiste "español primero". El resto del orden por defecto es Europa > USA > Japón, y se puede cambiar en Ajustes. |
| S3 | La base de datos se respalda como JSON por tabla, para que git guarde solo los cambios entre copias. |
| S4 | Todo el respaldo va a **un solo repositorio privado**, `otteretro-datos`, con una carpeta por tipo. Si quieres separar las BIOS y keys en otro repositorio, dímelo. |
| S5 | **Game Pass** se muestra como "en Game Pass", no como "lo tengo", porque depende de tener la suscripción activa. |
| S6 | Para saber cuándo se cierra un juego y respaldar sus partidas, los accesos de Steam lanzan **`otteretro jugar <id>`** y no el emulador directamente (§10). |
| S7 | Dijiste que lo pesado debe ir donde tú elijas. Puse **todo lo que no es la app** en la misma carpeta de datos: la base de datos, `Herramientas\` y la caché de imágenes. Así migrar de equipo es mover una sola carpeta. La base de datos es liviana y podría quedarse en AppData, pero entonces habría dos sitios que respaldar. |

**Pendiente de confirmar:**

| # | Supuesto |
|---|---|
| S8 | En Descubrir, lo que marcas "no me interesa" vuelve a poder recomendarse a los **6 meses** (configurable en Ajustes). Dijiste "después de un tiempo" sin fijar cuánto (#85). |

### 13.5 Siguiente paso
Orden decidido (#74): **especificación de pantallas → Claude Design** (sistema de diseño y pantallas) **→ arquitectura → plan por fases → código**. Hechos: pantallas, Claude Design (#91), arquitectura (#92 a #99), **[plan por fases](plan.md)** (#100, reordenado por la #138) y F0. Siguiente: **P0 · PlayOS y Linux** (#137), después **D1 · Diseño** y F1.

## 14. PlayOS (#103 a #117)

Mod público de WorkOS (tu NixOS) para juegos, en el repo `playos`, que importa `workos` como base (#104).

| Tema | Decisión |
|---|---|
| Sesiones | Al iniciar sesión eliges **Play** o **Work** (Hyprland); en Work los juegos son apps normales (#105). **Play es la sesión gamescope de Steam** y Otteretro arranca encima a pantalla completa; Big Picture está a un botón y Otteretro sigue detrás (#129) |
| Máquinas | legioncita; a futuro un PC para la tele y una portátil de mano; una VM de pruebas (#106) |
| GPU de legioncita | Se usa la configuración que funciona hoy, **comprobada en el equipo** (#109) 🔬 |
| Rendimiento | GameMode, gamescope, MangoHud, perfil de rendimiento automático, LACT; kernel por defecto; LenovoLegionLinux y OpenRGB (#110) |
| Steam y tiendas | Steam (gamescope, Proton-GE, Remote Play y transferencia local, extest, Steam ROM Manager), Heroic, Lutris, Bottles, itch.io; Discord (Vesktop), OBS, gpu-screen-recorder (#111) |
| Sin soporte en Linux | Se juega en Torrecita por streaming (Moonlight) y Otteretro lo marca (#112) |
| Emuladores y ports | Todo instalado de base (#113): nixpkgs y, lo que no esté, armado por Nix desde las **recetas compartidas** de Otteretro, con todas sus versiones (#135); configuración y partidas, de Otteretro |
| Otteretro | Flake propio, se actualiza con el sistema, datos en `~/Otteretro`, claves en libsecret, GameMode/MangoHud/NVIDIA automáticos (#114, #116) |
| Juegos | SSD interno, USB y copiar desde Torrecita; a futuro un NAS con todas las ROMs (#115) |
| Cómo se trabaja | Fase P0 antes de F1; pruebo por `ssh legioncita` y el rebuild lo haces tú (#117) |

## 15. Metodología de trabajo con IA (#118 a #127)

Cómo se trabaja en Otteretro y en tus demás repos personales. Va en un plugin propio de Claude Code, **`otter`** (repo privado `OscarVargas97/claude-skills`). Parte de las skills de `mynu-ai` y del workspace del LMS, pero es independiente: no se sincroniza con ellos y **no toca nada de Multiplex** (#118, #119).

| Tema | Decisión |
|---|---|
| Requerimientos | Las encuestas levantan los requerimientos y de ahí salen las tareas. Skill `requerimientos`: idea → encuesta → decisiones y requisitos → **una solicitud por tema** con sus sub-issues, creados cuando dices que sí. Criterios de aceptación derivados de las respuestas; lo que no queda claro y las preguntas que aparecen al implementar van a una mini-encuesta. Selector para 1 a 3 preguntas, artifact para más (#121) |
| Tareas en GitHub | Solicitud = issue `solicitud`; tareas = sub-issues; estado en un GitHub Project (Pendiente → En desarrollo → En revisión → Hecho, Descartado); etiquetas de tipo y prioridad, `solicitud` y `automatizar`; un milestone por fase; se identifica con el número del issue. Sin Notion (#120) |
| Decisiones y plan | Las decisiones siguen en `historial/decisiones.md` y los issues las citan. El estado de las tareas vive en issues y milestones; `plan-implementacion.md` queda con el detalle técnico de cada fase, sin marcar avance (#122) |
| Reglas para la IA | `AGENTS.md` (y `CLAUDE.md` que lo importa), adaptaciones por repo, `work/<tarea>/ESTADO.md` para retomar entre sesiones, estado al iniciar sesión, lint después de editar, worktrees con las reglas del LMS, `docs/README.md` como índice, `docs/review-checklist.md`, mapa funcional y badge de la skill (#123) |
| Guardián | En el plugin, basado en el del LMS, con reglas por repo. En Otteretro bloquea o pide confirmación según la lista de #124. Cambios sin commitear o sin pushear: **avisa, no bloquea** (#124) |
| Workspace | `otteretro-workspace` en `D:\dev\personal\otteretro` agrupa Otteretro y PlayOS en `repos/<repo>/<rama>`, con `work/`, la configuración del guardián y lo que cruza repos (#128) |
| Idioma | Código en inglés; el resto en español (#125, §3) |
| Skills | `requerimientos`, `issue-solver`, `pr-reviewer` (revisa tus PRs antes de fusionar en `dev`), `repo-docs`, docs entre repos, `fase` y `estado`. Un paso a la vez; commit, push y PR solo cuando los pides (#126) |
| Puesta en marcha | Primero el plugin; después se aplica a Otteretro en una rama propia, con milestones de todas las fases e issues de la fase en curso. Se instala a nivel usuario (#127) |

Otteretro adoptó el plugin el 2026-10-16 (rama `chore/metodologia`) y se trabaja desde un workspace con PlayOS (#128): reglas en `AGENTS.md` del repo y del workspace, configuración en el `.claude/otter.json` del workspace, tareas en el [Project otteretro](https://github.com/users/OscarVargas97/projects/2) y un milestone por fase.
