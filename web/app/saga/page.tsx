"use client";

import { ComingSoon } from "@/components/Shell";

// El detalle de una saga se abre como /saga/?id=… (la exportación estática no admite rutas dinámicas sin conocerlas al compilar).
export default function Saga() {
  return <ComingSoon screen="saga" phase={1} />;
}
