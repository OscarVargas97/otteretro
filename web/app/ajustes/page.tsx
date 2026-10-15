"use client";

import { useQuery } from "@tanstack/react-query";
import { VersionYActualizacion } from "@/componentes/Actualizacion";
import { pedir } from "@/lib/cliente";
import type { EstadoApp } from "@/lib/tipos";

export default function Ajustes() {
  const { data } = useQuery({ queryKey: ["estado"], queryFn: () => pedir<EstadoApp>("/api/estado") });
  return (
    <div className="content">
      <section className="sec">
        <div className="sec-h"><h2>Carpeta de datos</h2></div>
        <div className="panel">
          <span>{data?.carpetaDatos}</span>
          <span className="small muted">Base de datos, Herramientas, caché de imágenes y registro de errores.</span>
        </div>
      </section>
      <section className="sec">
        <div className="sec-h"><h2>Acerca de</h2></div>
        <VersionYActualizacion version={data?.version} />
        <p className="small muted" style={{ margin: 0 }}>Bibliotecas, cuentas y claves, respaldo y el resto de ajustes llegan con su fase.</p>
      </section>
    </div>
  );
}
