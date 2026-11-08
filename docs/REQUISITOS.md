# Otteretro: requisitos consolidados

> **Este es el único documento vigente.** Si algo de aquí contradice el historial, manda este documento.
> **Última actualización:** 2026-11-06, con la #343 a la #345 (Herramientas: una tarjeta por emulador o port en secciones por consola, con la versión en uso a la vista y las demás plegadas), la #336 a la #342 (solo se puede elegir una versión del catálogo con todas las columnas que usa la app, y el manifiesto publica sus tablas y columnas), la #331 a la #339 (Herramientas › Emuladores en una lista con la versión en uso, librería con una sola copia y Switch 2 con emuladores), la #328 a la #330 (`.nup` de Switch y Switch 2 tal cual, paquetes sin juegos en Revisar y reintento con botón o al actualizar), la #324 a la #327 (un port tiene una sola versión, en `Ports\<Port>`, que se actualiza en el lugar sin perder lo tuyo), la #320 a la #322 (avisos de versiones solo de lo instalado, en su pestaña y en una fila), la #305 a la #319 (avisos también de lo no instalado, decompilaciones y ports no listados en el catálogo de ports, partidas compartidas entre versiones, Snes9x, Mega Drive y Dreamcast, Steam ROM Manager como herramienta, Dusklight portable, Ryubing Canary y las RC de Eden, `Herramientas` fuera de los datos, Novedades de ports, perfil al instalar un port, usuarios de consola y licencias zRIF), con `Herramientas\` en cada disco para Steam ROM Manager (#274), la #299 a la #304 (el catálogo de ports en la administración, en dos pasos; perfil por versión del juego; una carpeta por emulador; segunda copia de la librería en el disco que elijas; reinstalar desde la librería), la #286 a la #298 (encuesta 19: emuladores compartidos con perfiles por juego, librería privada, avisos, UPD y DLC, ports, compartir perfiles y configurar el emulador desde la app), la #279 a la #282 (el catálogo se genera con la menor cantidad de Claude; en Otteretro, Claude identifica al terminar el escaneo o la importación, sin horario, desde Almacenamiento › Reconocer juegos), la #278 (el catálogo publica el ID de cada juego; las tablas de TitleID y seriales quedan de respaldo), la #276 y la #277 (identificar sin IA primero y juegos de Switch 2 extraídos como carpeta), la #269 a la #275 (encuesta 18: cómo se presenta Otteretro en su README), la #264 a la #268 (imágenes sin clave en Otteretro, el catálogo completa y publica en bloque, un buscador de versiones del catálogo en su propia pestaña de Ajustes y versiones solo cuando cambian los datos), con la #258 a la #263 (Otteretro solo consume el catálogo y sus consolas solo filtran lo que se muestra: sin portadas propias, cambio de versión manual y confirmado, diff acumulativo publicado por el catálogo y verificado por la app, y PR del catálogo validado), la #253 a la #256 (catálogo como proyecto aparte), la #250 a la #252 (catálogo incluido, licencia GPL-3.0 y portadas), la #244 a la #249 (descargas a la vez por sitio y calidad de las descargas, canal Local detectado en los discos, capturador persistente con enlaces privados por juego, portapapeles y su aviso), la #239 a la #242 (fuentes de emuladores y librería privada, TitleID solo del nombre, herramientas en el repositorio y conservar el entorno), la #215 a la #237 (encuesta 17: el plan va por dominios, la F4 pasa a «Archivos de juegos», un solo modelo de discos, descargas como un JDownloader dentro de Otteretro, carpeta de entrada por disco, verificar lo convertido y §5 y §9 reescritos sin capas), la #212 a la #214 (apertura de la F4: cuándo se escanea, TitleID de Switch y juegos de PC sueltos en `Juegos\PC`), la #211 (mover por grupos: tipo › consola › cada cosa, una consola entera en una sola tarea), la #210 (Almacenamiento con la distribución de Steam: tabla con los datos de la app como una fila más), la #208 y la #209 (la carpeta de datos es la raíz del disco ★, que se registra solo, y los discos en un desplegable como Steam), la #207 (el tercer número sube con cada PR a dev), la #206 (canal de actualizaciones Local), la #201 a la #205 (discos como Steam: una raíz `X:\Otteretro` por disco con cinco carpetas fijas, `E:\Consolas` y Herramientas pasan al estándar, disco predeterminado ★ y mover lo marcado), la #200 (Ajustes y Herramientas con pestañas arriba y una sola columna centrada; reemplaza la #196 y la #197), la #192 a la #199 (encuesta 16: la consola como centro, Almacenamiento por tipo con ubicaciones, Herramientas como inventario y Ajustes con lista y detalle), la #186 a la #189 (plan reorganizado desde la F3, adoptar migra a §5.1, Dusklight con su enlace y avisos de versiones nuevas), la #184 y la #185 (Ajustes en dos columnas y menú lateral retraíble), la #183 (`0.FASE.extra`: F2.5 es 0.2.1 y el plan completo, 1.0.0), la #179 a la #182 (versión por fase, canales de actualización, `main` por defecto y la primera release 0.2.0), la #178 (la imagen de cada saga), la #169 a la #177 (revisión de UI de toda la app y fluidez primero, encuesta 15), la #165 a la #168 (exclusivas, más reconocidas, catálogo separado de tus datos), la #162, la #163 y la #164 (consolas que habilitas tú, catálogo al día cada semana y reevaluar icónicas), la #161 (corregir lo que cambió Claude), la #159 y la #160 (el horario lanza solo, «Ejecutar ahora» corre ya y el tope de día es opcional; antes #158: sin tope de tareas).

## Cómo se mantiene

- Después de cada encuesta o decisión:
  1. Las respuestas van en el issue «Encuesta NN» de la encuesta, sin contenido personal (#146).
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
2. **Saber qué tienes**: archivos en tus discos y juegos en tu cuenta de Steam.
3. **Elegir cómo jugar** cada juego: versión de PC, port o emulador, y cuál es la mejor versión.
4. **Conseguir y organizar** juegos con la mínima intervención, ya sea con descargas o con megapacks.
5. **Llevarlo todo a Steam**, organizado por saga.
6. **Descubrir** sagas que no conoces y **recorrerlas** siguiendo la ruta que elijas.
7. **Ser tu centro de entretenimiento** (#107): la pantalla principal del modo juego de PlayOS, con tus juegos y sagas **de consola y de PC**, el seguimiento de lo que juegas (§7.4) y el paso fácil a Steam Big Picture. Se maneja completo con mando (#108, §12.5). **Envuelve, no reemplaza:** muestra lo útil para jugar y cada juego lo abre su tienda o lanzador dueño (#130).

### 1.1 Cómo se presenta (encuesta 18, #269 a #275)
- **Qué es:** un organizador de tu colección de juegos y emuladores repartida en discos, que además te dice qué jugar y en qué orden. Lema en la dirección de «Ordena tu colección. Recorre tus sagas.», con la emulación nombrada desde el inicio.
- **Para quién:** quien tiene juegos y emuladores repartidos; también el coleccionista, el jugador de sagas y el usuario avanzado de emulación. Se presenta como acceso anticipado.
- **Al frente:** rutas, catálogo incluido, seguimiento y ordenar como ahorro de tiempo. Atrás: descargas (para tus propios juegos guardados en servicios de terceros, con aviso legal contra la piratería), megapacks y jerga técnica. Claude, como ayuda opcional; el catálogo, como proyecto de comunidad.
- **README:** en español, tono cercano, largo medio, capturas con portadas (Sagas arriba), recorrido descubrir → tener → ordenar → jugar, hoja de ruta breve al final y estado en su sección. Sale con la 0.4 estable.

## 2. Principios ✅

| # | Principio |
|---|---|
| P1 | **Instalada, con los datos donde tú elijas.** La app se instala con Velopack (en AppData; *en Linux, reemplazado por la #114: flake de Nix y se actualiza con el sistema*). Todo lo pesado o valioso (base de datos, herramientas, caché de imágenes) va en una **carpeta de datos** que eliges tú. *Reemplaza a "Portable" (decisión #55).* |
| P2 | **Tus juegos van separados del sistema.** Pueden estar en cualquier disco y la app los encuentra (§5). |
| P3 | **Multiplataforma**: Windows hoy y Linux en el futuro. El destino es **PlayOS**, un mod de WorkOS (tu sistema NixOS) orientado a juegos, con Otteretro, Steam y configuraciones de juego, para el portátil de juego (NVIDIA, Hyprland); se diseña pensando en él desde ya y se trabaja después (#103). |
| P4 | **Claude decide y los scripts repiten.** Claude se usa solo para juzgar o la primera vez que aparece un caso. Lo repetitivo pasa a scripts y no gasta tokens (§11). |
| P5 | **Claude se usa a través de tu Claude Code** (`claude -p`), **nunca de la API** de Anthropic. |
| P6 | **Intervención mínima.** La app solo pregunta cuando no puede decidir. Si duda, asigna y marca "revisar". |
| P7 | **No se pierden datos.** Nunca se borran versiones viejas de emuladores ni actualizaciones de juegos. Mover siempre es copiar, verificar y recién entonces borrar. |
| P8 | **Solo enlaces tuyos.** La app nunca busca de dónde descargar juegos. Emuladores, ports y complementos sí los descarga de su fuente oficial. |
| P9 | **La app evoluciona.** Cuando detecta tareas que conviene automatizar, crea issues. Claude Code las implementa en el repositorio y la app se actualiza sola (§11.5). |
| P10 | **Se puede migrar.** Formatear, pasar a Linux o cambiar de PC no hace perder nada (§7.6). |

## 3. Stack y repositorio ✅

- **Backend:** C# (.NET 10) con ASP.NET Core y SQLite (Dapper). Detalle en **[arquitectura.md](arquitectura.md)** (#92 a #99). **Interfaz:** Next.js exportado como estático (React). **Ventana:** Photino, que usa WebView2 en Windows y WebKitGTK en Linux.
- **Idioma y uso:** interfaz en **español e inglés** (#140 a #142), con los textos en un catálogo por idioma para que sumar otro sea agregar un archivo; se elige en Ajustes y por defecto sigue al sistema. Pensada para escritorio con ratón y teclado. Para la tele se usa Steam Big Picture.
- **Idioma del código** (#125, #139): identificadores, tablas y columnas en **inglés**; documentación, comentarios, commits, PRs e issues en español.
- **Repositorio:** en **GitHub, público** (#145). Ramas: **`dev`**, donde se trabaja, y **`main`**, que es lo publicado.
- **Releases:** se desarrolla en `dev` y una release es un PR de `dev` a `main` (#181); al fusionarlo, GitHub Actions publica `0.FASE.N` (#207, reemplaza el tercer número de la #183): `VERSION` dice la fase y N sube con cada PR fusionado en `dev`, que publica la prerelease `0.FASE.N-dev`; la estable lleva la N de la punta de dev que se fusiona. El plan completo, 1.0.0.
- **Canales** (#180): en Ajustes › General, Estable (releases de `main`, por defecto), Pruebas (prereleases de `dev`), Local (los paquetes armados en este equipo, que se encuentran solos en `Otteretro\Paquetes` de tus discos; #244) u Otro origen (carpeta, URL o repo de GitHub de quien arma su propia versión).
- **Instalación y actualización:** con **Velopack**, desde las releases de GitHub. En Windows se instala en `%LocalAppData%\Otteretro`. *En Linux, reemplazado por la #114:* paquete y módulo de Nix, que se actualiza con el sistema. Se actualiza sola y nunca toca la carpeta de datos.

## 4. Consolas ✅

**Incluidas: 24 sistemas** (Switch 2 desde la #152), y las que habilites tú (#162).

| Empresa | Sistemas |
|---|---|
| Nintendo (13) | NES · Game Boy (incluye GBC) · SNES · Virtual Boy · N64 (incluye 64DD) · GBA · GameCube · DS (incluye DSi) · Wii · 3DS (incluye New 3DS) · Wii U · Switch · Switch 2 (emulación experimental, #339) |
| Sony (7) | PS1 · PS2 · PSP · PS3 · PS Vita · PS4 · PS5 (emulación temprana: SharpEmu, KytyPS5) |
| Microsoft (4) | Xbox · Xbox 360 · Xbox One y Series X\|S (estas dos solo se catalogan) |
| **PC** | Versiones de PC oficiales y ports (§6.6). **PC VR** va aparte (#152), como PC: solo por saga y con lo comprado en Steam |

**Reglas**
- Los add-ons van dentro de su consola base y las variantes, junto con su consola.
- Las consolas sin emulación también se catalogan.
- El estado de la emulación es un dato que se actualiza, no un valor fijo.
- **Sumar consolas** (#162): en Ajustes › Consolas se agrega cualquier plataforma de IGDB, una a la vez, y al habilitarla se cargan sus juegos. Una consola deshabilitada no se carga ni se pone al día.
- **Juegos nuevos** (#163): cada semana (editable) la app trae de IGDB lo nuevo o cambiado de las consolas habilitadas.
- Un juego va a la saga de su colección de IGDB cuando esa colección no lleva el nombre de su franquicia: Counter-Strike tiene su propia saga aunque IGDB lo ponga en Half-Life (#153).
- Un juego de una saga sin ninguna de estas consolas (Game & Watch, CD-i, Satellaview…) se muestra en la saga atenuado, con «Sin consola en Otteretro», y no cuenta para la colección (#152).

**Fuera: 48 sistemas.** Game & Watch, FDS, Satellaview, Pokémon Mini y todo lo de Sega, NEC, SNK, Atari, otras marcas, arcade y computadoras retro. Detalle en [historial/v1-por-tema/consolas.md](historial/v1-por-tema/consolas.md).

## 5. Almacenamiento: discos, como Steam

Lo vigente, sin capas (#217). El camino hasta acá (bibliotecas, ubicaciones por tipo, discos) está en [historial/decisiones.md](historial/decisiones.md), de la #187 a la #211 y la #219.

### 5.1 Discos ✅
- Se eligen **discos**, como las bibliotecas de Steam (#201). Cada disco tiene una sola raíz `X:\Otteretro\` (en Linux, `~/Otteretro`) con su marcador y carpetas fijas que crea la app:

```
%LocalAppData%\Otteretro\        ← la app instalada con Velopack (solo binarios; guarda dónde está la carpeta de datos)

X:\Otteretro\                    ← un disco (marcador otteretro-ubicacion.json)
├── Juegos\<Consola>\<Juego>\…   ← y Juegos\PC\<Juego>\ para los juegos de PC sueltos (#214)
├── Emuladores\<Emulador>\<versión>\     ← uno por emulador aunque sirva a varias consolas (#302)
├── Ports\<Port>\               ← una sola versión por port, que se reemplaza al actualizar (#324)
├── Herramientas\<Herramienta>\<versión>\   ← lo que no es emulador ni port, como Steam ROM Manager (#274)
├── Libreria\                    ← solo en el ★: el original de cada versión (#293), en una sola copia (#335)
├── Complementos\<Emulador o port>\<Complemento>\<versión>\   ← texturas, shaders, ReShade, mods
├── Sistema\<Consola>\…          ← BIOS, keys y firmware, con versiones
└── Entrada\                     ← lo que dejas acá se importa solo (§9.3, #221)

X:\Otteretro\ del disco ★ además lleva la carpeta de datos:
├── data\otteretro.db            ← lo tuyo: lo que tienes, progreso, favoritas, claves, ajustes
├── catalogo\                    ← el catálogo, sin nada tuyo (#167): catalogo.db e imagenes\
└── logs\
```

- **Disco predeterminado ★** (#203, #208): el de la carpeta de datos, que se registra solo como disco. Ahí van lo nuevo y las herramientas. Cada consola puede elegir otro disco para sus juegos nuevos (#195).
- **Un solo modelo: discos** (#219). Lo que venga de antes (carpetas con `otteretro-biblioteca.json` o ubicaciones de un solo tipo) se migra solo a un disco al abrir la app.
- **En red** (#136): un disco puede ser una carpeta de red (SMB/NFS), pensando en el NAS (#115).

### 5.2 Marcador y registro ✅
- El marcador `otteretro-ubicacion.json` de la raíz guarda `{id, nombre, creada, formato}`. El `id` no cambia nunca.
- `data\otteretro.db` guarda el registro de discos y la última ruta conocida de cada uno.

### 5.3 Operaciones ✅
Todo desde **Ajustes › Almacenamiento**: arriba, un desplegable con el disco elegido (nombre, ★ y espacio libre), «+» para agregar otro y «⋯» con sus acciones; abajo, una tabla agrupada por tipo › consola o emulador › cada cosa, con su tamaño, y la fila «Datos de Otteretro» en el disco ★ (#209 a #211).

| Operación | Qué hace |
|---|---|
| Agregar | Elige una unidad, crea `X:\Otteretro\` con su marcador y la registra. |
| Adoptar | Una carpeta de antes (como `E:\Consolas`) pasa al estándar (#202): sus juegos a `Juegos\<Consola>\<Juego>` (en el mismo disco, sin copiar), sus emuladores, ports y complementos a sus carpetas, con las rutas internas al día. |
| Renombrar / Quitar | Quitar saca el disco del registro. **Nunca borra archivos.** |
| Reconectar | Si un disco no está en su última ruta, se marca desconectado y se busca en las raíces de los discos y puntos de montaje (2 niveles), o indicas la ruta. |
| Mover | Un tipo, una consola entera (en una sola tarea) o cosas sueltas, siempre a la misma ruta en el otro disco (#211). Copia, verifica el hash, actualiza la base de datos, **reescribe las rutas internas** y por último borra el origen. Se retoma si se corta. |
| Mover los datos de Otteretro | La fila «Datos de Otteretro» mueve solo `data`, `catalogo` y `logs`; lo demás del disco se queda (#210). |
| Quitar un juego | Con confirmación, **a la papelera** (#224): libera espacio y deja de contar como «lo tengo». Sus actualizaciones y DLC solo se van si las marcas (P7). |

### 5.4 Rutas ✅
- En la base de datos se guarda **id del disco + ruta relativa**. La ruta absoluta se arma solo al lanzar o exportar un juego.
- Las rutas guardadas dentro de emuladores y ports, como `isoPath` o `ISOPath0`, **se reescriben solas** según la receta de cada uno.
- Los juegos de un disco desconectado aparecen como «no disponibles» y no se exportan.

### 5.5 Lo actual pasa al estándar ✅
- `E:\Consolas` pasa a `E:\Otteretro\Juegos` con la tarea de Adoptar (#202), y Dolphin, Ryujinx SS4K y Dusklight con sus texturas, a las carpetas del disco ★ (#204). **El dominio Archivos no se cierra con tus juegos en la estructura vieja** (#218).
- `_Herramientas\Catalogo` se integra al repositorio (§11.6).
- Carpetas sobrantes: `E:\Roms`, `E:\Emuladores` y `D:\Roms` están aprobadas para borrar, pero lo tienes que hacer tú, porque Claude Code bloquea los borrados en la raíz de un disco.

### 5.6 Credenciales ✅
- Las claves de Steam Web API, IGDB, ScreenScraper, SteamGridDB, RetroAchievements, las tiendas y las **cuentas de los sitios de descarga** (1fichier, Mega, MediaFire…, #225) se guardan en `data\`, **cifradas con el almacén seguro del sistema operativo**: DPAPI en Windows, libsecret en Linux (con gnome-keyring, #147). Las claves son de cada equipo: lo que cifra un PC no lo abre otro.
- **Un solo token de GitHub** sirve para escribir en `otteretro-datos` (#68). Las actualizaciones no lo necesitan: el repo es público (#180).
- **Bitwarden** es tu **respaldo manual**: la app no lo lee. En un PC nuevo, las claves se vuelven a escribir desde ahí.

## 6. Catálogo

### 6.1 Alcance ✅
- **La mayor cantidad de juegos posible**, siempre con su **saga identificada**. También entran juegos destacados sin saga.
- **También juegos y sagas de PC**, no solo de consolas (#107): las sagas icónicas de PC y todo lo que tienes (#131).
- **Una saga no pertenece a un sistema** y no se repite: sus juegos se asocian a los sistemas, y PC es uno más (#131).
- **La carga es por partes** (scripts y Claude): nunca todas las sagas de PC de golpe, para que termine y no agote la suscripción. PC va después de Microsoft (#69, #131).
- **Todo lo que tienes aparece siempre.**
- Para cada juego se indica **si existe versión de PC**.
- **Exclusivas** (#165): una saga es exclusiva de una empresa si todas sus versiones de consola son de ella; una versión de PC no le quita lo exclusivo.
- **Más reconocidas** (#166): primero las icónicas aprobadas y después la popularidad en IGDB (cuánta gente calificó sus juegos, en la consola que mires).
- **El catálogo va aparte de lo tuyo** (#167) y **viene con la app** (#250): quien la instala tiene las sagas y los juegos desde el primer momento, sin claves. Se publica sin nada tuyo. El catálogo es un proyecto aparte, `otteretro-catalogo` (#253): en la pestaña Ajustes › Catálogo ves el catálogo en uso, las versiones publicadas con sus cambios y eliges a cuál ir, también a una anterior (#266, #268), pero solo entre las **compatibles** (#340): las que traen todas las tablas y columnas de catálogo que usa la app, según el `schema` (tabla.columna) de su manifiesto, que el catálogo publica (#342); las demás salen sin botón y con su razón («Necesita Otteretro más nuevo» o «De una versión anterior de Otteretro», #341) y la versión nueva que pide una app más nueva no se baja sola, solo avisa que hay que actualizar Otteretro; solo hay versión nueva cuando cambian los datos (#267); antes de aplicarla ves qué cambia (renombrados, fusionados, quitados), con lo que toca tus datos primero, y la aceptas o la rechazas entera (#259, #260). El diff lo publica el catálogo como `cambios.json` acumulativo y la app lo verifica con su propia comparación (#262). Al aceptar, verifica el hash y la aplica sin tocar lo tuyo; un fork pasa por la misma confirmación. Generar el catálogo (IGDB, Claude, sus claves) ya no es parte de la app, y los cambios se proponen en ese repo (#254, #256). Otteretro no busca portadas ni usa claves de imágenes: baja al navegar las que publica el catálogo, que las busca y completa con sus claves (#258, #264, #265).

### 6.2 Modelo ✅
**Saga → Juego → Versión** (consola o PC; original, remaster o remake) **→ Origen** (un archivo en un disco o tu cuenta en una tienda de PC).
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
  - **En PlayOS** (#132), lo que tienes se lee de **Steam**, **Heroic** (Epic, GOG, Amazon), **Lutris** (Battle.net, EA, Ubisoft) e **itch.io**; Xbox/Game Pass para PC solo en el PC de escritorio. También cuentan los **juegos sueltos** sin tienda (una carpeta).
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
| HowLongToBeat | Duración. **Opcional**: no tiene API y el scraper puede fallar; entonces se muestra "sin datos" (#66). Fuera de F1, para una fase posterior (#151) |
| RAWG | Complemento |
| SteamGridDB | **Imágenes** de la app y de Steam. La cabecera de una saga usa el héroe de su juego icónico más reciente (o del más reciente), y la del juego, el suyo (#178) |
| Steam Web API | Lo que tienes y horas jugadas |
| IsThereAnyDeal · GamerPower · catálogo de Game Pass | Disponibilidad en tiendas, precios y juegos gratis (T2) |

Si dos fuentes no coinciden, se usa una **prioridad fija por campo**. Lo que corriges a mano no se sobrescribe nunca.

## 7. Colección

### 7.1 Qué cuenta como "lo tengo" ✅
- Un archivo en `Juegos\` de un disco.
- Un juego en cualquiera de tus tiendas de PC (Steam con el préstamo familiar, Epic, Xbox, Battle.net). Cuenta como versión de PC.
- Un juego de PC suelto en `Juegos\PC\<Juego>` de un disco (#214); lo que instala un launcher en su propia carpeta se detecta por su tienda (F9).
- En PlayOS, además, lo que está en **Heroic** (Epic, GOG, Amazon), **Lutris** (Battle.net, EA, Ubisoft) e **itch.io**, y los **juegos sueltos** sin tienda (#132).

### 7.2 Detección ✅
1. Escaneo rápido por nombre y TitleID o serial, al abrir la app, al agregar un disco, al terminar una mudanza, con «Escanear» en Colección (#212) y cuando cambia algo en `Juegos\` mientras la app está abierta (#223). En Switch, el TitleID sale solo del nombre del archivo: Otteretro no abre ni descifra los NSP o XCI (#240).
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

- **No** se respaldan aquí los packs de texturas, los binarios de emuladores ni los juegos. Los emuladores y mods se conservan en tu librería privada (§8.1, #239). Alguna versión vieja de un emulador podría no estar ya disponible.
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

### 8.0 Emuladores y perfiles ✅ (encuesta 19, #286 a #298)
- **Muchos a muchos:** una consola puede tener varios emuladores y un emulador puede servir a varias consolas. Cada consola tiene un **emulador base** y cada juego lo usa salvo que le asignes otro; se puede cambiar. Los emuladores se instalan pocas veces y se comparten: no hay uno por juego.
- **Perfiles:** un perfil es la configuración y los mods de un juego en un emulador (o en otra versión del mismo). Un juego puede tener varios, con **uno activo**. Cada emulador tiene un **perfil por defecto** configurable. Al cambiar el activo, se guarda el anterior, se quita del emulador y se carga el nuevo; volver lo restaura. Se eligen y ajustan en la ficha del juego.
- **Instalar:** desde la receta con la versión elegida, desde la URL de una release ya compilada, o subiendo una carpeta o un zip. Queda en su versión; lo instalado a mano se ofrece adoptar.
- **Un port, una versión** (#324 a #327): un port vive en `Ports\<Port>`, sin la versión en la ruta. Instalar, actualizar o reinstalar uno que ya existe reemplaza sus archivos y conserva lo de la persona (partidas y contenido en `_compartido`, la configuración, los mods y los archivos por juego de su receta y todo archivo que no venga en el paquete nuevo), respaldado antes en `_respaldos`. El archivo de cada versión anterior queda en la librería y «Reinstalar» la deja como la instalada. Los perfiles del port usan la versión instalada. Lo que ya tenía varias versiones se junta en la más nueva al abrir la app (#316). Los emuladores siguen con cada versión al lado de la otra.
- **Versiones de un emulador** (#331 a #334, #336 a #338): se pueden tener varias instaladas, una marcada «En uso». Al instalar una nueva se pregunta «¿Pasar los perfiles a <versión>?». Herramientas › Emuladores es una sola lista, con una tarjeta por emulador y sus versiones adentro (sin la sección «Tu librería»); cada versión tiene Usar esta versión, Borrar la instalación (el archivo queda en la librería), Reinstalar y Abrir su carpeta. Arriba van los botones «Instalar un emulador» (abre el formulario) y «Cargar un perfil (.zip)» (con selector de archivo); el texto bajo «Versión» aparece solo si ya hay otra instalada. **Una tarjeta por emulador o port** (#343 a #345): lo instalado y lo encontrado en disco van juntos, en secciones por consola (un emulador de varias consolas aparece en cada una; un port, en la de su juego). La cabecera muestra la versión en uso, su disco y el aviso de versión nueva o «Sin receta», con abrir su carpeta y mover; las demás versiones y sus URL van plegadas en «▾ N versiones más». Los botones de arriba y «Revisar ahora» van en una sola barra, y cada aviso de versión nueva, en la tarjeta de lo suyo.
- **Switch 2** (#339, reemplaza a la #152): se emula en experimental con Eden NX2, Eglantine y Lichii.
- **Partidas al cambiar de emulador:** se copian si el formato es compatible, se transforman si hay forma conocida (con respaldo previo) y, si no, se avisa.
- **Mods:** por URL o subiendo carpeta o zip, donde el emulador los carga según su receta. Antes de diseñarlos se investigan las opciones de modding de cada emulador.
- **Varias ROMs por juego:** la base y sus modificaciones, marcadas; el perfil elige cuál usa. Ninguna ROM va en un perfil compartido.
- **Compartir un perfil:** un .zip con emulador, mods y configuración; «Cargar perfil» lo instala desde el paquete. Sin keys, BIOS, firmware ni juegos.
- **Configurar desde la app:** los ajustes más usados y los mandos de cada emulador, por perfil, según la versión que usa cada juego (la receta los describe por versión); el resto, con un botón que abre la configuración del emulador para ese juego.
- **Emulador base de cada consola** (#291): Eden en Switch (alternativos: Ryubing, Citron y Ryujinx SS4K), Rosalie's Mupen GUI en N64, Azahar en 3DS, uno por consola en el retro, DuckStation, PCSX2, PPSSPP, RPCS3, shadPS4, Vita3K, melonDS, Cemu, xemu, Xenia Canary y Flycast; en PS5, el proyecto más avanzado, aunque inestable.

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

### 8.1.1 Fuentes y tu librería ✅ (#239, se aterriza emulador por emulador al abrir la F5)
- **Otteretro público solo trae recetas** con la fuente oficial y el hash, sin binarios. La descarga de un emulador siempre la solicita la persona.
- **La fuente se elige:** cada persona puede indicar la suya (una carpeta, un drive sincronizado, un repositorio), y Otteretro busca ahí.
- **Tu librería privada** guarda tus emuladores y mods, con versión y hash, para que sigan disponibles si dan de baja un emulador. Guarda **una sola copia**, sin segunda copia local (#335, reemplaza a la #303).
- **Compartir con un amigo** va sin keys, BIOS, firmware ni ROMs, y con cuidado con los mods derivados de un juego. A un desconocido se le recomienda dónde conseguirlos.
- **Conservar el entorno (#242):** tampoco deben perderse las imágenes, las rutas ni el contenido generado. Se define con Herramientas y Respaldo.

### 8.2 Ubicación ✅
Van en las carpetas `Emuladores\`, `Ports\` y `Complementos\` del disco ★ (§5.1) y se pueden mover a otro disco. Los **complementos** (packs de texturas, shaders, ReShade, mods) van junto a su emulador o port y se **instalan y actualizan igual que ellos**.

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
- La actualización la lanzas tú. La app **avisa** dentro de ella (Herramientas, barra lateral e Inicio › Novedades) qué emuladores, ports y complementos tienen versión nueva, y tú eliges qué instalar (#189). Solo avisa de lo que tienes instalado; lo demás se instala desde «Instalar…» (decisión 321). En Herramientas, cada aviso va en la pestaña de su tipo, con un contador, y es una fila que se abre con las notas y los juegos que lo usan; «Omitir esta versión» va en ⋯ (decisiones 320 y 322).
- La versión nueva se instala **al lado** de la anterior. **La vieja nunca se reemplaza** y puedes volver a ella.
- **Migración:** antes de actualizar se hace una copia de seguridad puntual. Después se copian las partidas y la configuración según la receta. Cómo guarda sus partidas cada port se investiga al escribir su receta.

### 8.5 Archivos de sistema ✅
- Las BIOS, keys y firmware **los subes desde la interfaz**. La app no los descarga.
- Quedan en `Sistema\<Consola>\` del disco ★, compartidos por todos los emuladores, **con versiones**. Subirlos desde la interfaz llega con Archivos de juegos (#218).
- La app indica dónde va cada uno, comprueba que estén y avisa si falta alguno.

### 8.6 Contenido instalado dentro del emulador ✅
- Actualizaciones y DLC de Switch, juegos de Wii U (Cemu), paquetes `.pkg` de PS3 y `.cia` de 3DS: **la app los instala sola** en el emulador, según la receta. Los archivos originales siguen en `Juegos\`.
- **PS4:** los `.pkg` no se extraen (#243); se decide con el emulador, en la F5.
- Si no lo consigue, **avisa de qué hay que instalar a mano**.

### 8.7 Herramientas que gestiona la app ✅
- **Steam ROM Manager** es una herramienta más: tiene su receta (tipo `tool`) y vive en `Herramientas\Steam ROM Manager\<versión>` del disco ★ (#274).
- **RetroAchievements**: inicias sesión y la app muestra los logros de cada juego y los activa en los emuladores compatibles.

### 8.8 Mandos ✅
*Reemplazado por la #108:* Otteretro se maneja **completo con mando** sin cambiar la distribución de la interfaz (Xbox, adaptador inalámbrico de Xbox, DualSense/DualShock 4, Switch Pro/Joy-Con). Dentro de los juegos los mandos los sigue gestionando Steam Input o cada emulador.

### 8.9 Linux ✅
*Reemplazado por la #113 y la #114:* Linux es **PlayOS** (§14). Ahí Otteretro se instala con su flake y se actualiza con el sistema, y los emuladores vienen **instalados de base** (nixpkgs y lo que no esté, gestionado aparte). Las recetas siguen teniendo una variante por sistema operativo.

## 9. Archivos de juegos: conseguir, importar y ordenar

Todo lo que le pasa a un archivo de juego hasta quedar en su disco, verificado y como «lo tengo». Es un solo dominio (#218): se cierra completo antes de pasar a Herramientas.

### 9.1 Proceso único ✅
```
Origen ─► Descomprimir ─► Identificar ─► Convertir ─► Ubicar ─► Verificar ─► «lo tengo»
 · descarga (enlaces, torrents)   · carpeta que eliges   · Entrada\ de un disco   · megapack
```
Todo origen termina en la misma importación. Lo que se importa **se mueve**: copiar, verificar y recién entonces borrar el original (#220).

### 9.2 Descargas: un JDownloader dentro de Otteretro ✅
- **Sin programas aparte ni servicios debrid** (#225). Otteretro descarga con la librería .NET `Downloader` (por partes, con cola, y retoma aunque cierres la app) y un **conector por sitio**, con lo más oficial que haya:

  | Sitio | Conector |
  |---|---|
  | Enlaces directos | Sin nada extra |
  | 1fichier | Su API oficial para cuentas premium |
  | Mega | MegaApiClient (MIT) o el SDK oficial de Mega; descifra los enlaces |
  | MediaFire | Su página de descarga (no hay librería mantenida: es el que puede romperse si la página cambia) |

  Un sitio nuevo es un conector nuevo. Los de la primera versión son esos cuatro (#225).
- **Tus cuentas** van en Ajustes › Cuentas y claves, cifradas (§5.6). Se descarga desde tu PC, con tu IP y reutilizando la sesión o la clave.
- **Agregar enlaces** (#227): pegas un texto con muchos enlaces (o lo arrastras) en Descargas o en la ficha de un juego. Otteretro los reconoce, comprueba que estén en línea y su tamaño, junta las partes de un mismo archivo (`.part1.rar`, `.part2.rar`…) en un paquete y muestra la lista antes de empezar. Los enlaces **quedan guardados al instante** en el capturador, como en JDownloader, hasta que los mandas a la cola o los quitas (#245). Con la app abierta, **los enlaces que copias de un sitio con conector entran solos**, con un interruptor para apagarlo y un aviso del sistema que lo confirma; el resto de lo copiado no se guarda (#246, #247).
- **Tus enlaces de cada juego** (#245): cuando se identifica el juego de una descarga, o si la pegaste desde su ficha, sus enlaces quedan en la ficha. Son datos privados: viven en tu base, aparte del catálogo, y no se comparten con la comunidad. Sí van a tu respaldo privado.
- **Se respetan las reglas de cada sitio** (#228): sin cuenta, si el sitio pide esperar, se espera; si pide un captcha, te lo muestra para que lo resuelvas tú; si solo permite una descarga a la vez, se respeta. Nunca se saltan límites ni se comparten cuentas.
- **Descargas a la vez por sitio** (#248): eliges cuántas bajan al mismo tiempo de cada sitio, con el tope de tu cuenta (1fichier: 1 sin premium y 10 con premium). Sitios distintos bajan en paralelo.
- **Calidad** (#249): cada archivo se compara con su tamaño esperado y con los hashes que traiga el paquete (`.sfv`, `.md5`, `.sha*` o `[CRC]` en el nombre). Lo que llega dañado se vuelve a bajar hasta dos veces; un comprimido que no pasa su CRC deja la descarga con error para volver a bajarla, sin borrar nada. Cada archivo dice cómo se verificó, o «Sin verificar». 1fichier se comprueba sin pedir tokens, para no sumar pedidos fallidos a tu cuenta.
- **Conexiones por sitio** (#226): por debajo del máximo de cada sitio, ajustable en Ajustes.
- **Un enlace de un sitio sin conector** (#226): si es directo se baja; si no, Otteretro avisa qué sitio es.
- **Torrents y magnets** (#229) con MonoTorrent (.NET, MIT), en la misma cola. Mientras bajan también comparten, desde tu IP.
- **Pantalla Descargas, como Steam** (#227): la cola con progreso, velocidad, pausa y reanudar; cada paquete elige disco viendo los GB libres (el ★ por defecto, o el de la consola).
- **Solo enlaces tuyos** (P8): Otteretro nunca busca de dónde descargar un juego.

### 9.3 Importar una carpeta y la carpeta de entrada ✅
- **Importar una carpeta** (#218): eliges una carpeta (descargas, otro disco, el PC de escritorio) y pasa por el proceso único.
- **`Entrada\` en cada disco** (#221): lo que dejas ahí se ordena solo. Al estar en el mismo disco, mover es instantáneo.
- Lo que llega solo (Entrada y descargas) **se importa si se identifica con seguridad** (hash o nombre); lo dudoso espera en Revisar (#222).
- Un paquete que se descomprime **sin nada reconocible** queda pendiente en Revisar con sus archivos, con «Reintentar»; al cambiar la versión de Otteretro se reintenta solo (#321, #322). Un `.nup` (volcado de Switch o Switch 2) se guarda tal cual, en la consola de su TitleID (#320).

### 9.4 Megapacks ✅
- **Vista previa:** la app muestra lo que identificó, todo marcado por defecto, con filtros por saga, consola o "está en el catálogo", y tú eliges qué importar.
- **Regiones:** se importa una versión por juego (1G1R). El orden de regiones es configurable y por defecto **va primero la que tiene español**.
- **Extras:** solo se importan **traducciones de fans**. No se importan hacks, betas, demos ni juegos sin licencia.
- **Duplicados:** si ya tienes el juego, se salta, **salvo que el nuevo esté verificado y el tuyo no**. En ese caso se reemplaza.

### 9.5 Descomprimir ✅
- Al terminar un paquete se descomprime solo (#230) con SharpCompress (dentro de la app, sin 7-Zip): zip, 7z y rar, también en varias partes y **con contraseña**, probando una lista de contraseñas tuyas.
- El comprimido se borra después de verificar lo importado.

### 9.6 Identificación ✅
Se prueba en este orden:
1. El origen, por ejemplo la ficha donde pegaste el enlace.
2. El nombre y el TitleID o serial.
3. El hash contra los DAT (No-Intro de libretro y Redump). Si no coincide, se consulta **ScreenScraper** (#218).
4. Los alias aprendidos.
5. **Claude**: si no está seguro, **asigna el juego y lo marca "revisar"**. Hay una lista de pendientes de confirmar.
6. Si Claude tampoco puede proponer nada, el archivo va a "Sin identificar" y la app avisa.

### 9.7 Formatos y verificación ✅
- La regla general es **el formato más liviano**: GameCube y Wii pasan a RVZ; PS1 y PS2 pasan a CHD.
- **Lo que pide el port manda sobre el emulador.** Si un port exige el formato original, se guarda **solo el original**, sin recortes, y esa única copia la usan el port y el emulador. Si lo que tienes es RVZ o CHD, se regenera el ISO completo.
- **Verificar lo convertido** (#231): se verifica el original contra Redump y se guarda su hash **antes** de convertir; un RVZ o CHD que llega ya convertido lo verifica DolphinTool o chdman.
- **Cuándo** (#233): al importar, y con un botón por consola para lo que ya tienes.
- **DolphinTool y chdman vienen en el repositorio** con su licencia (#232, #241); si faltan, se toma el del Dolphin instalado.
- **PS4:** los `.pkg` no se extraen (#243): normalmente implica descifrarlos. Se decide con el emulador, en la F5.

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
- **Aprendizaje automático:** lo que resuelve Claude se guarda solo como dato (un alias, una regla, una receta o una ruta). Hay un **registro** (Revisar › Aprendizajes) donde ves cada cambio con sus juegos y sagas, agrupado por tarea, y puedes deshacerlo o **corregirlo** con una nota: se deshace y la nota queda como regla de la saga para la próxima depuración (#161).
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
- **Tus juegos**: se escanean al abrir la app, al agregar un disco, al terminar una mudanza y con «Escanear»; mientras la app está abierta vigila `Juegos\` y `Entrada\` de cada disco (#223).
- **Carga inicial del catálogo**: **completa**, todas las sagas y juegos, en orden **Nintendo → Sony → Microsoft** (#69). Los scripts bajan primero todos los datos de las APIs. Claude solo juzga, **por lotes de varias sagas por llamada**, para gastar los mínimos tokens.
- **Horario y tope de Claude**: Claude trabaja en segundo plano **de noche** (00:00 a 08:00 por defecto, ajustable; #154) y ahí usa la ventana entera, **sin tope** de uso ni de tareas (#157, #158). La carga inicial **dura días** por los límites de la suscripción (ventanas de 5 h y semanales): la cola se pausa al llegar al límite y **continúa sola** cuando se renueva. Al empezar el horario, la app **lanza sola** la carga si no hay nada de Claude en la cola; lo que no cambió se salta sin gastar (#159). De día corre si lo lanzas con «Ejecutar ahora», que arranca **de inmediato** (#159); con la casilla «Con tope» se queda en el porcentaje elegido de cada ventana (**50 %** por defecto) para no quitarte cuota mientras programas, y sin ella usa la ventana entera (#157, #160).
- **Claude sin horario (#280 a #282; reemplaza lo anterior para Otteretro)**: a Otteretro solo le queda identificar lo que ningún script reconoce (`claude.identify`), y lo hace **al terminar** el escaneo o la importación, a cualquier hora. Solo espera cuando la suscripción llega a su límite, y sigue sola. Se ajusta en **Ajustes › Almacenamiento › Reconocer juegos**: un interruptor «Usar Claude cuando ningún script lo reconoce» (encendido por defecto; apagado, lo no reconocido va directo a Revisar › Sin identificar) y una línea con las llamadas y el costo del mes. No hay pestaña Claude, ni «Ejecutar ahora», ni tope por ventana.
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

### 12.6 Revisión de UI (encuesta 15, #169 a #172)
- **Toda la app:** barra superior fija; en Saga y Juego, el camino en la barra; un solo aviso de claves que faltan; enlaces-botón sin subrayado; portada sin bajar con el color de su consola; menú de fases futuras atenuado con su fase; vacíos con la nutria y el siguiente paso; encabezado de Saga y Juego con el arte del juego.
- **Inicio y Sagas:** «Siguiente en tus rutas» como tarjeta con avance; Revisar solo si hay algo; filtros fijos con el total; tarjeta de saga con insignias y barra de lo que tienes.
- **Saga y Juego:** ruta con pestañas junto al título y pasos unidos; consolas abreviadas («+N»); recalcular en «⋯»; Seguimiento compacto; Versiones y Comparativa en una tabla con barras de 5; Datos y fuentes plegado con lápiz.
- **Colección, Revisar, Ajustes y el resto:** consolas con algo, por empresa; Cuadrícula/Lista con íconos; pestañas subrayadas en toda la app; ruta por aprobar con portadas; Ajustes › Claude en tres bloques; registro de uso con costo; panel de tareas con progreso y ✕; bienvenida con la nutria; menú rápido con más accesos.
- **Ajustes (#175, en F2):** pestañas sin barra, las que no caben en «Más ▾»; sin segundo título; paneles angostos y botones de tamaño estándar en toda la app; Consolas por empresa con interruptor; puesta al día arriba; Claude con horario en una frase y registro compacto con costo; Cuentas y claves en lista con estado en color; Carpeta de datos más clara; Idioma y Acerca de en «General». Con espacio, los bloques de Ajustes van en dos columnas (#184; **reemplazada por la #200**: pestañas arriba, como antes, y una sola columna de contenido centrada con ancho máximo; lo mismo en Herramientas).
- **Menú lateral retraíble (#185):** su botón o Ctrl+B lo retraen a una franja de íconos y lo expanden; se recuerda al reabrir la app.
- **Dónde va (#176):** Ajustes y fluidez en F2; el resto en «F2.5 · Pulido de UI».

### 12.7 Fluidez primero (#173, #174)
**Regla para todo lo que se haga:** el marco de cada pantalla aparece en menos de 100 ms y ninguna consulta pasa de 200 ms con los datos reales; una prueba lo mide y está en la revisión de cada PR. Se logra con esqueletos de la forma real, datos ya vistos al instante y adelantados al pasar por un enlace, bloques y selectores que cargan por su cuenta, consultas con índices o precalculadas, cambios que se ven al tocar y el marco de la app antes que los datos.
- **En concreto (#177):** consultas simples con índices, sin copias guardadas de respuestas de la base (nunca datos viejos); lo que lee el disco (portadas, tamaño de la carpeta) se recuerda; toda lista larga es scroll infinito pedido por tramos de 60 al servidor (Colección, saga «Emulación», Sagas, Buscar, Aprendizajes).

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
| A2 | Poner `prod.keys` y el firmware en Ryujinx SS4K. Hoy no arranca ningún juego de Switch. Cuando exista, la app lo hará desde `Sistema\` del disco ★ (§8.5). |
| A3 | Crear las cuentas y claves cuando toque: Steam Web API, IGDB (Twitch), los sitios de descarga que pagues, ScreenScraper, SteamGridDB, RetroAchievements y las tiendas de PC. |

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
| S7 | Dijiste que lo pesado debe ir donde tú elijas. Puse **todo lo que no es la app** en la misma carpeta de datos: la base de datos, `Herramientas\` y la caché de imágenes (*`Herramientas\` ya no es parte de los datos: #315*). Así migrar de equipo es mover una sola carpeta. La base de datos es liviana y podría quedarse en AppData, pero entonces habría dos sitios que respaldar. |

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
| Máquinas | El portátil de juego; a futuro un PC para la tele y una portátil de mano; una VM de pruebas (#106) |
| GPU del portátil de juego | Se usa la configuración que funciona hoy, **comprobada en el equipo** (#109) 🔬 |
| Rendimiento | GameMode, gamescope, MangoHud, perfil de rendimiento automático, LACT; kernel por defecto; LenovoLegionLinux y OpenRGB (#110) |
| Steam y tiendas | Steam (gamescope, Proton-GE, Remote Play y transferencia local, extest, Steam ROM Manager), Heroic, Lutris, Bottles, itch.io; Discord (Vesktop), OBS, gpu-screen-recorder (#111) |
| Sin soporte en Linux | Se juega en el PC de escritorio por streaming (Moonlight) y Otteretro lo marca (#112) |
| Emuladores y ports | Todo instalado de base (#113): nixpkgs y, lo que no esté, armado por Nix desde las **recetas compartidas** de Otteretro, con todas sus versiones (#135); configuración y partidas, de Otteretro |
| Otteretro | Flake propio, se actualiza con el sistema, datos en `~/Otteretro`, claves en libsecret, GameMode/MangoHud/NVIDIA automáticos (#114, #116) |
| Juegos | SSD interno, USB y copiar desde el PC de escritorio; a futuro un NAS con todas las ROMs (#115) |
| Cómo se trabaja | Fase P0 antes de F1; pruebo por ssh en el portátil de juego y el rebuild lo haces tú (#117) |

## 15. Metodología de trabajo con IA (#118 a #127)

Cómo se trabaja en Otteretro y en tus demás repos personales. Va en un plugin propio de Claude Code, **`otter`** (repo privado `OscarVargas97/claude-skills`). Parte de otras skills y de un workspace anterior, pero es independiente: no se sincroniza con ellos ni toca otros proyectos (#118, #119).

| Tema | Decisión |
|---|---|
| Requerimientos | Las encuestas levantan los requerimientos y de ahí salen las tareas. Skill `requerimientos`: idea → encuesta → decisiones y requisitos → **una solicitud por tema** con sus sub-issues, creados cuando dices que sí. Criterios de aceptación derivados de las respuestas; lo que no queda claro y las preguntas que aparecen al implementar van a una mini-encuesta. Selector para 1 a 3 preguntas, artifact para más (#121) |
| Tareas en GitHub | Solicitud = issue `solicitud`; tareas = sub-issues; estado en un GitHub Project (Pendiente → En desarrollo → En revisión → Hecho, Descartado); etiquetas de tipo y prioridad, `solicitud` y `automatizar`; un milestone por fase; se identifica con el número del issue. Sin Notion (#120) |
| Decisiones y plan | Las decisiones siguen en `historial/decisiones.md` y los issues las citan. El plan vive en GitHub: cada fase es un milestone, sus partes futuras son borradores del Project con el campo `Fase` y la fase en curso tiene su issue «Fase N»; en `docs/` queda solo `plan.md` con el orden de las fases (#122, #143) |
| Reglas para la IA | `AGENTS.md` (y `CLAUDE.md` que lo importa), adaptaciones por repo, el issue de cada tarea para retomar entre sesiones (`## Estado` y comentarios, #144), estado al iniciar sesión, lint después de editar, worktrees (el checkout base no se edita), `docs/README.md` como índice, `docs/review-checklist.md`, mapa funcional y badge de la skill (#123) |
| Guardián | En el plugin, con reglas por repo. En Otteretro bloquea o pide confirmación según la lista de #124. Cambios sin commitear o sin pushear: **avisa, no bloquea** (#124) |
| Workspace | Un espacio de [ia-workspace](https://github.com/OscarVargas97/ia-workspace) agrupa Otteretro y PlayOS en `repos/<repo>/<rama>`, con `work/`, la configuración del guardián y lo que cruza repos (#128) |
| Idioma | Interfaz en español e inglés (#140); código y tablas en inglés; documentación en español (#125, #139, §3) |
| Skills | `requerimientos`, `issue-solver`, `pr-reviewer` (revisa tus PRs antes de fusionar en `dev`), `repo-docs`, docs entre repos, `fase` y `estado`. Un paso a la vez; commit, push y PR solo cuando los pides (#126) |
| Puesta en marcha | Primero el plugin; después se aplica a Otteretro en una rama propia, con milestones de todas las fases e issues de la fase en curso. Se instala a nivel usuario (#127) |

Otteretro adoptó el plugin el 2026-10-16 (rama `chore/metodologia`) y se trabaja desde un workspace con PlayOS (#128): reglas en `AGENTS.md` del repo y del workspace, configuración en el `.claude/otter.json` del workspace, tareas en el [Project otteretro](https://github.com/users/OscarVargas97/projects/2) y un milestone por fase.
