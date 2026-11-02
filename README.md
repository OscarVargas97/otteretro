<p align="center">
  <img src="web/public/nutria.png" alt="La nutria de Otteretro" width="112">
</p>

<h1 align="center">Otteretro</h1>

<p align="center">
  <b>Qué jugar, en qué orden y dónde lo tienes.</b>
</p>

<p align="center">
  Una app de escritorio que junta las sagas que quieres jugar, la colección que tienes repartida entre discos y Steam, y los archivos que llegan a tus discos: los reconoce, los verifica y los deja en su lugar.
</p>

<p align="center">
  <a href="#empezar">Instalar</a> ·
  <a href="#un-recorrido">Recorrido</a> ·
  <a href="#en-qué-punto-está">Estado</a> ·
  <a href="docs/README.md">Documentación</a>
</p>

![Sagas en Otteretro: cada tarjeta con sus juegos, sus consolas y lo que ya tienes](docs/capturas/sagas.png)

## Por qué existe

Una colección de videojuegos suele estar por todas partes: cartuchos y discos en carpetas con nombres a medias, en dos o tres discos, juegos comprados en Steam, ports de PC, emuladores en versiones que nadie recuerda. Y encima está la pregunta de siempre: *si quiero jugar Final Fantasy, ¿por cuál empiezo?*

Otteretro parte de las **sagas**: qué juegos tiene cada una, en qué consolas salieron, en qué orden conviene jugarlos y cuáles ya tienes. Debajo se ocupa de los archivos: los identifica por nombre o por hash, los convierte al formato más liviano y los ordena en tus discos sin perder nada en el camino.

No reemplaza a tus emuladores ni a Steam: les deja la colección en orden.

## Un recorrido

### Una saga, con su ruta

![La saga Final Fantasy con su ruta de la comunidad, paso a paso](docs/capturas/saga.png)

Cada saga muestra sus juegos en una línea de tiempo. Las más conocidas traen además **rutas**: por orden de lanzamiento, por orden de la historia o la que recomienda la comunidad, con la versión sugerida en cada paso y el porqué de ese orden. Sigues una y Otteretro te dice cuál es el siguiente juego.

### Lo que tienes, por consola

![La colección filtrada por SNES, con los juegos que ya tienes](docs/capturas/coleccion.png)

**Escanear** recorre tus discos y marca como «lo tengo» lo que reconoce. **Verificar** calcula el hash de cada archivo y lo compara, sin conexión, con los DAT de No-Intro y Redump. Lo que el nombre y el hash no resuelven lo puede proponer Claude, y queda en **Revisar** para que lo confirmes. Los juegos que tienes en Steam cuentan también, con sus horas.

### Tus descargas, en la misma app

![La cola de descargas con un paquete en partes, cada archivo con su verificación](docs/capturas/descargas.png)

Un gestor de descargas dentro de Otteretro: enlaces directos, 1fichier, Mega, MediaFire y torrents en una sola cola, con pausa y reanudación aunque cierres la app. Cada archivo se compara con su tamaño y con los `.sfv` o `.md5` del paquete, y lo dañado se vuelve a bajar. Al terminar, el paquete se descomprime y se importa solo.

![El capturador de enlaces, con las partes de un mismo archivo agrupadas](docs/capturas/capturador.png)

El **capturador** guarda al instante lo que pegas o copias, junta las partes de un mismo archivo y comprueba que los enlaces estén en línea antes de empezar. Otteretro solo baja **los enlaces que le das**: nunca busca de dónde sacar un juego.

### Cada juego, con su seguimiento

![La ficha de Final Fantasy X, con su seguimiento y sus versiones](docs/capturas/juego.png)

La ficha reúne las versiones de un juego (original, remaster, cada consola), cuáles tienes y dónde están sus archivos. Llevas su estado (pendiente, jugando, terminado, 100 %, abandonado), tu nota y tus apuntes.

> Las capturas salen de una instalación limpia con una biblioteca y descargas de ejemplo, sin portadas descargadas.

## Cómo llega un juego a tu colección

Da igual de dónde venga, todo pasa por el mismo camino:

```text
descarga · carpeta que importas · Entrada\ de un disco · megapack
        │
        ▼
descomprimir ─► identificar ─► convertir ─► ubicar ─► verificar ─► «lo tengo»
```

- **Identificar:** por nombre, TitleID o serial; después por hash contra los DAT; si no, con ScreenScraper o Claude. Lo dudoso espera a que lo revises.
- **Convertir:** GameCube y Wii pasan a RVZ; PS1 y PS2, a CHD. El original se borra solo cuando la copia dio el mismo hash.
- **Ubicar:** cada disco tiene una carpeta `Otteretro` con `Juegos`, `Emuladores`, `Ports`, `Complementos`, `Sistema` y `Entrada`. Lo que dejas en `Entrada` se ordena solo.
- **Mover es copiar, verificar y recién entonces borrar.** Quitar un juego lo manda a la papelera.

Los megapacks se importan con vista previa: una versión por juego, con tu orden de regiones (español primero), y las traducciones de fans sí; hacks, betas y demos, no.

## Empezar

1. Descarga el instalador desde [Releases](https://github.com/OscarVargas97/otteretro/releases).
2. Ábrelo. El asistente te pide la **carpeta de datos** (elige un disco con espacio) y los **discos** donde guardar tus juegos.
3. Listo. Si ya tenías una carpeta de juegos, el asistente la encuentra y ofrece adoptarla.

La app se actualiza sola desde el canal que elijas en **Ajustes › General › Actualizaciones**:

| Canal | Qué trae |
|---|---|
| **Estable** | La última release (hoy, 0.3.12: catálogo, sagas y rutas, seguimiento, discos y Steam). Es el canal por defecto |
| **Pruebas** | Lo de la fase en curso (0.4): el catálogo incluido, Archivos de juegos y Descargas |

> **El catálogo incluido llega con la 0.4.** Mientras siga en Pruebas, la versión Estable arranca sin sagas: para tenerlas desde el primer minuto, instala desde la última prerelease (`Otteretro-dev-Setup.exe`) o cambia a Pruebas.

## Qué necesitas

**Nada para empezar.** Sagas, rutas, seguimiento, colección, importar y convertir funcionan sin cuentas ni claves. Lo demás es opcional y se carga en **Ajustes › Cuentas y claves**:

| Para | Necesitas |
|---|---|
| Tus juegos y horas de Steam | Una clave gratuita de la [Steam Web API](https://steamcommunity.com/dev/apikey) |
| Descargar con tu cuenta | Tu cuenta de 1fichier (premium) o Mega |
| Identificar lo que no está en los DAT | Tu cuenta de [ScreenScraper](https://www.screenscraper.fr) |
| Que Claude proponga lo que nada reconoció | [Claude Code](https://claude.com/claude-code) instalado, con tu suscripción |

Las portadas y fondos no piden clave: llegan con el catálogo y se bajan a medida que aparecen en pantalla.

**Otteretro no trae juegos, BIOS, keys ni firmware**, ni te dice de dónde sacarlos. Tus BIOS y keys las subes tú en **Herramientas › Sistema**.

## Plataformas

- **Windows:** instalador con actualizaciones automáticas. Es donde se usa y se prueba hoy.
- **Linux:** hay un paquete de Nix (`flake.nix`, con módulo de NixOS) que compila y abre la app, pensado para **[PlayOS](https://github.com/OscarVargas97/playos)**. El soporte completo de Linux es la última fase del plan: no hay instalador para otras distribuciones.

Interfaz en español e inglés.

## El catálogo

Las sagas, los juegos y las rutas no los arma tu Otteretro: vienen hechos. El que trae la 0.4 cubre **5.795 sagas y 49.655 juegos** de 24 sistemas de Nintendo, Sony y Microsoft más PC, con **111 rutas** por ahora.

Detrás hay un proyecto hermano, [otteretro-catalogo](https://github.com/OscarVargas97/otteretro-catalogo). Ahí se generan los datos desde IGDB, Claude ayuda a depurar sagas y armar rutas, se eligen las imágenes y todo queda como un archivo por saga, que cualquiera puede corregir con un PR ([cómo](https://github.com/OscarVargas97/otteretro-catalogo/blob/main/CONTRIBUTING.md)). Cada versión se publica como una release. No necesitas instalar nada de ese repo: Otteretro ya trae una versión adentro.

Para pasar a una versión nueva, ve a **Ajustes › General › Catálogo**. Ahí ves las versiones publicadas con sus cambios y, antes de aplicar una, qué toca de tus datos. La aceptas o la rechazas entera, y se verifica su hash antes de aplicarla. También puedes volver a una anterior o seguir un fork.

## Tus datos

| Qué | Dónde |
|---|---|
| Lo tuyo: colección, progreso, notas, enlaces, ajustes | En tu equipo, en la carpeta de datos que elegiste. No se publica |
| El catálogo | Un archivo aparte, sin nada tuyo, que viene de otteretro-catalogo |
| Tus claves y cuentas | En la carpeta de datos, cifradas por el sistema (DPAPI en Windows, el llavero en Linux). No pasan de un equipo a otro |
| Respaldo | Todavía no existe. Está planeado como un repositorio privado tuyo en GitHub |

Otteretro se conecta a GitHub (actualizaciones y catálogo), a las fuentes de imágenes y de DAT, y a los servicios que configures. Cuando Claude identifica un archivo, recibe su nombre, su carpeta y su consola, a través de tu Claude Code.

## En qué punto está

Otteretro es un proyecto personal en desarrollo activo, que se publica a medida que se termina cada fase.

| | |
|---|---|
| **Listo** | Catálogo, sagas y rutas, seguimiento, discos, lo que tienes en Steam |
| **En Pruebas** | Archivos de juegos: escanear, verificar, importar, convertir y descargar |
| **Lo que viene** | Emuladores y ports gestionados · jugar y exportar a Steam con Steam ROM Manager · respaldo · tiendas y precios · Descubrir · Linux y PlayOS |

Todavía sin probar con cuentas y enlaces reales: Mega, MediaFire, ScreenScraper y torrents de internet. Si algo falla, abre un [issue](https://github.com/OscarVargas97/otteretro/issues). El plan vive en los [milestones](https://github.com/OscarVargas97/otteretro/milestones) y lo que cambia en cada versión, en el [CHANGELOG](CHANGELOG.md).

## Desarrollar

C# (.NET 10, ASP.NET Core y SQLite) con una interfaz Next.js, en una ventana Photino, empaquetado con Velopack.

```powershell
dotnet test                       # pruebas
npm --prefix web run e2e          # pruebas de interfaz
./scripts/empaquetar.ps1          # instalador local
```

Empieza por [`AGENTS.md`](AGENTS.md). Los requisitos están en [`docs/REQUISITOS.md`](docs/REQUISITOS.md), la arquitectura en [`docs/arquitectura.md`](docs/arquitectura.md) y el resto, en el [índice de la documentación](docs/README.md), en español.

## Créditos y licencia

- Datos de juegos: [IGDB.com](https://www.igdb.com).
- Hashes: [No-Intro](https://no-intro.org) vía [libretro-database](https://github.com/libretro/libretro-database) y [Redump](http://redump.org).
- [DolphinTool](https://dolphin-emu.org) y [chdman](https://www.mamedev.org) van incluidos con sus licencias, en `src/Otteretro/herramientas`.

Otteretro se publica bajo la licencia [GPL-3.0](LICENSE). Los nombres y marcas de los juegos pertenecen a sus dueños.
