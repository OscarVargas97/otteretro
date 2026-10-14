# Requisito: emuladores y ports

- **Estado:** decidido. Hay detalles pendientes en [temas-abiertos.md](temas-abiertos.md).
- **Fecha:** 2026-10-14

## Instalación

- La app **descarga** emuladores y ports desde su fuente oficial (GitHub Releases o la web del proyecto) y también **detecta** los que instales tú, como Ryujinx SS4K.
- Cada emulador y cada port tiene una **receta** en el repositorio (JSON) que indica:
  - de dónde se descarga y qué archivo bajar,
  - cómo se deja en modo portable,
  - dónde guarda partidas y configuración,
  - qué rutas internas hay que reescribir,
  - qué pasos extra necesita (por ejemplo, Ship of Harkinian genera sus datos desde la ROM y Dusklight necesita la ruta del ISO). La app **los ejecuta sola**,
  - qué **formato de juego** acepta o exige (ver [ingesta.md](ingesta.md#conversión-de-formatos)).
- Claude puede proponer recetas nuevas.

## Ubicación

- Los emuladores y ports viven en una **biblioteca de herramientas** separada de las bibliotecas de juegos y compartida por todas. Ver [bibliotecas.md](bibliotecas.md#tipos-de-biblioteca).

## Asignación

- Cada consola tiene un **emulador predeterminado**, que se puede configurar.
- Cualquier juego puede usar un **emulador o port específico** y una **configuración propia**. Por ejemplo, Skyward Sword HD usa Ryujinx SS4K con su pack 4K.
- Los ports se asignan **automáticamente** según la regla de [catalogo.md](catalogo.md#emulador-port-o-pc-por-juego).
- Puede haber **varias instalaciones** del mismo emulador, cada una con su nombre.

## Actualización

- La actualización se lanza a mano. Al lanzarla, la app revisa las versiones y **avisa** qué emuladores y ports tienen una versión nueva. Tú eliges cuáles instalar.
- **La versión vieja nunca se reemplaza.** La nueva se instala al lado y la anterior se conserva. Puedes volver a la anterior con un clic.
- **Migración:** antes de actualizar, la app hace una copia de seguridad puntual y después copia las partidas y la configuración a la versión nueva, siguiendo la receta.
- Cada port guarda sus partidas a su manera, y aún no sabemos cómo lo hace cada uno. **Esto se investiga al escribir su receta.**

## Archivos de sistema (BIOS, keys, firmware)

- Salen de tu consola. La app **no los descarga**.
- Se **suben desde la interfaz** y quedan en un almacén común, disponibles para **todos los emuladores** que los usen. Por ejemplo, las `prod.keys` de Switch sirven para todas las instalaciones de Ryujinx y Ryubing.
- Tienen **control de versiones**, por ejemplo varias versiones del firmware de Switch.
- La app indica dónde va cada archivo, comprueba que esté y avisa si falta.
