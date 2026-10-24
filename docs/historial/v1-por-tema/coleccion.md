# Requisito: mi colección

- **Estado:** decidido
- **Fecha:** 2026-10-14

## Qué cuenta como "lo tengo"

| Origen | Ejemplo |
|---|---|
| Un archivo en una biblioteca | El ISO de Twilight Princess en `E:\Consolas` |
| Tu biblioteca de **Steam** | God of War o Kingdom Hearts en Steam, que aparecen como **versión PC** |

## Detección de archivos

1. **Escaneo rápido** por nombre de archivo y TitleID o serial.
2. **Verificación por hash** bajo demanda, contra No-Intro y Redump. Los hashes quedan guardados.

## Actualizaciones y DLC

- Se **detectan y muestran** el juego base (BASE), las actualizaciones (UPD) y los DLC.
- **Nunca se borran las versiones viejas.**

## Juegos que no tengo

- Se marcan como **"no lo tengo"**.
- Para conseguirlos hay dos vías: descarga con JDownloader o importación de un megapack. Las dos usan el mismo proceso. Ver [ingesta.md](ingesta.md).

## Tiempo de juego

- La app **no lo registra**: lo registra Steam.
- La app **muestra** el tiempo jugado y la última partida que guarda Steam. Esas horas también sirven como señal de tus gustos.

## Partidas

- No hay copias de seguridad generales. Solo se hace una copia puntual antes de actualizar un emulador o un port (ver [emuladores.md](emuladores.md#actualización)).
