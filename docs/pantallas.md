# Especificación de pantallas

- **Fecha:** 2026-10-14 · actualizada el 2026-10-25 con la fase D1 (#32): mando, Inicio con consola y PC, juegos de PC y seguimiento
- **Para qué sirve:** es la base para diseñar en **Claude Design**. Cada pantalla indica qué muestra, qué acciones tiene y qué estados contempla. No define colores ni disposición exacta: eso sale del sistema de diseño.
- **Diseño:** sistema de diseño https://claude.ai/artifact/DMH4KuU2kkAnb3ApFTCiuu · prototipo navegable de todas las pantallas https://claude.ai/artifact/Bo8gtfiPdRAumFhkFikHPV
- **De dónde sale:** de [REQUISITOS.md](REQUISITOS.md), sobre todo §12. Cada punto lleva entre paréntesis la sección o decisión que lo respalda.
- **Decisiones de diseño:** cerradas en la [encuesta 9](https://claude.ai/artifact/XsFAKMCB683zcsdRzmZWLR) (decisiones #79 a #90). Las marcas **(Dn)** indican de qué pregunta sale cada punto.

---

## 0. Elementos comunes

### 0.1 Estructura de la ventana (§12.3)
```
┌──────────┬───────────────────────────────────────────────┐
│ [nutria] │  Barra superior: título · búsqueda (D1) · tareas │
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
- **Mando:** la misma ventana se usa con mando (§0.7).

### 0.2 Panel de tareas (§12.3)
- Un botón en la barra superior con un **indicador de actividad**. Al pulsarlo se abre un panel lateral.
- Lista de tareas: **en curso** (con progreso y botón cancelar si se puede), **en cola**, **pausadas** (por ejemplo, Claude al llegar al límite de la suscripción, con la hora de reanudación) y **terminadas o con error** (con reintentar y ver detalle).
- Cuando **Claude está trabajando**, el panel muestra a la nutria (D11).
- Tipos de tarea: escaneo, verificación por hash, descarga, ingesta, conversión, mover biblioteca, instalar o actualizar emulador, Claude, respaldo, exportar a Steam y refrescar tiendas.
- Al terminar una tarea larga llega una **notificación del sistema** (§12.3). El respaldo de partidas tras jugar va **en silencio** y solo se ve si falla (T7).

### 0.3 Tarjeta de juego (componente)
- Portada (SteamGridDB), **nombre oficial en español si existe; si no, el original** (D10), consola o consolas, año.
- **Datos a la vista** (D4b): lo tengo o me falta · consola · año · progreso en la ruta · tiene versión de PC · en Game Pass · **cómo se juega** (PC, port o emulador) · **tamaño en disco**.
- **Insignias de aviso:** revisar · no disponible (biblioteca desconectada).
- Progreso en la ruta: pendiente, en curso, terminado o abandonado.
- **Acciones rápidas** al pasar el ratón: Jugar (si lo tienes) y abrir la ficha.

### 0.4 Tarjeta de saga (componente)
- Arte de la saga, nombre, cantidad de juegos, cuántos tienes y tu avance en la ruta elegida.
- Marca de **favorita** (§6.7).

### 0.5 Estados que toda pantalla contempla
| Estado | Qué se muestra |
|---|---|
| Vacío | La **nutria** con un mensaje y la acción que lo resuelve (§12.2). Ejemplo: "Aún no hay bibliotecas. Crear o adoptar una". |
| Cargando | Esqueletos de las tarjetas, sin spinners de página completa. En cargas largas, **animación de la nutria** (D11) |
| Error | Qué falló, en español, con reintentar. Las fuentes opcionales que fallan (HowLongToBeat, Game Pass) muestran "sin datos" sin bloquear nada (#66) |
| Sin conexión | Lo local funciona; lo que necesita internet se marca como no disponible |

### 0.6 Búsqueda (D1)
- **Búsqueda global** en la barra superior (atajo `Ctrl+K`) que encuentra sagas, juegos, emuladores y ajustes. Con mando se abre con X y se escribe con el teclado en pantalla (§0.7).

### 0.7 Mando (§12.5, #134)
- **La misma interfaz**, con navegación espacial: la cruceta salta entre elementos. Con mando el foco crece (anillo de 3px, la tarjeta sube) y abajo aparece la **barra de botones** con lo que hace cada uno en esa pantalla; con ratón, ninguno de los dos.
- Botones por función: **A elegir, B volver, X buscar, Menú**, con la forma del mando conectado (Xbox, PlayStation o Switch).
- **Botón Guía:** menú rápido encima de todo, también de un juego: volver a Otteretro, ir a Steam, capturar pantalla y cerrar el juego (pide A otra vez). Sin juego abierto, solo ir a Steam. En la sesión de Steam se abre con una combinación (playos#6).
- **Escribir:** el teclado de Steam dentro de su sesión; fuera, el propio de Otteretro (B cerrar, X borrar, Y espacio, Menú listo).
- Todo lo que se elige mide al menos 44px. Recorrido de ejemplo en el prototipo: «Recorrido con mando».

---

## 1. Primer arranque (asistente)

Aparece solo la primera vez, o si la carpeta de datos no está donde se esperaba (P1, §5).

Cuatro pasos (#418, decisión 394):

| Paso | Contenido | Acciones |
|---|---|---|
| 1. Bienvenida | Logo. Qué hace la app y que son cuatro pasos. Idioma | Empezar |
| 2. Dónde guardar | Los discos con su espacio libre, para marcar dónde van juegos, emuladores y ports. Plegado en «Más opciones»: la carpeta de datos (S7; la sugerida, cambiable) y adoptar una carpeta con la estructura antigua escrita a mano (§5.5). El respaldo (#68) llega en la F7 | Seguir (guarda la carpeta de datos, agrega los discos y, si detecta la estructura antigua, ofrece **Adoptar** (§5.5) antes de seguir) · Atrás |
| 3. Steam y cuentas (opcional) | Cuenta detectada en `loginusers.vdf` (§10) y lo que se puede marcar para Steam. Plegado: claves y cuentas de descarga (§5.6, A3) | Exportar · **Después** · Atrás |
| 4. Carga inicial | El escaneo de los discos marcados, que sigue solo en segundo plano | Entrar |

- Lo que se salta queda como **aviso en Inicio** hasta que se completa.

---

## 2. Inicio (§12.4)

Secciones, en este **orden fijo** (D2):

1. **Continuar jugando** (#138): lo último que jugaste, de consola o de PC, con cómo se juega, cuándo y las horas. Es lo primero que recibe el foco con mando.
2. **Siguiente en tus rutas:** el **siguiente juego** de cada ruta que sigues (§6.5), en tarjetas grandes con botón Jugar, o "Conseguirlo" si no lo tienes.
3. **Revisar:** cuántos archivos esperan revisión; lleva a Colección con la lista abierta (§2.1).
4. **Novedades:** ports nuevos, cambios de compatibilidad, versiones nuevas de emuladores, ports y complementos, y **bajadas de precio** (#57, #75, D6).
   - **De qué juegos:** los de las **sagas que sigues que no tienes**, más los que marcas **"seguir precio"**.
   - **Cuándo cuenta como bajada:** llega a su **mínimo histórico**, queda **gratis** o tiene un **descuento del 40 % o más** (D6b).
5. **Tareas en curso:** descargas y tareas activas (resumen del panel de tareas).
6. **Avisos de configuración:** pasos del asistente que se saltaron, credenciales vencidas, biblioteca desconectada, BIOS o keys que faltan (§8.5), LFS bloqueado (#70).

Estados (§0.5): **vacío** (la nutria, «Escanear una carpeta» y «Explorar sagas»), **cargando** (esqueletos de las tarjetas), **error** (qué carpeta no responde, que tus datos no se tocaron, Reintentar y Abrir Ajustes) y **con datos**.

### 2.1 Revisar (D3)
**Desde la decisión 390 (#415) ya no es una pantalla:** Colección muestra el aviso «N archivos por revisar» solo si hay archivos dudosos o sin identificar, y «Revisar» despliega ahí mismo la lista (archivo, carpeta, propuesta, Confirmar / Es otro juego); el contador va en Colección de la barra lateral. La tabla de abajo es el diseño original. Es la cola de lo que la app decidió con dudas (P6, §9.4):

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
- **Filtros:** favoritas, que sigo, icónicas, por consola, por empresa, con o sin juegos que tengo, **con juegos de PC** (§6.6).
- Cada tarjeta muestra sus **plataformas**, con PC como una más.
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
- Al **terminar un juego** o **completar una ruta**, aparece la nutria celebrándolo (D11).
- **Acciones:** marcar favorita, seguir la saga, cambiar el estado de un juego, abrir la ficha, editar la ruta, pedir a Claude que la recalcule.
- **Debajo:** la justificación de la ruta con sus fuentes, y una tabla de todos los juegos de la saga, incluidos los que no están en la ruta.

---

## 4. Colección (§12.4)
- **Agrupada por consola**: un selector de consola (las 23 más PC) con el número de juegos de cada una.
- **PC** (§6.6): cada juego dice **quién lo lanza** (Steam, Heroic · Epic o GOG, Lutris, Battle.net, Xbox, itch.io o carpeta suelta) y las horas; **«En Game Pass»** va aparte de «Lo tengo»; los que no corren en este equipo dicen **«Jugar en el PC»** (#112). El resumen cuenta los juegos por tienda.
- **Filtros:** lo tengo · me falta · verificado · revisar · versión PC · no disponible · por biblioteca.
- **Orden:** nombre, saga o año.
- **Vista** (D4): **cuadrícula** de portadas por defecto, con alternativa de **lista compacta**.
- **Acciones:** **Escanear** (una biblioteca o todas, §11.2), **Verificar por hash** lo seleccionado (§7.2), marcar varias versiones para Steam, mover juegos a otra biblioteca (§5.3).

---

## 5. Ficha de juego (§12.4)

### 5.1 A la vista
| Bloque | Contenido | Acciones |
|---|---|---|
| Cabecera | Portada, nombre, saga (enlace), años y consolas, insignias | Jugar (dice con qué lanza si es de PC) · Favorito |
| **Cómo jugarlo** | Opción elegida (versión de PC, port o emulador) y por qué la eligió la regla (§8.3) | **Jugar** · Cambiar opción · **Quién lo lanza** (acceso de Steam u Otteretro directo, #130) · Configuración específica del juego |
| **Quién lo lanza** (juegos de PC) | Las tiendas donde lo tienes, instalado o no, con las horas de cada una; y **jugar en el PC** por streaming (#112). Por defecto, la tienda donde más jugaste | Elegir para este juego |
| **Dónde lo tienes** (juegos de PC) | Tienda, cuenta, instalado, tamaño y logros | — |
| **Versiones** | Todas las versiones: consola o PC, original, remaster o remake. Comparativa con los **4 criterios por separado**: contenido, jugabilidad hoy, técnico y original (§6.4) | Marcar para Steam (varias) · Corregir la clasificación (§6.3) |
| **Archivos** | Dónde está cada archivo (biblioteca y ruta), BASE, UPD y DLC, verificado o no, formato y tamaño (§7.2, §7.3) | Abrir carpeta · Verificar · Mover · Instalar en el emulador (§8.6) · **Traer a este equipo** si está en una biblioteca en red (#136) |
| **Seguimiento** (§7.4, #133) | Estado (pendiente, jugando, terminado, 100 %, abandonado) con fecha de inicio y de fin (solo cuando aplica), nota **de 1 a 10** (D5), notas, tiempo jugado, sesiones, logros y última vez | Cambiar estado y fechas · Puntuar · Escribir notas. Jugar lo pasa solo a «jugando» |

### 5.2 En secciones desplegables
| Sección | Contenido |
|---|---|
| Sesiones | Historial: cuándo, cuánto, con qué y quién lo midió (Otteretro, o Steam importado) (§7.4) |
| Logros | De Steam y de RetroAchievements: lista y avance (§8.7) |
| Tiendas | Dónde se vende, precio actual, mínimo histórico, gratis ahora o antes, Game Pass, y el interruptor **seguir precio** (D6, T2) |
| Conseguirlo | Pegar enlaces para JDownloader, elegir biblioteca destino con GB libres (§9.2) o buscarlo en un megapack |
| Datos y fuentes | Descripción traducida, duración (HowLongToBeat, opcional), notas (IGDB y las citadas por Claude) y de qué fuente sale cada dato. **Corregir a mano** cualquier campo (§6.8) |

---

## 6. Descubrir (§12.4, §6.7)
- Lista de **sagas recomendadas por afinidad**, cada una con su **porqué** escrito por Claude.
- **Acciones por recomendación:** seguir la saga, marcar favorita y **"no me interesa"** (D7), que **oculta** la recomendación. **Pasado un tiempo vuelve a poder recomendarse**: por defecto 6 meses, configurable (supuesto S8).
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
- **Cómo se ve (#417, decisión 392):** arriba la caja **Añadir** (pegar o arrastrar enlaces, los paquetes capturados con Comprobar y Quitar, y en una línea el disco de destino y «Descargar»). Debajo, dos pestañas: **En curso** (con su contador) y **Terminadas**; cada fila con barra, sus enlaces plegados, Pausar/Reanudar/Cancelar y «Volver a bajar» si falló. Conexiones, descargas a la vez por sitio, capturar lo copiado y las cuentas van en un `details` **Avanzado** plegado. Megapack sigue en Colección (importar carpeta); «Ver juego» y «Comprobar al terminar» esperan al backend.

---

## 8. Herramientas (§8)

| Pestaña | Contenido | Acciones |
|---|---|---|
| **Emuladores** | Por consola: predeterminado y alternativos (#72), versiones instaladas lado a lado, cuál usa cada juego, compatibilidad | Instalar · **Actualizar** (con migración y copia previa, §8.4) · Volver a una versión · Hacer predeterminado · Abrir configuración · Detectar instalados a mano |
| **Ports** | Ports instalados y **ports nuevos** de la lista (Recompendium, §11.1), con su estado; cada port aparece también en su consola y en la ficha del juego (#193) | Instalar · Actualizar · Ver qué juego cubre |
| **Complementos** | Texturas, shaders, ReShade y mods, agrupados por emulador o port | Instalar · Actualizar · Activar o desactivar |
| **Sistema** | BIOS, keys y firmware por consola, con versión y si están bien (§8.5) | **Subir archivo** · Ver qué emuladores lo usan · Avisos de lo que falta |
| **Recetas** | Lista de recetas, quién las escribió (Claude o el repositorio) y cuándo | Ver · Pedir a Claude la receta de algo nuevo |

- Cuando hay **versiones nuevas**, aparece un contador y el botón **Actualizar seleccionados**. Nunca se actualiza solo (§8.4).
- **Cómo se ve (#416, decisión 391):** cada pestaña es una lista (nombre, para qué es, insignia de estado) y la fila elegida abre su detalle a la derecha (una columna en pantallas angostas): acción principal, «Abrir su carpeta», versiones plegadas y «Más» (mover a otro disco, quitar). Las versiones nuevas salen en un aviso arriba y en el contador de cada pestaña; «Actualizar todo» espera a que el backend lo tenga. **Instalar** es un diálogo de 2 pasos: qué y desde dónde, y luego versión y archivo.

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

**Diseño (#200, reemplaza la #196 y la #197):** pestañas arriba y una sola columna de contenido centrada con ancho máximo; nunca dos columnas de bloques. Herramientas, igual.

| Sección | Contenido |
|---|---|
| **Almacenamiento** (D8, #201 a #205, #208 a #210, con la distribución de Steam) | Una **tabla de discos** (#419): nombre (★ el de la carpeta de datos), ruta, espacio libre, estado (Conectado o Desconectado) y la acción principal, «Abrir carpeta» o «Reconectar» si está desconectado, con «Más» al lado; arriba, «+ Agregar disco» (ventana con las unidades; crea `Otteretro` con sus cinco carpetas) y, debajo, «Quitar un disco lo saca de Otteretro; nunca borra archivos». «Más» despliega bajo la fila lo del disco: una **barra de uso gruesa** por tipo (juegos, emuladores, ports, complementos, sistema, datos de la app, otros archivos y libre) y su leyenda. Abajo, una **tabla** con casilla, nombre y tamaño; en el ★, la primera fila es «Datos de Otteretro» (se mueve sola, con su progreso, y el ★ la sigue). Lo demás va en **grupos desplegables** (#211), cerrados al abrir: tipo › consola o emulador › cada cosa, con cuántas tiene y su tamaño; la casilla de un grupo marca todo lo de adentro o queda a medias. Al pie, «Mover N cosas (tamaño) a [disco] · Mover», y «Usar otra carpeta de datos…» en el ★ o «Quitar disco…» (sin borrar) en los demás. Si quedan carpetas de antes o Herramientas en la carpeta de datos, un plegable «Pasar al estándar» (§5.3). Al final, el bloque **Reconocer juegos** (#251): interruptor «Usar Claude cuando ningún script lo reconoce» y «Este mes: N llamadas · X US$» (o hasta cuándo espera al límite) |
| **Consolas** (#192, #195) | Lista de consolas por empresa con interruptor; al abrir una: sus juegos (en qué ubicación, destino de lo nuevo, mover), sus emuladores (predeterminado y alternativos), sus ports (con el juego que cubren) y su sistema (BIOS, keys, firmware) |
| **Cuentas y claves** | Una tarjeta por servicio de la fase en curso, con para qué sirve y su estado («Lista», «Falta 1 de 2»; válida y vencida llegan con su integración, #148); «Configurar» abre un diálogo para pegar, cambiar o borrar cada campo y el enlace para conseguirla. Los servicios de fases posteriores van plegados en «Más adelante», con su fase (#57). Recordatorio de que el respaldo manual va en Bitwarden (§5.6) |
| **Respaldo** | Repositorio, último respaldo de cada tipo, uso de LFS sobre 10 GiB (#70), conflictos guardados aparte (#77). Acciones: respaldar ahora, restaurar |
| **Cómo jugar** | Orden de la regla automática y sus valores editables (§8.3) |
| **Ingesta** | Orden de regiones 1G1R (español primero, S2), formatos preferidos por consola (§9.5) |
| **Comprobaciones** | Intervalo de refresco de tiendas, precios y versiones con la app abierta (#75) |
| **Equipo** | Nombre de este PC y lo que se guarda por equipo (§7.6) |
| **Acerca de** | Versión, buscar actualización (Velopack) y notas de la versión |

---

## 11. Lanzar un juego (`otteretro jugar`) (D9)
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

## Decisiones de la encuesta 9

| # | Decisión |
|---|---|
| D1 | Búsqueda global en la barra superior, `Ctrl+K` |
| D2 | Inicio con el orden de §2, fijo |
| D3 | ~~Revisar es una pantalla propia, con contador en la barra lateral~~ Reemplazada por la decisión 390: un aviso desplegable en Colección, con el contador en Colección |
| D4 | Colección en cuadrícula, con alternativa de lista compacta |
| D4b | La tarjeta muestra: lo tengo, consola, año, progreso, versión PC, Game Pass, cómo se juega y tamaño |
| D5 | Puntuación de 1 a 10 |
| D6 | Avisos de precio: juegos de las sagas que sigues que no tienes, más los marcados "seguir precio" |
| D6b | Cuenta como bajada: mínimo histórico, gratis o descuento del 40 % o más |
| D7 | "No me interesa" oculta la recomendación; pasado un tiempo puede volver (6 meses por defecto, S8) |
| D8 | Bibliotecas en Ajustes |
| D9 | Ventana al lanzar solo si hay que preparar algo o si falla |
| D10 | Nombre oficial en español si existe; si no, el original |
| D10b | En Steam, el mismo nombre que en la app, con "(Consola)" |
| D11 | Nutria: icono, pantallas vacías, avisos, animación de carga, cuando Claude trabaja y al terminar un juego o completar una ruta. No en el asistente |
