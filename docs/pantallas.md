# Especificación de pantallas

- **Fecha:** 2026-10-14
- **Para qué sirve:** es la base para diseñar en **Claude Design**. Cada pantalla indica qué muestra, qué acciones tiene y qué estados contempla. No define colores ni disposición exacta: eso sale del sistema de diseño.
- **De dónde sale:** de [REQUISITOS.md](REQUISITOS.md), sobre todo §12. Cada punto lleva entre paréntesis la sección o decisión que lo respalda.
- **Decisiones abiertas:** las marcadas con **❓Dn** se cierran en la [encuesta 9](https://claude.ai/artifact/XsFAKMCB683zcsdRzmZWLR). Mientras tanto, se diseña con la opción recomendada.

---

## 0. Elementos comunes

### 0.1 Estructura de la ventana (§12.3)
```
┌──────────┬───────────────────────────────────────────────┐
│ [nutria] │  Barra superior: título · búsqueda ❓D1 · tareas │
│ Inicio   ├───────────────────────────────────────────────┤
│ Sagas    │                                               │
│ Colección│               Contenido                       │
│ Descubrir│                                               │
│          │                                               │
│ ──────── │                                               │
│ Descargas│                                               │
│ Herram.  │                                               │
│ Steam    │                                               │
│ Ajustes  │                                               │
└──────────┴───────────────────────────────────────────────┘
```
- **Barra lateral:** Inicio, Sagas, Colección y Descubrir arriba. Descargas, Herramientas, Steam y Ajustes abajo. Cada entrada puede mostrar un **contador**: pendientes de revisar, descargas activas, versiones nuevas o exportación pendiente.
- **Acento de color:** toma el color dominante de la portada de la saga o del juego que estás viendo. Fuera de una saga o un juego, se usa el acento base (§12.2).
- **Tema:** oscuro, con portadas grandes y densidad mixta (§12.2).

### 0.2 Panel de tareas (§12.3)
- Un botón en la barra superior con un **indicador de actividad**. Al pulsarlo se abre un panel lateral.
- Lista de tareas: **en curso** (con progreso y botón cancelar si se puede), **en cola**, **pausadas** (por ejemplo, Claude al llegar al límite de la suscripción, con la hora de reanudación) y **terminadas o con error** (con reintentar y ver detalle).
- Tipos de tarea: escaneo, verificación por hash, descarga, ingesta, conversión, mover biblioteca, instalar o actualizar emulador, Claude, respaldo, exportar a Steam y refrescar tiendas.
- Al terminar una tarea larga llega una **notificación del sistema** (§12.3). El respaldo de partidas tras jugar va **en silencio** y solo se ve si falla (T7).

### 0.3 Tarjeta de juego (componente)
- Portada (SteamGridDB), nombre, consola o consolas, año.
- **Insignias de estado:** lo tengo · versión PC · en Game Pass · verificado · revisar · no disponible (biblioteca desconectada) · me falta.
- Progreso en la ruta: pendiente, en curso, terminado o abandonado.
- **Acciones rápidas** al pasar el ratón: Jugar (si lo tienes) y abrir la ficha.

### 0.4 Tarjeta de saga (componente)
- Arte de la saga, nombre, cantidad de juegos, cuántos tienes y tu avance en la ruta elegida.
- Marca de **favorita** (§6.7).

### 0.5 Estados que toda pantalla contempla
| Estado | Qué se muestra |
|---|---|
| Vacío | La **nutria** con un mensaje y la acción que lo resuelve (§12.2). Ejemplo: "Aún no hay bibliotecas. Crear o adoptar una". |
| Cargando | Esqueletos de las tarjetas, sin spinners de página completa |
| Error | Qué falló, en español, con reintentar. Las fuentes opcionales que fallan (HowLongToBeat, Game Pass) muestran "sin datos" sin bloquear nada (#66) |
| Sin conexión | Lo local funciona; lo que necesita internet se marca como no disponible |

### 0.6 Búsqueda ❓D1
- Recomendado: **búsqueda global** en la barra superior (atajo `Ctrl+K`) que encuentra sagas, juegos, emuladores y ajustes.

---

## 1. Primer arranque (asistente)

Aparece solo la primera vez, o si la carpeta de datos no está donde se esperaba (P1, §5).

| Paso | Contenido | Acciones |
|---|---|---|
| 1. Bienvenida | Logo y nutria. Qué hace la app, en tres líneas | Empezar |
| 2. Carpeta de datos | Explica qué guarda: base de datos, Herramientas y caché (S7). Muestra el espacio libre de cada disco | Elegir carpeta nueva · **Usar una carpeta existente** (reinstalación o migración, §7.6) |
| 3. Respaldo | Token de GitHub (#68) y repositorio `otteretro-datos` | Pegar token · Crear el repositorio · **Restaurar desde el respaldo** (PC nuevo) · Saltar por ahora |
| 4. Bibliotecas | Detecta `E:\Consolas` con la estructura antigua (§5.5) | **Adoptar** · Crear nueva · Saltar |
| 5. Steam | Cuenta detectada en `loginusers.vdf` (§10) y clave de la Web API | Confirmar cuenta · Pegar clave · Iniciar sesión para el préstamo familiar (T1) · Saltar |
| 6. Resto de claves | Lista de servicios (§5.6, A3): IGDB, SteamGridDB, ScreenScraper, RetroAchievements, MyJDownloader, IsThereAnyDeal, Epic. Cada uno con su enlace "cómo conseguirla" | Pegar cada clave · Saltar (se puede hacer después en Ajustes) |
| 7. Carga inicial | Explica que la carga completa dura días y va Nintendo → Sony → Microsoft (#69) | **Empezar la carga** · Más tarde |

- Lo que se salta queda como **aviso en Inicio** hasta que se completa.

---

## 2. Inicio (§12.4)

Secciones, en este orden ❓D2:

1. **Continuar:** el **siguiente juego** de cada ruta que sigues (§6.5), en tarjetas grandes con botón Jugar, o "Conseguirlo" si no lo tienes.
2. **Revisar:** contadores de identificaciones dudosas, sin identificar y aprendizajes de Claude recientes. Cada uno lleva a la pantalla Revisar (§2.1).
3. **Novedades:** ports nuevos, cambios de compatibilidad, versiones nuevas de emuladores, ports y complementos, y **bajadas de precio** (#57, #75).
4. **En curso:** descargas y tareas activas (resumen del panel de tareas).
5. **Avisos de configuración:** pasos del asistente que se saltaron, credenciales vencidas, biblioteca desconectada, BIOS o keys que faltan (§8.5), LFS bloqueado (#70).

### 2.1 Revisar ❓D3
Cola de lo que la app decidió con dudas (P6, §9.4):

| Pestaña | Cada elemento muestra | Acciones |
|---|---|---|
| Identificaciones dudosas | Archivo, juego asignado, confianza y por qué | Confirmar · Cambiar juego (buscador) · Ignorar archivo |
| Sin identificar | Archivo, consola detectada y tamaño | Asignar juego · Pedir a Claude otra vez · Ignorar |
| Aprendizajes de Claude | Alias, regla, receta o ruta guardada, con fecha (§11.1) | Ver · **Deshacer** |
| Resultados por aprobar | Rutas, "icónico" y versiones que preparó Claude (§6.5, §6.7) | Aprobar · Editar · Rechazar |

---

## 3. Sagas (§12.4)

### 3.1 Lista de sagas
- **Cuadrícula** de tarjetas de saga (§0.4).
- **Filtros:** favoritas, que sigo, icónicas, por consola, por empresa, con o sin juegos que tengo.
- **Orden:** nombre, más icónicas, más jugadas (horas de Steam), última actividad.
- Entrada fija **"Emulación"** para los juegos sin saga (§6.2).

### 3.2 Detalle de saga
```
┌───────────────────────────────────────────────────────────┐
│ Arte de la saga · Nombre · ★ favorita · Seguir esta saga  │
│ Ruta: [Lanzamiento | Historia | Comunidad]  · 4/12 ▓▓░░░  │
├───────────────────────────────────────────────────────────┤
│  ◄ [portada]─[portada]─[portada]─[portada]─[portada] ►    │
│      ✔          ▶          ·          ·          ·        │
│   terminado  en curso   pendiente                         │
├───────────────────────────────────────────────────────────┤
│ Por qué este orden (Claude, con fuentes) · Notas del paso │
└───────────────────────────────────────────────────────────┘
```
- **Línea de tiempo horizontal** con las portadas de la ruta elegida. Cada paso muestra: estado de progreso, si lo tienes, **qué versión recomienda la comunidad** y por qué (§6.5).
- **Selector de ruta:** lanzamiento, historia o comunidad. Muestra la ruta que sigues y deja cambiarla.
- **Acciones:** marcar favorita, seguir la saga, cambiar el estado de un juego, abrir la ficha, editar la ruta, pedir a Claude que la recalcule.
- **Debajo:** la justificación de la ruta con sus fuentes, y una tabla de todos los juegos de la saga, incluidos los que no están en la ruta.

---

## 4. Colección (§12.4)
- **Agrupada por consola**: un selector de consola (las 23 más PC) con el número de juegos de cada una.
- **Filtros:** lo tengo · me falta · verificado · revisar · versión PC · no disponible · por biblioteca.
- **Orden:** nombre, saga o año.
- **Vista:** cuadrícula de tarjetas o lista compacta ❓D4.
- **Acciones:** **Escanear** (una biblioteca o todas, §11.2), **Verificar por hash** lo seleccionado (§7.2), marcar varias versiones para Steam, mover juegos a otra biblioteca (§5.3).

---

## 5. Ficha de juego (§12.4)

### 5.1 A la vista
| Bloque | Contenido | Acciones |
|---|---|---|
| Cabecera | Portada, nombre, saga (enlace), años y consolas, insignias | Jugar · Favorito · Puntuar ❓D5 |
| **Cómo jugarlo** | Opción elegida (versión de PC, port o emulador) y por qué la eligió la regla (§8.3) | **Jugar** · Cambiar opción · Configuración específica del juego |
| **Versiones** | Todas las versiones: consola o PC, original, remaster o remake. Comparativa con los **4 criterios por separado**: contenido, jugabilidad hoy, técnico y original (§6.4) | Marcar para Steam (varias) · Corregir la clasificación (§6.3) |
| **Archivos** | Dónde está cada archivo (biblioteca y ruta), BASE, UPD y DLC, verificado o no, formato y tamaño (§7.2, §7.3) | Abrir carpeta · Verificar · Mover · Instalar en el emulador (§8.6) |
| **Tu progreso** | Estado en la ruta, puntuación y notas | Cambiar estado · Escribir notas |

### 5.2 En secciones desplegables
| Sección | Contenido |
|---|---|
| Logros | RetroAchievements: lista y avance (§8.7) |
| Tiempo jugado | Horas de Steam (§7.4) |
| Tiendas | Dónde se vende, precio actual, mínimo histórico, gratis ahora o antes, Game Pass, y **seguir precio** ❓D6 (T2) |
| Conseguirlo | Pegar enlaces para JDownloader, elegir biblioteca destino con GB libres (§9.2) o buscarlo en un megapack |
| Datos y fuentes | Descripción traducida, duración (HowLongToBeat, opcional), notas (IGDB y las citadas por Claude) y de qué fuente sale cada dato. **Corregir a mano** cualquier campo (§6.8) |

---

## 6. Descubrir (§12.4, §6.7)
- Lista de **sagas recomendadas por afinidad**, cada una con su **porqué** escrito por Claude.
- **Acciones por recomendación:** seguir la saga, marcar favorita, "no me interesa" (Claude lo tiene en cuenta la próxima vez) ❓D7.
- **Pedir nuevas recomendaciones** (gasta tokens; muestra la fecha de la última).
- Bloque de **"Mis gustos"**: sagas favoritas, juegos puntuados, horas de Steam, para que veas en qué se basa.

---

## 7. Descargas (§9)

| Pestaña | Contenido | Acciones |
|---|---|---|
| **En curso** | Descargas de JDownloader y pasos de ingesta: extraer → identificar → convertir → ubicar → marcar (§9.1) | Pausar · Cancelar · Ver detalle |
| **Añadir enlaces** | Pegar una lista. El script asocia cada enlace a un juego y Claude resuelve lo que no (§9.2). Se elige biblioteca destino con **GB libres** | Revisar asociaciones · Enviar a JDownloader |
| **Megapack** | Elegir carpeta o archivo. **Vista previa** de lo identificado, todo marcado, con filtros por saga, consola y "está en el catálogo". Muestra qué se salta por 1G1R, duplicado o extra no admitido (§9.3) | Desmarcar · Importar |
| **Historial** | Lo importado, con fecha, origen y resultado | Ver juego. No hay deshacer: mover es definitivo una vez verificado (P7) |

- **Estado de JDownloader** arriba: conectado o no, y el dispositivo.

---

## 8. Herramientas (§8)

| Pestaña | Contenido | Acciones |
|---|---|---|
| **Emuladores** | Por consola: predeterminado y alternativos (#72), versiones instaladas lado a lado, cuál usa cada juego, compatibilidad | Instalar · **Actualizar** (con migración y copia previa, §8.4) · Volver a una versión · Hacer predeterminado · Abrir configuración · Detectar instalados a mano |
| **Ports** | Ports instalados y **ports nuevos** de la lista (Recompendium, §11.1), con su estado | Instalar · Actualizar · Ver qué juego cubre |
| **Complementos** | Texturas, shaders, ReShade y mods, agrupados por emulador o port | Instalar · Actualizar · Activar o desactivar |
| **Sistema** | BIOS, keys y firmware por consola, con versión y si están bien (§8.5) | **Subir archivo** · Ver qué emuladores lo usan · Avisos de lo que falta |
| **Recetas** | Lista de recetas, quién las escribió (Claude o el repositorio) y cuándo | Ver · Pedir a Claude la receta de algo nuevo |

- Cuando hay **versiones nuevas**, aparece un contador y el botón **Actualizar seleccionados**. Nunca se actualiza solo (§8.4).

---

## 9. Steam (§10)
- **Cuenta:** la detectada, con opción de cambiarla si hay varias (en este PC hay dos). Estado de la clave Web API y del préstamo familiar (T1).
- **Exportación:**
  - Lista de lo que se va a exportar: por saga, con su categoría y nombre "Juego (Consola)".
  - Diferencias con la última exportación: se agregan, se quitan, cambian.
  - Lo que ya está en Steam aparece aparte y **no se exporta** (§10).
  - Botón **Exportar a Steam** (avisa si Steam está abierto, porque hay que cerrarlo).
- **Arte:** portadas de SteamGridDB elegidas para cada juego, con opción de cambiarlas.
- **Estado de Steam ROM Manager** (instalado, versión) y enlace a su receta en Herramientas.

---

## 10. Ajustes

| Sección | Contenido |
|---|---|
| **Bibliotecas** ❓D8 | Lista con nombre, ruta, disco, espacio libre, estado (conectada o desconectada). Acciones: crear, adoptar, renombrar, quitar (sin borrar), **reconectar** y **mover o migrar** con progreso (§5.3) |
| **Carpeta de datos** | Ruta actual y espacio. Mover la carpeta de datos (S7) |
| **Cuentas y claves** | Cada servicio con estado (válida, vencida, falta), pegar o cambiar, enlace para conseguirla. Recordatorio de que el respaldo manual va en Bitwarden (§5.6) |
| **Respaldo** | Repositorio, último respaldo de cada tipo, uso de LFS sobre 10 GiB (#70), conflictos guardados aparte (#77). Acciones: respaldar ahora, restaurar |
| **Cómo jugar** | Orden de la regla automática y sus valores editables (§8.3) |
| **Ingesta** | Orden de regiones 1G1R (español primero, S2), formatos preferidos por consola (§9.5) |
| **Claude** | Tope de tareas por ejecución, estado de la cola, **registro de uso** (tarea, duración, resultado, §11.4) y sugerencias de automatizar con botón **Crear issue** (§11.5) |
| **Comprobaciones** | Intervalo de refresco de tiendas, precios y versiones con la app abierta (#75) |
| **Equipo** | Nombre de este PC y lo que se guarda por equipo (§7.6) |
| **Acerca de** | Versión, buscar actualización (Velopack) y notas de la versión |

---

## 11. Lanzar un juego (`otteretro jugar`) ❓D9
- Desde Steam o desde el botón Jugar (S6).
- Si antes de abrir hay que hacer algo (instalar una actualización en el emulador, regenerar un ISO, §8.6, §9.5), se muestra una **ventana pequeña** con el paso y su progreso. Si no hay nada que hacer, el juego se abre directo, sin ventana.
- Si falla (falta una BIOS, biblioteca desconectada), la ventana pequeña lo explica y ofrece abrir Otteretro.

---

## 12. Diálogos importantes
| Diálogo | Contenido |
|---|---|
| Mover o migrar | Origen, destino con espacio libre, tamaño, pasos (copiar → verificar → actualizar → borrar origen) y progreso (§5.3) |
| Actualizar emulador o port | Versión actual y nueva, novedades, qué se migra (partidas, configuración), copia previa. La versión vieja se conserva (§8.4) |
| Quitar biblioteca | Deja claro que **no borra archivos** (§5.3) |
| Confirmar importación de megapack | Resumen: cuántos se importan, saltan y reemplazan, y espacio necesario |
| Conflicto de respaldo | Qué versión ganó y dónde quedó la otra (#77) |

---

## Decisiones abiertas (encuesta 9)

| # | Pregunta | Recomendación |
|---|---|---|
| D1 | ¿Búsqueda global? | Sí, en la barra superior con `Ctrl+K` |
| D2 | ¿Orden de las secciones de Inicio, y se puede personalizar? | El orden de §2, fijo |
| D3 | ¿Dónde vive "Revisar"? | Pantalla propia a la que se llega desde Inicio y desde el contador de la barra lateral |
| D4 | Vista de Colección | Cuadrícula por defecto, con alternativa de lista |
| D5 | Escala de puntuación | 1 a 10 |
| D6 | ¿De qué juegos se avisa cuando bajan de precio? | Solo los que marcas **"seguir precio"** (una lista de deseos) |
| D7 | ¿"No me interesa" en Descubrir? | Sí, y Claude lo tiene en cuenta |
| D8 | ¿Dónde se gestionan las bibliotecas? | En Ajustes |
| D9 | ¿Ventana al lanzar un juego? | Solo si hay que preparar algo o si falla |
| D10 | Nombres de los juegos | Nombre oficial en español si existe; si no, el original |
| D11 | ¿Qué hace la nutria? | Icono, pantallas vacías y avisos (§12.2). ¿Algún sitio más? |

La encuesta añade tres preguntas de detalle: qué datos lleva la tarjeta de juego (D4b), cuándo cuenta como bajada de precio (D6b) y qué nombre se usa en Steam (D10b). También deja un espacio para comentarios libres (D12).
