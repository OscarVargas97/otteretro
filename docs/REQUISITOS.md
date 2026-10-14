# Otteretro: requisitos consolidados

> **Este es el único documento vigente.** Si algo de aquí contradice el historial, manda este documento.
> **Última actualización:** 2026-10-14, tras la encuesta "Temas abiertos (1)" y los requisitos de Steam, megapacks y automatización.

## Cómo se mantiene

- Después de cada encuesta o decisión:
  1. Se guardan las respuestas originales en [historial/respuestas/](historial/respuestas/).
  2. Se añade una entrada en [historial/decisiones.md](historial/decisiones.md), que es el registro cronológico de qué cambió y cuándo.
  3. Se **actualiza este documento** para que refleje siempre el estado actual.
- [historial/v1-por-tema/](historial/v1-por-tema/) guarda la primera versión de los requisitos, dividida por tema, tal como estaba antes de consolidarla.

| Estado | Significado |
|---|---|
| ✅ | Decidido |
| 🟡 | Decidido en lo general, con detalles abiertos (ver §12) |

---

## 1. Visión

Otteretro es una app **personal y portable** para:

1. **Catalogar** las sagas icónicas y los juegos interesantes de las consolas elegidas, de lo retro a la generación de PS5 y Switch, incluida la versión de PC.
2. **Saber qué tienes**, ya sea en archivos de tus bibliotecas o en tu cuenta de Steam.
3. **Elegir cómo jugar** cada juego: versión de PC, port o emulador, y cuál es la mejor versión.
4. **Conseguir y organizar** juegos con el mínimo de intervención: descargas y megapacks.
5. **Llevarlo todo a Steam**, organizado por saga.
6. **Descubrir** sagas que no conoces y recorrerlas en el orden que convenga.

## 2. Principios

| # | Principio |
|---|---|
| P1 | **Portable.** La app vive en su propia carpeta, con su base de datos dentro, y no escribe nada en AppData. ✅ |
| P2 | **El sistema va separado de las bibliotecas.** Las bibliotecas pueden estar en cualquier disco y la app las encuentra. ✅ |
| P3 | **Multiplataforma.** Windows hoy y Linux en el futuro. ✅ |
| P4 | **Claude decide, los scripts repiten.** Claude solo se usa cuando hay que juzgar o la primera vez que aparece un caso. Lo repetitivo queda en scripts y no gasta tokens. Ver §11. 🟡 |
| P5 | **Claude se usa a través de tu Claude Code** (`claude -p`), **nunca con la API** de Anthropic. ✅ |
| P6 | **Intervención mínima.** Solo se pregunta cuando la app no puede decidir. ✅ |
| P7 | **No se pierden datos.** Las versiones viejas de emuladores y las actualizaciones de juegos nunca se borran. Mover siempre es copiar, verificar y recién entonces borrar. ✅ |
| P8 | **Solo enlaces tuyos.** La app nunca busca de dónde descargar juegos. Emuladores y ports sí puede descargarlos de su fuente oficial. ✅ |

## 3. Stack ✅

- **Backend:** C# con ASP.NET Core y SQLite.
- **Interfaz:** Next.js exportado como estático (React).
- **Ventana:** Photino, una ventana nativa que usa WebView2 en Windows y WebKitGTK en Linux.
- **Uso:** interfaz en **español**, pensada para escritorio con ratón y teclado. Para la tele se usa Steam Big Picture.
- **Nombre y repositorio:** Otteretro, en `D:\dev\personal\otteretro`. La build final es portable y puede ir en cualquier carpeta.

## 4. Consolas ✅

**Incluidas: 23 sistemas.**

| Empresa | Sistemas |
|---|---|
| Nintendo (12) | NES · Game Boy (incluye GBC) · SNES · Virtual Boy · N64 (incluye 64DD) · GBA · GameCube · DS (incluye DSi) · Wii · 3DS (incluye New 3DS) · Wii U · Switch |
| Sony (7) | PS1 · PS2 · PSP · PS3 · PS Vita · PS4 · PS5 (emulación temprana: SharpEmu, KytyPS5) |
| Microsoft (4) | Xbox · Xbox 360 · Xbox One y Series X\|S (estas dos solo se catalogan) |
| **PC** | Plataforma para las versiones de PC oficiales y ports. Ver §6.6 |

**Reglas**
- Los add-ons van dentro de su consola base y las variantes se agrupan con su consola.
- Las consolas sin emulación se catalogan igual.
- El estado de la emulación es un dato que se actualiza, no algo escrito a mano.

**Fuera: 49 sistemas.** Game & Watch, FDS, Satellaview, Pokémon Mini, Switch 2, y todo lo de Sega, NEC, SNK, Atari, otras marcas, arcade y computadoras retro. El detalle está en [historial/v1-por-tema/consolas.md](historial/v1-por-tema/consolas.md).

## 5. Bibliotecas

### 5.1 Tipos 🟡

| Tipo | Contiene |
|---|---|
| **Juegos** | `<Consola>\<Juego>\…` y `_Entrada\`, donde se procesan las descargas y los megapacks. Puede haber las que quieras, en cualquier disco. |
| **Herramientas** | `Emuladores\<Consola>\<Instalación>\<versión>\`, `Ports\<Consola>\<Port>\<versión>\` y `Sistema\<Consola>\<archivo>\<versión>\` (BIOS, keys, firmware). Es compartida por todas las bibliotecas de juegos. |

### 5.2 Marcador y registro ✅
- Cada biblioteca tiene en su raíz un `otteretro-biblioteca.json` con `{id, nombre, tipo, creada, formato}`. El `id` no cambia nunca.
- La app guarda en `data\otteretro.db` el registro de bibliotecas, con la última ruta conocida de cada una.

### 5.3 Operaciones ✅
| Operación | Qué hace |
|---|---|
| Crear | Elige una carpeta, escribe el marcador y registra la biblioteca. Las carpetas de cada consola se crean cuando hacen falta. |
| Adoptar | Registra una carpeta que ya tiene marcador, o una con la estructura antigua (en ese caso crea el marcador). |
| Renombrar | Cambia el nombre de la biblioteca. |
| Quitar | Saca la biblioteca del registro. **Nunca borra archivos.** |
| Reconectar | Si la biblioteca no está en su última ruta, la marca como desconectada y la busca en las raíces de los discos y puntos de montaje (2 niveles), o te deja indicar la ruta. |
| Mover o migrar | Mueve una biblioteca, una consola, un juego o un emulador o port: copia, verifica el hash, actualiza la base de datos y **reescribe las rutas internas**, y por último borra el origen. |

### 5.4 Rutas ✅
- En la base de datos se guarda **id de biblioteca + ruta relativa**. La ruta absoluta se arma solo al lanzar un juego o exportarlo.
- Las rutas guardadas dentro de emuladores y ports, como `isoPath` en Dusklight o `ISOPath0` en Dolphin, **se reescriben solas**. Cada receta indica cuáles son.
- Los juegos de una biblioteca desconectada aparecen como "no disponibles" y no se exportan.

### 5.5 Migración de lo actual ✅
- `E:\Consolas` se adopta como primera biblioteca de juegos. Sus emuladores y ports **se mueven** a la biblioteca de herramientas.
- `_Herramientas\Catalogo` se integra al repositorio (ver §11.4).
- Las carpetas sobrantes `E:\Roms`, `E:\Emuladores` y `D:\Roms` quedaron aprobadas para borrar, pero las tienes que borrar tú: Claude Code bloquea borrar carpetas en la raíz de un disco.

## 6. Catálogo

### 6.1 Alcance ✅
- **La mayor cantidad de juegos posible**, siempre con su **saga identificada**. Los juegos destacados sin saga también entran.
- **Todo lo que tienes aparece siempre**, aunque no sea icónico.
- Para cada juego se indica **si existe versión de PC**.

### 6.2 Modelo ✅
**Saga → Juego → Versión** (consola o PC; original, remaster o remake) **→ Origen** (archivo en una biblioteca o tu cuenta de Steam).
- Los datos se guardan por consola, pero **se muestran, se gestionan y se exportan por saga**.
- Los juegos sin saga se agrupan como "Emulación".

### 6.3 Versiones ✅
- **Todo es una versión del mismo juego**, también los remakes. Por ejemplo, Pokémon Ruby y Omega Ruby.
- IGDB clasifica cada versión, Claude revisa y tú puedes corregir.
- Se muestra una tarjeta por juego con sus versiones desplegables.
- Puedes marcar **varias versiones** del mismo juego para Steam.

### 6.4 Mejor versión ✅
Hay cuatro criterios y se muestran **por separado**: **contenido**, **jugabilidad hoy**, **técnico** y **original**. La comparación incluye todas las versiones.

### 6.5 Rutas por saga 🟡
- Cada saga tiene sus rutas: **orden de lanzamiento**, **orden de la historia** y **ruta recomendada por la comunidad**.
- En cada paso, la ruta indica **qué versión recomienda la comunidad**, el original o el remaster, y por qué.

### 6.6 Versión de PC 🟡
- PC es una plataforma más dentro de cada saga.
- **Tu biblioteca de Steam cuenta como "lo tengo"**: esos juegos aparecen como "lo tengo · versión PC" y no hace falta conseguirlos para un emulador.
- Si un juego tiene versión de PC pero no la tienes, se indica que existe.

### 6.7 Icónico, gustos y Descubrir ✅
| Eje | De dónde sale |
|---|---|
| Icónico | Claude reúne el consenso de Reddit, ResetEra, rankings, notas (OpenCritic/Metacritic) y ventas, con justificación y fuentes. Tú apruebas. |
| Mis gustos | Sagas favoritas, juegos puntuados y horas jugadas en Steam |
| Descubrir | Claude recomienda por afinidad con tus favoritos y explica por qué |

### 6.8 Fuentes ✅
| Fuente | Para qué |
|---|---|
| IGDB | Base: sagas, versiones, plataformas e ids de Steam |
| ScreenScraper | Identificar juegos por hash y medios |
| Wikidata | Relaciones entre juegos y sagas |
| OpenCritic / Metacritic | Notas |
| HowLongToBeat | Duración |
| RAWG | Complemento de datos |
| SteamGridDB | **Imágenes** de la app y de Steam |

Si dos fuentes no coinciden, manda una **prioridad fija por campo**. Lo que corriges a mano nunca se sobrescribe.

## 7. Colección

### 7.1 Qué cuenta como "lo tengo" ✅
Un archivo en una biblioteca de juegos, o **un juego en tu biblioteca de Steam** (cuenta como versión PC).

### 7.2 Detección ✅
1. Escaneo rápido por nombre y TitleID o serial.
2. Verificación por hash bajo demanda, contra No-Intro y Redump. Los hashes quedan guardados.

### 7.3 Actualizaciones y DLC ✅
Se detectan y se muestran BASE, UPD y DLC. **Nunca se borran las versiones viejas.**

### 7.4 Tiempo de juego y partidas ✅
- La app no registra el tiempo de juego. **Muestra el de Steam**, que también sirve como señal de tus gustos.
- No hay copias de seguridad generales de partidas, solo la copia puntual antes de actualizar un emulador o port (ver §8.4).

## 8. Emuladores y ports

### 8.1 Instalación ✅
- Se **descargan de la fuente oficial** y también se **detectan** los que pones a mano.
- Cada emulador y port tiene una **receta** en el repositorio (JSON) con:
  - de dónde se descarga,
  - cómo se deja portable,
  - dónde guarda partidas y configuración,
  - qué rutas internas tiene,
  - qué pasos extra necesita (la app los ejecuta sola),
  - qué formato de juego acepta o exige.

### 8.2 Ubicación ✅
Van en la **biblioteca de herramientas** (§5.1).

### 8.3 Asignación ✅
- Cada consola tiene un emulador **predeterminado**, y cada juego puede tener un emulador o port **específico**, con su propia configuración.
- Puede haber **varias instalaciones** del mismo emulador.
- La elección entre PC, port y emulador es **automática**, se puede cambiar a mano en cada juego, y los valores que usa la regla son editables. La prioridad de la versión de PC está 🟡 abierta.

### 8.4 Actualización ✅
- La actualización la lanzas tú. La app revisa y **avisa** qué tiene versión nueva, y tú eliges qué instalar.
- La versión nueva se instala **al lado** de la anterior. **La vieja no se reemplaza nunca** y se puede volver a ella.
- **Migración**: primero una copia de seguridad puntual, luego se copian las partidas y la configuración según la receta. Cómo guarda cada port sus partidas se investiga al escribir su receta.

### 8.5 Archivos de sistema ✅
- Las BIOS, keys y firmware **los subes tú desde la interfaz**. La app no los descarga.
- Quedan en un almacén común, compartido por todos los emuladores, **con control de versiones**.
- La app indica dónde va cada uno, comprueba que estén y avisa si falta alguno.

## 9. Ingesta (descargas y megapacks)

### 9.1 Proceso único ✅
```
Origen ─► Extraer ─► Identificar ─► Convertir ─► Ubicar ─► Marcar "lo tengo"
 · enlaces (JDownloader)   · megapack o carpeta local
```

### 9.2 JDownloader ✅
- Se usa la **API de MyJDownloader**.
- Los enlaces se pegan en la ficha del juego o se importan en una lista. En las listas, la app averigua a qué juego corresponde cada enlace.
- Al pedir la descarga eliges la biblioteca de destino, viendo cuántos **GB libres** tiene. La descarga, la extracción y la organización ocurren ahí mismo.

### 9.3 Megapacks 🟡
Un archivo o carpeta con muchos juegos entra por el mismo proceso y se identifica, ordena y organiza igual.

### 9.4 Identificación ✅
Se intenta en este orden:
1. El origen (por ejemplo, la ficha donde pegaste el enlace).
2. El nombre y el TitleID o serial.
3. El hash contra No-Intro y Redump.
4. Claude, solo para los casos ambiguos.
5. Si nada funciona, va a "Sin identificar" y la app avisa.

### 9.5 Formatos ✅
- Siempre se usa **el formato más liviano**: se descomprimen zip, 7z y rar; GameCube y Wii pasan a RVZ; PS1 y PS2 pasan a CHD.
- **Excepción:** si un port exige el juego original sin modificar, se respeta. Qué copia se guarda en ese caso está 🟡 abierto.
- El comprimido se borra después de verificar.

## 10. Steam ✅

| Tema | Decisión |
|---|---|
| Herramienta | Steam ROM Manager, a partir de manifiestos "Manual" que genera Otteretro. El arte sale de SteamGridDB. |
| Cuenta | Se detecta la última que inició sesión (`loginusers.vdf`). |
| Qué se exporta | Por cada versión marcada, la opción elegida (port o emulador). Pueden ser varias versiones de un mismo juego. |
| Juegos que ya tienes en Steam | **No se exportan.** Solo marcan "lo tengo". |
| Categorías | **Por saga**. Lo que no tiene saga va a **"Emulación"**. |
| Nombre | "Juego (Consola)", por ejemplo "The Legend of Zelda: Twilight Princess (GameCube)". |

## 11. Automatización y Claude

### 11.1 Claude decide, los scripts repiten 🟡
- **Claude** se usa para lo que requiere **criterio**: qué es icónico, la mejor versión, las rutas por saga, Descubrir, los casos ambiguos de identificación, y escribir una receta nueva.
- **Los scripts** hacen todo lo **repetitivo y determinista**, sin gastar tokens.
- **La primera vez** que aparece un caso, lo resuelve Claude. Lo que aprende se **guarda como dato reutilizable** (alias, regla o receta), y desde ahí lo hace el script.

| Función | Lo que hace el script (sin tokens) | Lo que hace Claude (solo cuando hace falta) |
|---|---|---|
| Ports nuevos | Descarga Recompendium y otras fuentes y calcula qué cambió | Clasificar entradas que no encajan en los patrones conocidos |
| Compatibilidad de emuladores | Todo | Nada |
| Identificar juegos | TitleID, hash, DAT y alias aprendidos | Casos ambiguos; el resultado se guarda como alias |
| Enlaces de una lista → ficha | Búsqueda aproximada por nombre y alias | Lo que no encaje; el resultado se guarda como alias |
| Recetas de emuladores y ports | Instalar, actualizar y migrar | Escribir la receta la primera vez |
| Icónico, mejor versión y rutas | Reunir notas, ventas y datos de las APIs | Ponderar y justificar; el resultado se guarda y solo se rehace cuando lo pides |
| Descubrir | Leer gustos y horas jugadas | Recomendar; se guarda |

Cómo se implementa este principio (dónde viven los scripts, si se registra el uso de Claude, si las reglas aprendidas se aprueban) se decide en la encuesta 4 (§12).

### 11.2 Ejecución ✅
- Las actualizaciones las lanzas tú con un botón. Desde ahí, el proceso es automático.
- Se lanzan a mano porque pueden implicar migrar partidas.

### 11.3 Claude Code ✅
- Otteretro ejecuta `claude -p` con prompts guardados en el repositorio.
- Esos prompts fijan qué puede tocar Claude, cuántos cambios puede hacer como máximo y que debe citar fuentes.

### 11.4 Generador actual ✅
`E:\Consolas\_Herramientas\Catalogo`, el script en Node de unas 350 líneas, se integra al repositorio. Propuesta: portarlo a C#.

## 12. Temas abiertos

Se cierran con la encuesta 4, **[Temas abiertos (2)](https://claude.ai/artifact/J451E3j479E1AXqRJah3iF)** (doc `cuestionario/temas2`), que incluye el bloque F de automatización.

| Bloque | Pendiente |
|---|---|
| A · Steam y PC | Cómo leer Steam (Web API o archivos locales), qué cuentas, otras tiendas, prioridad de la versión de PC y qué mostrar si no la tienes |
| B · Megapacks | Copiar o mover, qué importar, regiones (1G1R), hacks y traducciones, duplicados, y qué hacer cuando Claude no está seguro |
| C · Herramientas | Cuántas bibliotecas de herramientas, si los ports van ahí, qué significa "temas para emuladores" y dónde van los packs de texturas |
| D · Formatos | Qué copia se guarda cuando un port exige el original |
| E · Rutas | Tipos de ruta, quién las prepara, y si se registra el progreso |
| F · Automatización | Dónde viven los scripts, aprendizaje de reglas, registro del uso de Claude y sugerencias de automatización |
