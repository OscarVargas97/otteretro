"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { pedir } from "@/lib/cliente";
import type { Disco, EstadoApp, PedidoCarpeta } from "@/lib/tipos";

const PASOS = [
  { titulo: "Bienvenida" },
  { titulo: "Carpeta de datos" },
  { titulo: "Respaldo", fase: 5 },
  { titulo: "Bibliotecas", fase: 3 },
  { titulo: "Steam", fase: 6 },
  { titulo: "Claves", fase: 1 },
  { titulo: "Carga inicial", fase: 2 },
];

const gb = (bytes: number | string) => `${Math.round(Number(bytes) / 1024 ** 3).toLocaleString("es")} GB`;

/** Asistente de primer arranque (pantallas.md §1). En F0 funcionan los pasos 1 y 2. */
export default function Bienvenida() {
  const [paso, setPaso] = useState(1);
  return (
    <div className="asistente">
      <aside className="asistente-pasos">
        <span className="barra-logo" style={{ padding: 0 }}>
          <span>otteretro</span>
        </span>
        <ol>
          {PASOS.map((p, i) => (
            <li key={p.titulo} className="ot-nav-item" aria-current={i + 1 === paso ? "page" : undefined} aria-disabled={p.fase ? "true" : undefined}>
              <span>{i + 1}. {p.titulo}</span>
              <span className="small">{i + 1 < paso ? "✓" : p.fase ? `Fase ${p.fase}` : ""}</span>
            </li>
          ))}
        </ol>
      </aside>
      <main className="asistente-cuerpo">
        <span className="small muted">Paso {paso} de {PASOS.length}</span>
        {paso === 1 ? <PasoBienvenida alSeguir={() => setPaso(2)} /> : <PasoCarpeta alVolver={() => setPaso(1)} />}
      </main>
    </div>
  );
}

function PasoBienvenida({ alSeguir }: { alSeguir: () => void }) {
  return (
    <>
      <h1>Otteretro</h1>
      <p className="muted">
        Cataloga tus sagas, organiza tus juegos, emuladores y ports en cualquier disco, y llévalo todo a Steam. Esto toma unos minutos.
      </p>
      <div>
        <button className="ot-btn ot-btn-primary" type="button" onClick={alSeguir}>Empezar</button>
      </div>
    </>
  );
}

function PasoCarpeta({ alVolver }: { alVolver: () => void }) {
  const router = useRouter();
  const cache = useQueryClient();
  const discos = useQuery({ queryKey: ["discos"], queryFn: () => pedir<Disco[]>("/api/discos") });
  const [ruta, setRuta] = useState("");
  // Por defecto, el disco con más espacio libre (normalmente no el del sistema).
  const mayor = discos.data?.reduce((a, b) => (Number(b.libre) > Number(a.libre) ? b : a), discos.data[0]);
  const elegida = ruta || (mayor ? `${mayor.nombre}Otteretro` : "");

  const guardar = useMutation({
    mutationFn: (pedido: PedidoCarpeta) => pedir<EstadoApp>("/api/carpeta-datos", { method: "POST", body: JSON.stringify(pedido) }),
    onSuccess: (estado) => {
      cache.setQueryData(["estado"], estado);
      router.replace("/");
    },
  });

  return (
    <>
      <h1>Elige la carpeta de datos</h1>
      <p className="muted">
        Aquí van la base de datos, las Herramientas (emuladores, ports, BIOS) y la caché de imágenes. Ocupa bastante: elige un disco con espacio.
        Para cambiar de PC basta con mover esta carpeta.
      </p>
      <div className="panel" style={{ gap: 0, padding: "0 16px" }}>
        {discos.data?.map((d) => {
          const opcion = `${d.nombre}Otteretro`;
          return (
            <label key={d.nombre} className="opcion-disco">
              <span>
                <input type="radio" name="disco" checked={elegida === opcion} onChange={() => setRuta(opcion)} /> <b>{opcion}</b>
              </span>
              <span className="small muted">{gb(d.libre)} libres de {gb(d.total)}</span>
            </label>
          );
        })}
      </div>
      <label className="small muted" htmlFor="ruta">O escribe otra ruta</label>
      <input id="ruta" value={elegida} onChange={(e) => setRuta(e.target.value)} />
      {guardar.isError && <p className="error">{guardar.error.message}</p>}
      <div style={{ display: "flex", gap: 8 }}>
        <button className="ot-btn" type="button" onClick={alVolver}>Atrás</button>
        <button className="ot-btn ot-btn-primary" type="button" disabled={!elegida || guardar.isPending} onClick={() => guardar.mutate({ ruta: elegida })}>
          {guardar.isPending ? "Preparando…" : `Usar ${elegida}`}
        </button>
      </div>
      <p className="small muted">Los pasos siguientes (respaldo, bibliotecas, Steam, claves y carga inicial) se activan con su fase.</p>
    </>
  );
}
