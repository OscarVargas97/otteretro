"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { rutaNormal } from "./Marco";

const PRINCIPAL = [
  { href: "/", texto: "Inicio" },
  { href: "/sagas/", texto: "Sagas" },
  { href: "/coleccion/", texto: "Colección" },
  { href: "/descubrir/", texto: "Descubrir" },
  { href: "/revisar/", texto: "Revisar" },
];
const SECUNDARIA = [
  { href: "/descargas/", texto: "Descargas" },
  { href: "/herramientas/", texto: "Herramientas" },
  { href: "/steam/", texto: "Steam" },
  { href: "/ajustes/", texto: "Ajustes" },
];

/** Barra lateral (componente NavItem del sistema de diseño). Los contadores llegan con cada módulo. */
export function Barra() {
  const actual = rutaNormal(usePathname());
  const activa = (href: string) => {
    const destino = rutaNormal(href);
    return destino === actual || (destino !== "/" && actual.startsWith(destino)) ? "page" : undefined;
  };
  const entrada = (e: { href: string; texto: string }) => (
    <Link key={e.href} className="ot-nav-item" href={e.href} aria-current={activa(e.href)}>
      {e.texto}
    </Link>
  );

  return (
    <aside className="barra">
      <Link href="/" className="barra-logo">
        <img src="/nutria.png" alt="" width={32} height={32} />
        <span>otteretro</span>
      </Link>
      <nav className="ot-nav barra-nav" aria-label="Principal">
        {PRINCIPAL.map(entrada)}
        <div className="ot-nav-sep" />
        {SECUNDARIA.map(entrada)}
      </nav>
    </aside>
  );
}
