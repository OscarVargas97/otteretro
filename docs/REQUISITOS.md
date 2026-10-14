# Otteretro: requisitos consolidados

> **Este es el único documento vigente.** Si algo de aquí contradice el historial, manda este documento.
> **Última actualización:** 2026-10-14, después de la encuesta 6, "Interfaz y cierre".

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

Otteretro es una app **personal y portable** para:

1. **Catalogar** las sagas icónicas y los juegos interesantes de las consolas elegidas, desde lo retro hasta la generación de PS5 y Switch, incluida la versión de PC.
2. **Saber qué tienes**: archivos en tus bibliotecas y juegos en tu cuenta de Steam.
3. **Elegir cómo jugar** cada juego: versión de PC, port o emulador, y cuál es la mejor versión.
4. **Conseguir y organizar** juegos con la mínima intervención, ya sea con descargas o con megapacks.
5. **Llevarlo todo a Steam**, organizado por saga.
6. **Descubrir** sagas que no conoces y **recorrerlas** siguiendo la ruta que elijas.

## 2. Principios ✅

| # | Principio |
|---|---|
| P1 | **Portable.** La app vive en su carpeta, junto con su base de datos (`data\`) y sus herramientas (`Herramientas\`). No escribe nada en AppData. |
| P2 | **Las bibliotecas de juegos van separadas del sistema.** Pueden estar en cualquier disco y la app las encuentra. |
| P3 | **Multiplataforma**: Windows hoy y Linux en el futuro. |
| P4 | **Claude decide y los scripts repiten.** Claude se usa solo para juzgar o la primera vez que aparece un caso. Lo repetitivo pasa a scripts y no gasta tokens (§11). |
| P5 | **Claude se usa a través de tu Claude Code** (`claude -p`), **nunca de la API** de Anthropic. |
| P6 | **Intervención mínima.** La app solo pregunta cuando no puede decidir. Si duda, asigna y marca "revisar". |
| P7 | **No se pierden datos.** Nunca se borran versiones viejas de emuladores ni actualizaciones de juegos. Mover siempre es copiar, verificar y recién entonces borrar. |
| P8 | **Solo enlaces tuyos.** La app nunca busca de dónde descargar juegos. Emuladores, ports y complementos sí los descarga de su fuente oficial. |
| P9 | **La app evoluciona.** Cuando detecta tareas que conviene automatizar, crea issues. Claude Code las implementa en el repositorio y la app se actualiza sola (§11.5). |
| P10 | **Se puede migrar.** Formatear, pasar a Linux o cambiar de PC no hace perder nada (§7.6). |

## 3. Stack y repositorio ✅

- **Backend:** C# con ASP.NET Core y SQLite. **Interfaz:** Next.js exportado como estático (React). **Ventana:** Photino, que usa WebView2 en Windows y WebKitGTK en Linux.
- **Idioma y uso:** interfaz en **español**, pensada para escritorio con ratón y teclado. Para la tele se usa Steam Big Picture.
- **Repositorio:** `D:\dev\personal\otteretro`, con copia en **GitHub, privado**. Ramas: **`dev`**, donde se trabaja, y **`main`**, que es lo publicado.
- **Releases:** al fusionar `dev` en `main`, GitHub Actions compila y publica una release.
- **Autoactualización:** la app detecta una release nueva, la descarga y se reemplaza a sí misma **sin tocar `data\` ni `Herramientas\`**. No hace falta reinstalar.

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
Otteretro\                      ← app portable (cualquier ruta)
├── Otteretro(.exe) …           ← se reemplaza al actualizar
├── data\otteretro.db           ← registro + catálogo (no se toca al actualizar)
└── Herramientas\               ← la ÚNICA biblioteca de herramientas (no se toca al actualizar)
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
| **Herramientas** | **Una sola** | **Dentro de la carpeta de Otteretro.** Si mueves la app, se mueve todo con ella. |
| **Juegos** | Las que quieras | En cualquier carpeta o disco |

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
- `E:\Consolas` se adopta como primera biblioteca de juegos. Sus emuladores (Dolphin, Ryujinx SS4K), su port (Dusklight) y los packs de texturas pasan a `Otteretro\Herramientas\`.
- `_Herramientas\Catalogo` se integra al repositorio (§11.6).
- Carpetas sobrantes: `E:\Roms`, `E:\Emuladores` y `D:\Roms` están aprobadas para borrar, pero lo tienes que hacer tú, porque Claude Code bloquea los borrados en la raíz de un disco.

### 5.6 Credenciales ✅
- Las claves de Steam Web API, IGDB, MyJDownloader, ScreenScraper, SteamGridDB, RetroAchievements y las tiendas se guardan en `data\`, **cifradas con el almacén seguro del sistema operativo**: DPAPI en Windows, libsecret en Linux.
- **Bitwarden** es tu **respaldo manual**: la app no lo lee. En un PC nuevo, las claves se vuelven a escribir desde ahí.

## 6. Catálogo

### 6.1 Alcance ✅
- **La mayor cantidad de juegos posible**, siempre con su **saga identificada**. También entran juegos destacados sin saga.
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
- Las prepara **Claude**, citando fuentes (Reddit, wikis de la saga), y tú las apruebas o las editas.
- En cada paso se indica **qué versión recomienda la comunidad** (original o remaster) y por qué.
- **Eliges qué ruta seguir** en cada saga.
- **Progreso**: cada juego puede estar pendiente, en curso, terminado o abandonado. La app te dice **cuál es el siguiente juego** de la ruta que elegiste.

### 6.6 Versión de PC ✅ (lectura de cada tienda 🔬)
- PC es una plataforma más dentro de la saga.
- **Lo que tienes en cualquier tienda de PC cuenta como "lo tengo · versión PC"**, y ese juego no hace falta conseguirlo para un emulador. **Esto reemplaza lo decidido en la #31** ("solo cuenta Steam").
  - **Steam:** se lee con la **Steam Web API** e incluye todo lo comprado, esté instalado o no, y las horas jugadas. Se usa la cuenta **detectada**, y también cuentan los juegos del **préstamo familiar**. 🔬 T1
  - **Epic, GOG, Xbox para PC, EA app y Ubisoft Connect:** también cuentan como "lo tengo" y **se pueden lanzar desde la app**. 🔬 T6
  - **Game Pass:** se muestra como **"en Game Pass"**, aparte de "lo tengo", porque depende de la suscripción (supuesto S5).
- **Prioridad:** si tienes la versión de PC oficial, **va primero** (§8.3).
- **Si existe versión de PC pero no la tienes:** se indica que existe, con un enlace a la tienda, si está en Game Pass y si está o estuvo gratis en Epic. 🔬 T2
- Esta información **se actualiza periódicamente** (§11.2).

### 6.7 Icónico, gustos y Descubrir ✅
| Eje | De dónde sale |
|---|---|
| Icónico | Claude junta el consenso de Reddit, ResetEra, rankings, notas (OpenCritic/Metacritic) y ventas, y lo justifica con fuentes. Tú lo apruebas. |
| Mis gustos | Sagas favoritas, juegos puntuados, horas en Steam y tu progreso en las rutas |
| Descubrir | Claude recomienda por afinidad con lo que te gusta y explica por qué |

### 6.8 Fuentes ✅
| Fuente | Uso |
|---|---|
| IGDB | Base: sagas, versiones, plataformas, ids de Steam |
| ScreenScraper | Identificación por hash y medios |
| Wikidata | Relación entre juegos y sagas |
| OpenCritic / Metacritic | Notas |
| HowLongToBeat | Duración |
| RAWG | Complemento |
| SteamGridDB | **Imágenes** de la app y de Steam |
| Steam Web API | Lo que tienes y horas jugadas |
| Disponibilidad en tiendas | Game Pass, juegos gratis de Epic y otras tiendas (fuente por definir) 🔬 |

Si dos fuentes no coinciden, se usa una **prioridad fija por campo**. Lo que corriges a mano no se sobrescribe nunca.

## 7. Colección

### 7.1 Qué cuenta como "lo tengo" ✅
- Un archivo en una biblioteca de juegos.
- Un juego en cualquiera de tus tiendas de PC (Steam con el préstamo familiar, Epic, GOG, Xbox, EA, Ubisoft). Cuenta como versión de PC.

### 7.2 Detección ✅
1. Escaneo rápido por nombre y TitleID o serial.
2. Verificación por hash cuando la pides, contra No-Intro y Redump. Los hashes quedan guardados.

### 7.3 Actualizaciones y DLC ✅
Se detectan y se muestran BASE, UPD y DLC. **Nunca se borran las versiones viejas.**

### 7.4 Tiempo de juego ✅
El tiempo de juego no lo registra la app: **muestra el de Steam**.

### 7.5 Respaldo de datos y partidas ✅
- **Esto reemplaza lo que se había dicho antes** ("las partidas no tienen copia de seguridad").
- **Dónde:** en un repositorio **privado** de GitHub, `otteretro-datos`. Las partidas de más de 50 MB van con Git LFS.
- **Qué se respalda:**

  | Qué | Cuándo |
  |---|---|
  | Base de datos de la app, exportada como texto (JSON por tabla) | Cuando cambia algo y al cerrar la app |
  | Configuración de emuladores y ports | **Cada vez que cambia** |
  | Partidas guardadas | **Al cerrar el juego o el emulador** |
  | BIOS, keys y firmware | Cuando cambian (siempre en repositorio privado) |

- Los packs de texturas **no** se respaldan, porque pesan varios GB.
- Antes de actualizar un emulador o un port se sigue haciendo además una copia puntual (§8.4).
- Para saber cuándo se cierra un juego, **todo se lanza a través de Otteretro** (§10).

### 7.6 Migración entre equipos ✅
- Es probable que formatees, que pases a Linux o que tengas otro PC. **Todo tiene que poder moverse de un equipo a otro**:
  - La app es portable (P1).
  - Los datos y las partidas se recuperan desde el repositorio de respaldo (§7.5).
  - Las credenciales se recuperan según §5.6.
- Lo que depende de cada equipo, como la exportación a Steam o las rutas de Steam, se guarda **por equipo**. La app reconoce en qué PC está.

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

### 8.2 Ubicación ✅
Todo va en `Otteretro\Herramientas\` (§5.1). Los **complementos** (packs de texturas, shaders, ReShade, mods) van junto a su emulador o port y se **instalan y actualizan igual que ellos**.

### 8.3 Asignación ✅
- Cada consola tiene un emulador **predeterminado**. Cada juego puede usar un emulador o port **específico**, con su propia configuración.
- Puede haber **varias instalaciones** del mismo emulador.
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
- Si no lo consigue, **avisa de qué hay que instalar a mano**.

### 8.7 Herramientas que gestiona la app ✅
- **Steam ROM Manager** es una herramienta más: tiene su receta y vive en `Herramientas\`.
- **RetroAchievements**: inicias sesión y la app muestra los logros de cada juego y los activa en los emuladores compatibles.

### 8.8 Mandos ✅
Los mandos los gestiona **Steam Input**; la app no los configura.

### 8.9 Linux ✅
El formato de los emuladores en Linux (AppImage o Flatpak) **se decide cuando migres**. Las recetas ya prevén tener una variante por sistema operativo.

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
3. El hash contra No-Intro y Redump.
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
| Lanzador | Cada acceso de Steam abre **`otteretro jugar <id>`**, no el emulador directamente. Así Otteretro sabe cuándo se cierra el juego, respalda las partidas (§7.5) e instala lo que haga falta antes de abrirlo. El botón "Jugar" de la app usa el mismo camino. |
| Cuenta | La detectada (`loginusers.vdf`) |
| Qué se exporta | La opción elegida (port o emulador) de cada versión marcada. Puede haber varias versiones del mismo juego. |
| Juegos que ya están en Steam | **No se exportan.** Solo cuentan como "lo tengo". |
| Categorías | **Por saga**. Lo que no tiene saga va a **"Emulación"**. |
| Nombre | "Juego (Consola)" |

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
- **Información de tiendas y versión de PC**: se refresca periódicamente; el intervalo es configurable.
- **Bibliotecas**: se escanean **solo cuando pulsas "Escanear"**. La app no vigila las carpetas.
- **Carga inicial del catálogo**: **completa**, todas las sagas y juegos. Los datos que vienen de APIs se bajan con scripts.
- **Tope de Claude**: la **carga inicial va sin tope** y se hace de una sola vez. Después, cada ejecución tiene un tope configurable de tareas: al llegar a él, la app se pausa y avisa.
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
- La **especificación de pantallas** (qué muestra cada una y qué acciones tiene) se escribe antes de diseñar, a partir de esta sección.

### 12.2 Aspecto
| Tema | Decisión |
|---|---|
| Estilo | Oscuro, con portadas grandes. Referencias: **la biblioteca de Steam y Playnite** |
| Densidad | **Mixta**: portadas medianas con los datos clave a la vista (estado, consola, versión) |
| Acento | **Se adapta al color de la portada** de la saga o el juego que estás viendo |
| Identidad | **Logo y una pequeña mascota: una nutria retro**, usada con moderación (icono, pantallas vacías, avisos) |
| Imágenes | SteamGridDB |

### 12.3 Navegación
- **Barra lateral izquierda** con las pantallas principales: **Inicio, Sagas, Colección y Descubrir**.
- Abajo, una sección secundaria: **Descargas, Herramientas, Steam y Ajustes**.
- **Panel de tareas en segundo plano** (escaneos, descargas, Claude, respaldos) con su progreso, y una **notificación del sistema** cuando terminan.

### 12.4 Pantallas
| Pantalla | Contenido |
|---|---|
| **Inicio** | El siguiente juego de cada ruta que sigues · lo pendiente de revisar (identificaciones dudosas, sin identificar) · novedades (ports nuevos, cambios de compatibilidad, versiones nuevas de emuladores) · descargas y tareas en curso |
| **Sagas** | Cuadrícula de sagas. Al entrar en una saga: **línea de tiempo horizontal** con las portadas de la ruta elegida, un selector de ruta y tu progreso marcado en cada juego |
| **Colección** | **Por consola**, con filtros (lo tengo, me falta, verificado, revisar) y orden por nombre, saga o año |
| **Descubrir** | Sagas recomendadas por afinidad, cada una con su porqué |
| **Ficha de juego** | **A la vista:** cómo jugarlo (opción elegida, botón Jugar, cambiar de opción), versiones y comparativa con los 4 criterios, archivos (dónde están, BASE/UPD/DLC, si están verificados), y tu progreso, puntuación y notas. **En secciones desplegables o pestañas secundarias:** logros de RetroAchievements, tiempo jugado, disponibilidad en tiendas, conseguirlo (enlaces o megapack) y datos con sus fuentes |
| Descargas · Herramientas · Steam · Ajustes | Secundarias. Su contenido se detalla en la especificación de pantallas |

## 13. Pendientes

### 13.1 Decisiones del usuario
**No queda ninguna.** La encuesta 6 ([Interfaz y cierre](https://claude.ai/artifact/GHKEE7GuPTgWjKGopbKHwN)) cerró las que quedaban.

### 13.2 Pendientes técnicos 🔬
**Se investigan ahora, antes de desarrollar** (decisión #52). Los resultados quedarán en `docs/investigacion.md`.

| # | Qué hay que comprobar |
|---|---|
| T1 | **Préstamo familiar de Steam:** la Web API con clave no lo expone. Puede necesitar el token de sesión de Steam (`IFamilyGroupsService`) o leer archivos locales. |
| T2 | **Fuente de disponibilidad en tiendas:** por ejemplo, IsThereAnyDeal para tiendas y juegos gratis, y el catálogo público de Game Pass. |
| T3 | **Cómo guarda sus partidas cada port**, para su receta de migración. |
| T4 | **Qué formato exige cada port**, por ejemplo si Dusklight acepta RVZ o solo ISO. |
| T5 | **Autoactualización en Linux:** reemplazar el ejecutable mientras la app está abierta. |
| T6 | **Qué juegos tienes en Epic, GOG, Xbox para PC, EA y Ubisoft**, y cómo lanzarlos desde la app. |
| T7 | **Lanzador `otteretro jugar`:** detectar que el juego se cerró cuando el emulador abre procesos hijos, o cuando una tienda abre su propio launcher. |

### 13.3 Acciones tuyas fuera de la app
No son requisitos, pero siguen pendientes:

| # | Acción |
|---|---|
| A1 | Borrar `E:\Roms`, `E:\Emuladores` y `D:\Roms`. Están aprobadas, pero Claude Code no puede borrarlas. |
| A2 | Poner `prod.keys` y el firmware en Ryujinx SS4K. Hoy no arranca ningún juego de Switch. Cuando exista, la app lo hará desde `Herramientas\Sistema\` (§8.5). |
| A3 | Crear las cuentas y claves cuando toque: Steam Web API, IGDB (Twitch), MyJDownloader, ScreenScraper, SteamGridDB, RetroAchievements y las tiendas de PC. |

### 13.4 Supuestos que tomé sin preguntarte
Si alguno no te sirve, dímelo:

| # | Supuesto |
|---|---|
| S1 | El generador `_Herramientas\Catalogo` se porta a C#. Lo deduje de que elegiste scripts en C# dentro de la app (#36). |
| S2 | En 1G1R solo dijiste "español primero". El resto del orden por defecto es Europa > USA > Japón, y se puede cambiar en Ajustes. |
| S3 | La base de datos se respalda como JSON por tabla, para que git guarde solo los cambios entre copias. |
| S4 | Todo el respaldo va a **un solo repositorio privado**, `otteretro-datos`, con una carpeta por tipo. Si quieres separar las BIOS y keys en otro repositorio, dímelo. |
| S5 | **Game Pass** se muestra como "en Game Pass", no como "lo tengo", porque depende de tener la suscripción activa. |
| S6 | Para saber cuándo se cierra un juego y respaldar sus partidas, los accesos de Steam lanzan **`otteretro jugar <id>`** y no el emulador directamente (§10). |
