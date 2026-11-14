# Plan por fases

- **Orden (#138, #186, #215):** **P0 PlayOS y Linux → D1 Diseño → F1, F2, F2.5, F3** y, desde la F4, **por dominios** (#215): cada dominio se cierra de punta a punta antes de abrir el siguiente. **Archivos de juegos** (la F4 ampliada: discos, importar, descargas, conversión y verificación) **→ Herramientas → Jugar (con Steam) → Respaldo → Tiendas → Descubrir → L** (Linux y PlayOS, al final). Antes de abrir un dominio se hace una encuesta del dominio entero y se diseñan sus pantallas; al cerrarlo se reevalúa el diseño y lo que salga entra en la misma fase (#393). Una fase se cierra probada con tus datos reales (#216, #237). Desde F1, cada pantalla nueva se hace navegable con mando.
- **Dónde está el plan (#143):** en GitHub. Cada fase es un [milestone](https://github.com/OscarVargas97/otteretro/milestones) con lo que trae, lo que se te pide y su «Listo cuando». Lo que viene en cada fase son borradores del [Project otteretro](https://github.com/users/OscarVargas97/projects/2) con el campo `Fase` (vista «Plan», filtro `fase:F1`), que se editan o se borran mientras la fase no empieza. La fase en curso tiene su issue «Fase N» con el estado.
- **Plan anterior:** hasta el 2026-10-23 el plan completo vivía en este archivo y en `plan-implementacion.md` ([última versión](https://github.com/OscarVargas97/otteretro/blob/c2b5f1a/docs/plan-implementacion.md)).

## Cómo se trabaja cada fase

1. Al abrir una fase, sus borradores se revisan y pasan a solicitudes y sub-issues en GitHub (skill `otter:fase`, con una encuesta solo para lo que dejan abierto). Se programa en su rama **`fase/N-nombre`**, creada desde `dev`, enlazada al issue de la fase.
2. La fase está **lista** cuando se cumple el «Listo cuando» de su milestone y pasan sus pruebas (A4). Entonces se abre un pull request y se fusiona en **`dev`**.
3. **Por ahora nada va a `main`** (#101): todas las fases se acumulan en `dev`. Cuando decidas publicar, se fusiona `dev` en `main` y GitHub Actions publica la release (A6).
4. Si durante una fase aparece una decisión que no está en los documentos, se te pregunta antes de seguir y se registra en `decisiones.md`.

Las **acciones tuyas** de cada fase (cuentas y claves, A3) se piden al empezarla: están en la descripción de su milestone.

## Después

- Un PC para la tele y una portátil de mano con PlayOS (#106).
- El NAS con todas las ROMs (#115): las bibliotecas en red ya lo admiten desde F3.
- Mejoras que vayan saliendo como issues "automatizar".
