# Documentación de Otteretro

## Empieza por acá

| Documento | Para qué |
|---|---|
| [`REQUISITOS.md`](REQUISITOS.md) | **Única fuente vigente** de requisitos. Se lee antes de diseñar o programar |
| [`arquitectura.md`](arquitectura.md) | Piezas, módulos, modelo de datos, recetas y flujos (#92 a #99) |
| [`pantallas.md`](pantallas.md) | Qué muestra cada pantalla, sus acciones y estados; enlaza el sistema de diseño y el prototipo |
| [`plan.md`](plan.md) | El plan por fases, para la persona |
| [`plan-implementacion.md`](plan-implementacion.md) | El detalle técnico de cada fase, para la IA que programa: qué leer, cómo se ejecuta, tareas y tareas no planificadas |
| [`review-checklist.md`](review-checklist.md) | Errores propios de este stack que revisa `otter:pr-reviewer` |
| [`mapa/`](mapa/README.md) | Mapa funcional: qué pantalla o comando llama a qué endpoint, módulo y tablas. Se actualiza con el código |

## Historial

| Documento | Para qué |
|---|---|
| [`historial/decisiones.md`](historial/decisiones.md) | Registro de decisiones. Solo crece: lo que cambia se reemplaza con una entrada nueva |
| [`historial/respuestas/`](historial/respuestas/) | Respuestas originales de cada encuesta (`NN-nombre.json`) |
| [`historial/v1-por-tema/`](historial/v1-por-tema/) | Primera versión de los requisitos, por tema, antes de consolidarlos |

## Estudios (foto a una fecha)

| Documento | Para qué |
|---|---|
| [`investigacion.md`](investigacion.md) | Investigación técnica previa al desarrollo (T1 a T7), 2026-10-14 |
| [`viabilidad.md`](viabilidad.md) | Revisión de viabilidad de los requisitos, 2026-10-14 (encuesta 8) |

## Convenciones

- **Vivos o foto**: los documentos de "Empieza por acá" están vivos y se actualizan junto con el cambio que los afecta. Los estudios son una foto de su fecha: no se actualizan, se reemplazan por uno nuevo.
- **Trabajo en curso**: planes, notas, auditorías y traspasos de una tarea van en `work/<rama>/` del workspace (con su `ESTADO.md`), no acá.
- **Tareas y avance**: en GitHub (issues, sub-issues, el [Project](https://github.com/users/OscarVargas97/projects/2) y un milestone por fase), no en estos documentos.
- Un documento nuevo se agrega a este índice en el mismo PR.
