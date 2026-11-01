"use client";

import { Suspense } from "react";
import { ToolsPage } from "@/components/Tools";

/** Herramientas (§5.1, #193, #194): lo instalado por tipo, con lista y detalle. Instalar y actualizar llegan en la F5. */
export default function Tools() {
  return <div className="content"><Suspense><ToolsPage /></Suspense></div>;
}
