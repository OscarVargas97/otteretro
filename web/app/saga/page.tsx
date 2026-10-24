import { Proximamente } from "@/componentes/Marco";

// El detalle de una saga se abre como /saga/?id=… (la exportación estática no admite rutas dinámicas sin conocerlas al compilar).
export default function Saga() {
  return <Proximamente fase={1} titulo="Elige una saga" texto="Aquí verás la línea de tiempo de la saga con la ruta que sigas." />;
}
