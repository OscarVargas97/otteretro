# Requisito: ingesta de juegos (descargas y megapacks)

- **Estado:** decidido para las descargas. Los megapacks tienen detalles pendientes en [temas-abiertos.md](temas-abiertos.md).
- **Fecha:** 2026-10-14

## Un solo proceso para todas las entradas

Todo juego nuevo pasa por el **mismo proceso**, venga de donde venga:

```
Origen ─► Extraer ─► Identificar ─► Convertir ─► Ubicar en la biblioteca ─► Marcar "lo tengo"
 · enlaces (JDownloader)
 · megapack o carpeta local
```

El objetivo es **intervenir lo mínimo posible**. Solo se te pregunta cuando la app no puede decidir.

## Origen 1: enlaces con JDownloader

- **Conexión:** se usa la **API de MyJDownloader**. Necesita una cuenta, pero permite ver el progreso y pausar descargas.
- **Enlaces:** los pegas en la ficha de un juego, o importas una lista o un archivo con muchos. En las listas, **Claude identifica a qué juego corresponde cada enlace**.
- **Destino:** al pedir la descarga eliges la biblioteca. La app muestra **los GB libres** de cada una. La descarga, la descompresión y la organización ocurren **en esa biblioteca**, en su carpeta de entrada.
- La app solo usa los enlaces que tú le das. Nunca busca de dónde descargar juegos.

## Origen 2: megapacks y carpetas locales (nuevo)

- Puedes darle a la app un **megapack**: un archivo o una carpeta con muchos juegos, de una o varias consolas.
- La app lo procesa como cualquier otro origen: lo **identifica, ordena y organiza** en la biblioteca, con el mismo proceso.
- Detalles abiertos: copiar o mover, qué importar, variantes regionales y duplicados. Ver [temas-abiertos.md](temas-abiertos.md).

## Identificación

1. **Desde el origen:** si el enlace se pegó en una ficha, el juego ya se sabe.
2. **Por nombre y TitleID o serial.**
3. **Por hash**, contra los DAT de No-Intro y Redump.
4. **Con Claude**, para los casos ambiguos, siempre a través de tu Claude Code.
5. Si nada de eso funciona: el archivo va a **"Sin identificar"** y la app avisa. Es el último recurso; la idea es resolverlo durante el proceso.

## Conversión de formatos

- **Siempre se prefiere el formato más liviano:**
  - Se descomprimen `.zip`, `.7z` y `.rar`.
  - Los ISO de GameCube y Wii pasan a **RVZ**.
  - Los juegos de PS1 y PS2 pasan a **CHD**.
- **Excepción:** cuando un port necesita el juego **en su formato original y sin modificar** (por ejemplo, un ISO completo), manda el port. Ese requisito está en la receta de cada port.
- Después de descomprimir y verificar, **se borra el comprimido**.

## Steam como origen de "lo tengo" (nuevo)

Los juegos de tu **biblioteca de Steam** no pasan por este proceso, pero también cuentan como juegos que tienes: aparecen en su saga como **versión PC**. Ver [catalogo.md](catalogo.md#versión-de-pc).
