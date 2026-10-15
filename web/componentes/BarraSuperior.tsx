"use client";

import { useEffect, useRef, useState } from "react";
import { urlEventos } from "@/lib/cliente";
import { Vacio } from "./Marco";

/** Título de la pantalla, búsqueda global (Ctrl+K) y panel de tareas en segundo plano. */
export function BarraSuperior({ titulo }: { titulo: string }) {
  const [buscando, setBuscando] = useState(false);
  const [tareas, setTareas] = useState(false);
  const conectado = useEventos();

  useEffect(() => {
    const alPulsar = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setBuscando(true);
      }
      if (e.key === "Escape") {
        setBuscando(false);
        setTareas(false);
      }
    };
    window.addEventListener("keydown", alPulsar);
    return () => window.removeEventListener("keydown", alPulsar);
  }, []);

  return (
    <header className="top">
      <h1>{titulo}</h1>
      <button className="search" type="button" onClick={() => setBuscando(true)}>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
          <circle cx="11" cy="11" r="7" />
          <path d="M20 20l-4-4" />
        </svg>
        Buscar sagas, juegos, emuladores…
        <span className="kbd">CTRL K</span>
      </button>
      <button className="ot-btn" type="button" aria-expanded={tareas} onClick={() => setTareas(!tareas)}>
        <span className="dot" data-conectado={conectado} />
        Tareas
      </button>

      {tareas && (
        <div className="ot-tasks panel-tareas" role="dialog" aria-label="Tareas en segundo plano">
          <Vacio titulo="No hay tareas en curso" texto="Aquí verás escaneos, descargas, respaldos y el trabajo de Claude." />
          <p className="small muted panel-tareas-pie">{conectado ? "Conectado a la app" : "Reconectando…"}</p>
        </div>
      )}

      {buscando && <Busqueda alCerrar={() => setBuscando(false)} />}
    </header>
  );
}

function Busqueda({ alCerrar }: { alCerrar: () => void }) {
  const campo = useRef<HTMLInputElement>(null);
  useEffect(() => campo.current?.focus(), []);
  return (
    <div className="velo" onClick={alCerrar}>
      <div className="busqueda" role="dialog" aria-label="Buscar" onClick={(e) => e.stopPropagation()}>
        <label htmlFor="buscar" className="small muted">Buscar</label>
        <input id="buscar" ref={campo} placeholder="Sagas, juegos, emuladores, ajustes…" />
        <p className="small muted">La búsqueda funciona desde la fase 1, cuando haya catálogo.</p>
      </div>
    </div>
  );
}

/** Conexión de Server-Sent Events con la app; el navegador reconecta solo. */
function useEventos() {
  const [conectado, setConectado] = useState(false);
  useEffect(() => {
    const fuente = new EventSource(urlEventos());
    fuente.onopen = () => setConectado(true);
    fuente.onerror = () => setConectado(false);
    return () => fuente.close();
  }, []);
  return conectado;
}
