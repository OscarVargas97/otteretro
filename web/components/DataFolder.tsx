"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { pedir } from "@/lib/cliente";
import { gb } from "@/lib/formato";
import type { Disco, EstadoApp, EstadoMudanza, InfoCarpeta, PedidoCarpeta } from "@/lib/tipos";

/** Estado de la mudanza de la carpeta de datos; llega en vivo por el evento "mudanza". */
export const useMudanza = () =>
  useQuery({ queryKey: ["mudanza"], queryFn: () => pedir<EstadoMudanza>("/api/carpeta-datos/mudanza") }).data;

const porcentaje = (m: EstadoMudanza) => (Number(m.total) > 0 ? Math.round((Number(m.hecho) / Number(m.total)) * 100) : 0);

/** Fila del panel de tareas mientras se mueve la carpeta de datos (o con su resultado). */
export function TareaMudanza() {
  const m = useMudanza();
  if (!m || !(m.enCurso || m.paso === "Listo" || m.paso === "Error")) return null;
  return (
    <div className={`ot-task${m.error ? " is-error" : ""}`}>
      <span className="ot-task-title">Mover la carpeta de datos</span>
      <span className="ot-task-state">{m.enCurso ? `${porcentaje(m)} %` : m.error ? "Error" : "Listo"}</span>
      {m.enCurso && <div className="ot-progress"><span style={{ width: `${porcentaje(m)}%` }} /></div>}
      <span className="ot-task-detail">{m.error ?? (m.enCurso ? `${m.paso} · hacia ${m.destino}` : `Ahora está en ${m.destino}`)}</span>
    </div>
  );
}

/** Ajustes › Carpeta de datos: dónde está, cuánto ocupa, y moverla o usar otra (S7). */
export function AjustesCarpetaDatos() {
  const cache = useQueryClient();
  const info = useQuery({ queryKey: ["carpeta"], queryFn: () => pedir<InfoCarpeta>("/api/carpeta-datos") }).data;
  const discos = useQuery({ queryKey: ["discos"], queryFn: () => pedir<Disco[]>("/api/discos") }).data;
  const m = useMudanza();
  const [modo, setModo] = useState<"mover" | "usar" | null>(null);
  const [ruta, setRuta] = useState("");

  const refrescar = () => {
    cache.invalidateQueries({ queryKey: ["estado"] });
    cache.invalidateQueries({ queryKey: ["carpeta"] });
  };
  const mover = useMutation({
    mutationFn: (p: PedidoCarpeta) => pedir<EstadoMudanza>("/api/carpeta-datos/mover", { method: "POST", body: JSON.stringify(p) }),
    onSuccess: (estado) => {
      cache.setQueryData(["mudanza"], estado);
      setModo(null);
    },
  });
  const usar = useMutation({
    mutationFn: (p: PedidoCarpeta) => pedir<EstadoApp>("/api/carpeta-datos", { method: "POST", body: JSON.stringify(p) }),
    onSuccess: () => {
      refrescar();
      setModo(null);
    },
  });
  const accion = modo === "mover" ? mover : usar;

  // Al terminar la mudanza, la ruta mostrada pasa a ser la nueva.
  const terminada = m?.paso === "Listo" ? m.destino : null;
  useEffect(() => {
    if (terminada) {
      cache.invalidateQueries({ queryKey: ["estado"] });
      cache.invalidateQueries({ queryKey: ["carpeta"] });
    }
  }, [terminada, cache]);

  return (
    <div className="panel">
      <div className="fila-version">
        <div style={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
          <span>{info?.ruta}</span>
          {info && <span className="small muted">Ocupa {gb(info.ocupado)} · {gb(info.libreEnDisco)} libres en ese disco</span>}
        </div>
        {!m?.enCurso && !modo && (
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <button className="ot-btn" type="button" onClick={() => { setModo("mover"); setRuta(""); accion.reset(); }}>Mover a otra carpeta…</button>
            <button className="ot-btn ot-btn-ghost" type="button" onClick={() => { setModo("usar"); setRuta(""); accion.reset(); }}>Usar otra carpeta…</button>
          </div>
        )}
      </div>
      <span className="small muted">Base de datos, Herramientas, caché de imágenes y registro de errores.</span>

      {modo && (
        <div className="formulario">
          <p className="small" style={{ margin: 0 }}>
            {modo === "mover"
              ? "Se copiará todo a la carpeta nueva, se comprobará cada archivo y solo entonces se borrará la actual. En el mismo disco se mueve al instante. La carpeta nueva tiene que estar vacía o no existir."
              : "La app empezará a usar esa carpeta (por ejemplo, una que traes de otro PC). La actual no se mueve ni se borra. Si la carpeta elegida está vacía, empezarás de cero."}
          </p>
          <div className="chips">
            {discos?.map((d) => (
              <button key={d.nombre} className="chip" type="button" onClick={() => setRuta(`${d.nombre}Otteretro`)}>
                {d.nombre}Otteretro · {gb(d.libre)} libres
              </button>
            ))}
          </div>
          <label className="small muted" htmlFor="nueva-ruta">Carpeta</label>
          <input id="nueva-ruta" value={ruta} onChange={(e) => setRuta(e.target.value)} placeholder="Por ejemplo E:\Otteretro" />
          {accion.isError && <p className="error small" style={{ margin: 0 }}>{accion.error.message}</p>}
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => setModo(null)}>Cancelar</button>
            <button className="ot-btn ot-btn-primary" type="button" disabled={!ruta || accion.isPending} onClick={() => accion.mutate({ ruta })}>
              {modo === "mover" ? "Mover" : "Usar esta carpeta"}
            </button>
          </div>
        </div>
      )}

      {m?.enCurso && (
        <div className="formulario" role="status">
          <div className="fila-version">
            <span><span className="girando" aria-hidden="true" style={{ display: "inline-block", marginRight: 8 }} />{m.paso} hacia {m.destino}</span>
            <span className="small muted">{porcentaje(m)} %</span>
          </div>
          <div className="ot-progress"><span style={{ width: `${porcentaje(m)}%` }} /></div>
          <span className="small muted">No cierres la app hasta que termine.</span>
        </div>
      )}
      {!m?.enCurso && m?.paso === "Listo" && (
        <div className="resultado-actualizacion" role="status">
          <span className="ot-badge ot-badge-have">Movida</span>
          <span className="small muted">{m.aviso ?? `Tus datos ahora están en ${m.destino}.`}</span>
        </div>
      )}
      {!m?.enCurso && m?.error && (
        <div className="resultado-actualizacion" role="status">
          <span className="ot-badge ot-badge-danger">Error</span>
          <span className="small muted">{m.error}</span>
        </div>
      )}
    </div>
  );
}
