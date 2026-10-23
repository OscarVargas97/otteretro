import { Proximamente } from "@/componentes/Marco";

// La ficha de un juego se abre como /juego/?id=…
export default function Juego() {
  return <Proximamente fase={1} titulo="Elige un juego" texto="Aquí verás cómo jugarlo, sus versiones, archivos y tu progreso." />;
}
