# Requisito: automatización y Claude

- **Estado:** decidido
- **Fecha:** 2026-10-14

## Ejecución manual, proceso automático

- Las actualizaciones (compatibilidad de emuladores, ports nuevos, metadatos) **se lanzan a mano** con un botón. Una vez lanzadas, **se ejecutan solas**.
- Se lanzan a mano porque actualizar un emulador o un port puede obligar a migrar partidas o configuraciones, y eso tiene que pasar cuando tú lo decidas.

## Claude: tu Claude Code, no la API

- **No se usa la API de Anthropic.** Otteretro llama a **tu Claude Code** en modo sin interfaz (`claude -p …`), igual que hace hoy `_Herramientas/Catalogo/buscar-nuevos.cmd`.
- La app prepara el prompt y los datos, ejecuta Claude Code, lee el resultado y lo guarda en la base de datos.
- Los prompts viven en el repositorio y tienen reglas fijas: qué archivos o tablas pueden tocar, cuántos cambios como máximo por ejecución y cómo citar las fuentes.

## Usos de Claude

| Uso | Qué hace |
|---|---|
| Buscar ports y recompilaciones nuevas | Revisa los catálogos y repositorios y propone entradas nuevas. Hoy lo hace `prompt-buscar-nuevos.md`. |
| Proponer sagas icónicas y la mejor versión | Investiga el consenso de foros y listas y propone, con su justificación, qué sagas y juegos son icónicos y qué versión gana en cada criterio. Tú apruebas en la app. |

## Integración del generador actual

`E:\Consolas\_Herramientas\Catalogo` (Node: Recompendium + compatibilidad por emulador + buscador con Claude) **se integra** a Otteretro. Hay que decidir entre:

1. **Portarlo a C#.** Encaja con un stack único y no requiere Node en el equipo.
2. **Dejarlo como herramienta Node** que la app ejecuta. Es menos trabajo, pero suma Node como dependencia.

Recomendación: portarlo a C#, porque son unas 350 líneas y así la app depende solo de .NET y de Claude Code.
