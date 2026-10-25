# Adaptaciones de Otteretro al plugin `otter`

Valen para Otteretro y PlayOS (el registro y el Project de PlayOS se llevan desde acá). Las toma el guardián en cualquier espacio de ia-workspace que tenga este repo. El guardián inyecta la sección `## Todas` y la de la skill que se carga. **Si algo de acá contradice a la skill, gana esto.** Cada sección es corta: se inyecta entera.

## Todas

- Las rutas de documentos (`docs/...`) son de `repos/otteretro/<rama>/`, salvo que digan "del workspace".
- Antes de proponer o implementar en Otteretro: `docs/REQUISITOS.md` (única fuente vigente), `docs/arquitectura.md` y el milestone de la fase en curso (qué trae y «Listo cuando»).
- GitHub de Otteretro: repo `OscarVargas97/otteretro`; Project 2 (`PVT_kwHOAp1b0c4BnZq6`), campo `Status` `PVTSSF_lAHOAp1b0c4BnZq6zhl-elE` con opciones Pendiente `f75ad846`, En desarrollo `47fc9ee4`, En revisión `9008e4ba`, Hecho `98236657`, Descartado `a3fd7d53`; campo `Fase` `PVTSSF_lAHOAp1b0c4BnZq6zhm-VyU` con P0 `7f0871bd`, D1 `ab429c9a`, F1 `b1a7c269`, F2 `e725d8ba`, F3 `45543992`, F4 `128f3f39`, F5 `d9a232a5`, F6 `d51e0cb7`, F7 `0f150735`, F8 `ba7a8281`, F9 `f67dec22`.
- GitHub de PlayOS: repo `OscarVargas97/playos` (público, rama `main`); Project 3 (`PVT_kwHOAp1b0c4Bna16`), campo `Status` `PVTSSF_lAHOAp1b0c4Bna16zhl_knk` con opciones Pendiente `f75ad846`, En desarrollo `47fc9ee4`, En revisión `fcdd944a`, Hecho `98236657`, Descartado `8b020756`. Mismas etiquetas que Otteretro y milestone `P0 · PlayOS y Linux`. Sin datos personales en el repo (invariantes de workos).
- En Otteretro la rama por defecto en GitHub es `dev` (desde el 2026-10-16, mientras nada vaya a `main`, #101): `Closes #n` en el PR cierra el issue al fusionar. Después pásalo a `Hecho` en el Project.
- Idioma: identificadores en inglés (#125); todo lo demás en español neutro.

## requerimientos

- Numeración: la próxima encuesta es la siguiente al último issue «Encuesta NN» de Otteretro, y las decisiones siguen la numeración de `docs/historial/decisiones.md`. Las decisiones de PlayOS también se registran ahí (es el registro único del proyecto) hasta que se decida otra cosa.
- El registro de una encuesta va en un worktree de Otteretro (`make task R=otteretro B=docs/encuesta-NN-<tema>`) y entra a `dev` por PR.
- Etiqueta `automatizar`: los issues que la app crea sola (§11.5 de los requisitos) son origen de requerimientos; se tratan como cualquier idea.

## issue-solver

- Las tareas de una fase se trabajan en la rama de la fase (`fase/N-nombre`, worktree `repos/otteretro/fase-N-nombre`): un commit por tarea con `Refs #n` y un solo PR de la fase a `dev`. Una tarea fuera de fase va en su propia rama.
- Antes de cada commit: `dotnet build` y `dotnet test`; si se tocó `web/`, `npm --prefix web run revisar` (antes, `npm ci` en `web/` del worktree). Si cambió la API, `cd web && npm run tipos`.
- Datos del usuario: mover es copiar, verificar por hash y recién entonces borrar. Nunca se borran versiones viejas de emuladores ni actualizaciones de juegos.
- Claude en runtime solo con `claude -p`; nunca la API de Anthropic.

## fase

- Fases de Otteretro: F0 (cerrada), P0 · PlayOS y Linux (#137; solicitudes otteretro#7 y playos#1, más workos-personal#1), D1 · Diseño (#138), F1 a F9. Milestones con el mismo nombre. P0 usa la rama `fase/p0-playos` en otteretro y en playos; su issue de fase es otteretro#25 (lleva el estado) y playos#7.
- El plan vive en GitHub (#143): las partes de las fases futuras son borradores del Project 2 con el campo `Fase`. No hay «Dónde vamos» que actualizar: el estado de la fase está en su issue.
- Las acciones de la persona de cada fase (cuentas, claves, A3) se piden al abrirla.

## pr-reviewer

- Checklist de Otteretro: `docs/review-checklist.md`.

## repo-docs

- Mapa funcional de Otteretro: `docs/mapa/README.md` (pantalla → endpoint → módulo → tablas o archivos). Se actualiza en el mismo PR que el código; el renombrado al inglés (#4) también lo actualiza.
