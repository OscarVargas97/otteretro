# Requisito: bibliotecas

- **Estado:** decidido (cuestionario de requisitos, 2026-10-14)
- **Fecha:** 2026-10-14

## Idea

El **sistema** (Otteretro) y las **bibliotecas** (emuladores, ports y juegos) están separados.

- El sistema es una app portable que puede vivir en cualquier carpeta.
- Una biblioteca es una carpeta en cualquier disco. **Se crea desde la app** y la app la recuerda.
- No hay rutas fijas a E: o D:. La app sabe dónde están sus bibliotecas porque las registra ella misma, y las reencuentra aunque cambie la letra del disco o el punto de montaje en Linux.

```
<repositorio>\                 ← repositorio de desarrollo
<cualquier sitio>\Otteretro\   ← build portable lista para usar
   Otteretro(.exe)
   data\otteretro.db           ← registro de bibliotecas + catálogo (SQLite)

E:\Consolas\                   ← biblioteca de JUEGOS "Principal" (cualquier nombre y ruta)
   otteretro-biblioteca.json   ← marcador (tipo: juegos)
   _Entrada\                   ← descargas y megapacks que se están procesando
   GameCube\<Juego>\…
   Switch\<Juego>\…

D:\Juegos\Retro\               ← biblioteca de JUEGOS "Disco D"
   otteretro-biblioteca.json
   _Entrada\
   PS2\<Juego>\…

E:\Herramientas\               ← biblioteca de HERRAMIENTAS (cualquier ruta)
   otteretro-biblioteca.json   ← marcador (tipo: herramientas)
   Emuladores\<Consola>\<Instalación>\<versión>\   ← ej. Switch\Ryujinx SS4K\1.2.0\
   Ports\<Consola>\<Port>\<versión>\               ← ej. GameCube\Dusklight\2.0.3\
   Sistema\<Consola>\<archivo>\<versión>\          ← BIOS, keys y firmware con control de versiones
```

## Tipos de biblioteca

| Tipo | Contiene | Cuántas |
|---|---|---|
| **Juegos** | Los juegos, por consola, y una carpeta `_Entrada` para la ingesta (ver [ingesta.md](ingesta.md)) | Las que quieras, en cualquier disco |
| **Herramientas** | Emuladores, ports y archivos de sistema, compartidos por todas las bibliotecas de juegos (ver [emuladores.md](emuladores.md)) | Por definir (ver [temas-abiertos.md](temas-abiertos.md)) |

Cambio respecto de la estructura actual: hoy los emuladores y ports están dentro de cada consola (`GameCube\Emuladores\Dolphin`). Al adoptar `E:\Consolas`, la app los **mueve** a la biblioteca de herramientas.

## Marcador de biblioteca

En la raíz de cada biblioteca hay un archivo `otteretro-biblioteca.json`:

```json
{ "id": "7f3c…-uuid", "nombre": "Principal", "creada": "2026-10-14", "formato": 1 }
```

- El `id` identifica la biblioteca para siempre. El nombre y la ruta pueden cambiar.
- El marcador es lo único que el sistema escribe en la raíz de la biblioteca.

## Operaciones

| Operación | Qué hace |
|---|---|
| **Crear** | Eliges una carpeta (vacía o nueva) y un nombre. La app escribe el marcador y la registra. Las carpetas de cada consola se crean cuando se añade el primer elemento, no por adelantado. |
| **Adoptar** | Registras una carpeta que ya tiene el marcador (por ejemplo, un disco conectado a otro PC) o una con la estructura `<Consola>\{Emuladores,Ports,Juegos}` pero sin marcador (por ejemplo, `E:\Consolas` actual). Si no tiene marcador, la app lo crea. |
| **Renombrar** | Cambia el nombre en el marcador y en el registro. |
| **Quitar** | La saca del registro. **Nunca borra archivos.** |
| **Mover / migrar** | Desde la app se puede mover una biblioteca entera, una consola o un juego a otra ruta o a otra biblioteca. La app copia, verifica, actualiza la base de datos, **reescribe las rutas internas** de los emuladores y ports afectados y, al final, borra el origen. |
| **Reconectar** | Al arrancar, la app comprueba la última ruta conocida de cada biblioteca. Si no encuentra el marcador con su `id`, la marca **desconectada** (disco desenchufado) y ofrece *buscarla* (recorre las raíces de los discos y los puntos de montaje, hasta 2 niveles, buscando el marcador) o *indicar la ruta a mano*. |

## Cómo se guardan las rutas

- En la base de datos, todo archivo se guarda como **(id de biblioteca, ruta relativa con `/`)**. Ejemplo: `(7f3c…, GameCube/Juegos/The Legend of Zelda Twilight Princess/…iso)`.
- La ruta absoluta se arma solo al lanzar un juego o al exportar a Steam ROM Manager, a partir de la ruta actual de la biblioteca.
- Si una biblioteca está desconectada, sus juegos aparecen en el catálogo como "no disponibles" pero no desaparecen. Al exportar a Steam se saltan o se avisa.

## Reglas

- Una consola puede estar repartida entre varias bibliotecas de juegos. La app las muestra juntas.
- **Mover y migrar** sigue siempre el mismo orden: copiar, verificar por hash, actualizar la base de datos y las rutas internas, y recién entonces borrar el original. Se puede mover una biblioteca entera, una consola, un juego o un emulador o port.
- Un juego que aparece en dos bibliotecas a la vez (con el mismo hash o TitleID) se marca como **duplicado**, no como dos juegos distintos.
- El emulador lanza los juegos de cualquier biblioteca: un emulador en E: puede abrir una ISO de D:.
- Los emuladores se configuran **portables** para que sus partidas, mods y texturas viajen con la biblioteca de herramientas, como ya se hace con Dolphin (`portable.txt`), Ryujinx (`portable\`) y Dusklight (`Datos\`).
- Todos los nombres de carpeta de consola salen de la lista de [consolas.md](consolas.md): son fijos y sin caracteres raros.

## Migración desde lo actual

- `E:\Consolas` se **adopta** como la primera biblioteca. Hoy ya tiene la estructura `GameCube\` y `Switch\`.
- Lo que hay en `E:\Consolas` y no es contenido de la biblioteca sale de ahí:
  - `_Herramientas\Catalogo` (el generador Node de ports y compatibilidad) se integra al repositorio de Otteretro.
  - `Lista de ports.md` deja de ser necesario cuando el catálogo viva en la app.
- Carpetas sobrantes (`E:\Roms`, `E:\Emuladores`, `D:\Roms`): se aprobó borrarlas. Las tienes que borrar tú, porque la protección de Claude Code no permite borrar carpetas en la raíz de un disco. `E:\Emuladores` solo tiene cachés de compatibilidad que bajó la sesión anterior.

## Rutas internas de emuladores y ports

**Decidido: la app las reescribe sola.** Cuando una biblioteca, consola o juego cambia de ruta, la app actualiza los archivos de configuración que guardan rutas absolutas. Por ejemplo, `backend.isoPath` en `config.json` de Dusklight o `ISOPath0` en `Dolphin.ini`. Cada emulador o port tiene un adaptador que sabe qué claves tocar.

## Instalación de emuladores y ports

**Abierto.** Está en [temas-abiertos.md](temas-abiertos.md#1-instalación-y-actualización-de-emuladores-y-ports).
