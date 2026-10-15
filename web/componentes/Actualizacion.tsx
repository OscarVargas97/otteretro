"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { pedir } from "@/lib/cliente";
import type { EstadoActualizacion } from "@/lib/tipos";

/** Estado de la actualización de la app; llega en vivo por el evento "actualizacion". */
export const useActualizacion = () =>
  useQuery({ queryKey: ["actualizacion"], queryFn: () => pedir<EstadoActualizacion>("/api/actualizacion") }).data;

/** Fila del panel de tareas (TaskRow del sistema de diseño). No muestra nada si no hay actualización en marcha. */
export function TareaActualizacion() {
  const a = useActualizacion();
  if (!a || !["Buscando", "Descargando", "Lista", "Error"].includes(a.fase)) return null;
  const progreso = Number(a.progreso);
  const estado: Record<string, string> = {
    Buscando: "Buscando…",
    Descargando: `${progreso} %`,
    Lista: "Lista",
    Error: "Error",
  };
  const detalle: Record<string, string> = {
    Buscando: "Comprobando si hay una versión nueva",
    Descargando: `Descargando la versión ${a.version}`,
    Lista: `La versión ${a.version} se instalará al cerrar la app`,
    Error: a.mensaje ?? "No se pudo actualizar",
  };
  return (
    <div className={`ot-task${a.fase === "Error" ? " is-error" : ""}`}>
      <span className="ot-task-title">Actualización de Otteretro</span>
      <span className="ot-task-state">{estado[a.fase]}</span>
      {a.fase === "Descargando" && (
        <div className="ot-progress"><span style={{ width: `${progreso}%` }} /></div>
      )}
      <span className="ot-task-detail">{detalle[a.fase]}</span>
    </div>
  );
}

/** Ajustes › Acerca de: versión actual, botón para buscar y el estado de la actualización en el mismo sitio. */
export function VersionYActualizacion({ version }: { version?: string }) {
  const cache = useQueryClient();
  const a = useActualizacion();
  const buscar = useMutation({
    mutationFn: () => pedir<EstadoActualizacion>("/api/actualizacion/buscar", { method: "POST" }),
    onSuccess: (estado) => cache.setQueryData(["actualizacion"], estado),
  });
  const reiniciar = useMutation({ mutationFn: () => pedir("/api/actualizacion/reiniciar", { method: "POST" }) });
  const ocupado = a?.fase === "Buscando" || a?.fase === "Descargando";
  const texto: Record<string, string> = {
    NoDisponible: a?.mensaje ?? "",
    Buscando: "Buscando una versión nueva…",
    AlDia: "Estás al día: no hay versiones nuevas.",
    Descargando: `Descargando la versión ${a?.version} · ${Number(a?.progreso)} %`,
    Lista: `La versión ${a?.version} está lista. Se instalará al cerrar la app, o ahora si reinicias.`,
    Error: a?.mensaje ?? "No se pudo actualizar.",
  };
  return (
    <div className="panel">
      <div className="fila-version">
        <span>Otteretro {version}</span>
        {a?.fase === "Lista" ? (
          <button className="ot-btn ot-btn-primary" type="button" disabled={reiniciar.isPending} onClick={() => reiniciar.mutate()}>
            {reiniciar.isPending ? "Reiniciando…" : "Reiniciar ahora"}
          </button>
        ) : (
          <button className="ot-btn" type="button" disabled={ocupado || buscar.isPending} onClick={() => buscar.mutate()}>
            {ocupado ? "Buscando…" : "Buscar actualización"}
          </button>
        )}
      </div>
      {a && texto[a.fase] && <span className={`small ${a.fase === "Error" ? "error" : "muted"}`}>{texto[a.fase]}</span>}
      {a?.fase === "Descargando" && (
        <div className="ot-progress"><span style={{ width: `${Number(a.progreso)}%` }} /></div>
      )}
    </div>
  );
}

/** Aviso visible cuando hay una versión descargada lista para instalar. */
export function AvisoActualizacion() {
  const a = useActualizacion();
  const reiniciar = useMutation({ mutationFn: () => pedir("/api/actualizacion/reiniciar", { method: "POST" }) });
  if (a?.fase !== "Lista") return null;
  return (
    <div className="aviso" role="status">
      <img src="/nutria.png" alt="" width={32} height={32} />
      <span>
        <b>Otteretro {a.version} está lista.</b> Se instalará cuando cierres la app, o ahora mismo si reinicias.
      </span>
      <button className="ot-btn ot-btn-primary" type="button" disabled={reiniciar.isPending} onClick={() => reiniciar.mutate()}>
        {reiniciar.isPending ? "Reiniciando…" : "Reiniciar ahora"}
      </button>
    </div>
  );
}
