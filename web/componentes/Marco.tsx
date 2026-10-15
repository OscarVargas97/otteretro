"use client";

import { QueryClient, QueryClientProvider, useQuery } from "@tanstack/react-query";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { pedir } from "@/lib/cliente";
import type { EstadoApp } from "@/lib/tipos";
import { Barra } from "./Barra";
import { BarraSuperior } from "./BarraSuperior";

const TITULOS: Record<string, string> = {
  "/": "Inicio",
  "/sagas": "Sagas",
  "/saga": "Saga",
  "/coleccion": "Colección",
  "/juego": "Juego",
  "/descubrir": "Descubrir",
  "/revisar": "Revisar",
  "/descargas": "Descargas",
  "/herramientas": "Herramientas",
  "/steam": "Steam",
  "/ajustes": "Ajustes",
};

/** Quita la barra final que añade trailingSlash, salvo en la raíz. */
export const rutaNormal = (ruta: string) => (ruta.length > 1 ? ruta.replace(/\/$/, "") : ruta);

const consultas = new QueryClient({ defaultOptions: { queries: { refetchOnWindowFocus: false } } });

export function Marco({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={consultas}>
      <Contenido>{children}</Contenido>
    </QueryClientProvider>
  );
}

function Contenido({ children }: { children: ReactNode }) {
  const ruta = rutaNormal(usePathname());
  const router = useRouter();
  const estado = useQuery({ queryKey: ["estado"], queryFn: () => pedir<EstadoApp>("/api/estado") });
  const enAsistente = ruta === "/bienvenida";

  // Sin carpeta de datos, todo lleva al asistente de primer arranque.
  useEffect(() => {
    if (estado.data && !estado.data.configurada && !enAsistente) router.replace("/bienvenida/");
  }, [estado.data, enAsistente, router]);

  if (estado.isError)
    return <p className="content">No se pudo hablar con la app: {estado.error.message}. Ciérrala y vuelve a abrirla.</p>;
  if (!estado.data) return null;
  if (enAsistente) return <>{children}</>;

  return (
    <div className="app">
      <Barra />
      <main className="app-main">
        <BarraSuperior titulo={TITULOS[ruta] ?? "Otteretro"} />
        {children}
      </main>
    </div>
  );
}

/** Estado vacío con la nutria y, si la hay, la acción que lo resuelve. */
export function Vacio({ titulo, texto, children }: { titulo: string; texto: string; children?: ReactNode }) {
  return (
    <div className="ot-empty">
      <img src="/nutria.png" alt="" width={64} height={64} />
      <h3>{titulo}</h3>
      <p>{texto}</p>
      {children}
    </div>
  );
}

/** Para pantallas que se activan en una fase posterior. */
export function Proximamente({ titulo, texto, fase }: { titulo: string; texto: string; fase: number }) {
  return (
    <div className="content">
      <Vacio titulo={titulo} texto={texto}>
        <span className="ot-badge">Llega en la fase {fase}</span>
      </Vacio>
    </div>
  );
}
