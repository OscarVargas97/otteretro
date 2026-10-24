# Requisito: catálogo de sagas

- **Estado:** decidido. Las cronologías y la versión de PC tienen detalles pendientes en [temas-abiertos.md](temas-abiertos.md).
- **Fecha:** 2026-10-14

## Alcance

- El catálogo debe tener **la mayor cantidad de juegos posible**, siempre con su **saga identificada**. Los juegos destacados que no pertenecen a ninguna saga también entran.
- **Todo lo que tienes aparece siempre**, aunque no sea icónico.
- Para cada juego hay que saber **si existe versión de PC** (ver [Versión de PC](#versión-de-pc)).

## Se guarda por consola, se muestra por saga

- **Almacenamiento:** los archivos se guardan por consola dentro de cada biblioteca. Ver [bibliotecas.md](bibliotecas.md).
- **Vista, gestión y Steam:** todo se organiza **por saga**. Una saga reúne todas sus entregas, en todas sus consolas y en PC.
- Los juegos sin saga se agrupan en la categoría **"Emulación"** de Steam.

Modelo: **Saga → Juego (entrega) → Versión (consola o PC, edición, remaster o remake) → Origen (archivo en una biblioteca, o tu biblioteca de Steam)**.

## Versiones, remasters y remakes

- **Todo es una versión del mismo juego**, también los remakes. Por ejemplo, Pokémon Ruby (GBA) y Omega Ruby (3DS) son dos versiones de un mismo juego.
- **Quién lo clasifica:** IGDB etiqueta cada versión como original, remaster o remake; Claude lo revisa y tú puedes corregirlo.
- **Vista:** una tarjeta por juego, con sus versiones desplegables.
- **Steam:** puedes marcar **varias versiones** de un mismo juego para Steam. Como el nombre lleva la consola, no chocan.
- **Comparación:** la tabla con los 4 criterios incluye todas las versiones: original, remasters y remake.

## Mejor versión

Cuatro criterios que se muestran **por separado**, sin un veredicto único: **contenido**, **jugabilidad hoy**, **técnico** y **original**.

## Cronologías y rutas de juego (nuevo)

Para cada saga, la app muestra **rutas** que te ayudan a decidir qué jugar y en qué orden:

- **Orden de lanzamiento.**
- **Orden de la historia** (cronología interna).
- **Ruta recomendada por la comunidad.**

En cada paso de una ruta, la app indica **qué versión recomienda la comunidad** (el remaster porque mejora la experiencia, o el original) y por qué. Las rutas las prepara Claude a partir del consenso (Reddit, ResetEra, wikis de la saga) y siempre cita las fuentes. Los detalles están en [temas-abiertos.md](temas-abiertos.md).

## Versión de PC (nuevo)

- **PC es una plataforma más** dentro de la saga. Por ejemplo, God of War (2018) tiene versión PS4 y versión PC.
- **Los juegos de tu biblioteca de Steam cuentan como que los tienes.** Si tienes Kingdom Hearts en Steam, en la saga aparece como **"lo tengo · versión PC"** y no hace falta conseguirlo para un emulador.
- Si un juego tiene versión de PC pero no la tienes, se indica que existe.
- Cómo se lee la biblioteca de Steam, qué prioridad tiene la versión de PC y otras tiendas: ver [temas-abiertos.md](temas-abiertos.md).

## Icónico, mis gustos y Descubrir

| Eje | Cómo funciona |
|---|---|
| **Icónico** | Claude reúne el consenso de **Reddit, ResetEra, listas y rankings, notas (OpenCritic/Metacritic) y ventas**. Propone sagas y juegos con justificación y fuentes, y tú los apruebas. |
| **Mis gustos** | Marcas tus **sagas favoritas** y **puntúas los juegos que has jugado**. Tus **horas en Steam** también cuentan como señal. |
| **Descubrir** | **Claude** te recomienda sagas por afinidad con tus favoritas y explica el porqué. Siempre a través de tu Claude Code, nunca de la API. |

## Emulador, port o PC por juego

- La elección es **automática** y la puedes cambiar a mano en cada juego. Los valores de la regla también son editables.
- Cada consola tiene un **emulador predeterminado**, y cualquier juego puede tener un emulador o port **específico**, con su propia configuración. Ver [emuladores.md](emuladores.md).

## Fuentes de datos

| Fuente | Uso |
|---|---|
| IGDB | Base: franquicias, versiones, remaster o remake, plataformas e ids de Steam |
| ScreenScraper | Identificación por hash y medios |
| Wikidata | Relaciones entre sagas y juegos, fechas |
| OpenCritic / Metacritic | Notas, que alimentan lo icónico |
| HowLongToBeat | Duración |
| RAWG | Series y plataformas, como complemento |
| SteamGridDB | **Imágenes** de la app y de Steam (el mismo arte) |

**Conflictos entre fuentes:** cada campo tiene una fuente prioritaria y cualquier dato se puede corregir a mano. Lo que corriges a mano nunca se vuelve a sobrescribir.
