"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useTranslations } from "next-intl";
import { request } from "@/lib/client";
import { normalizePath, useAppState } from "./Shell";

// Clave en nav.* de cada entrada; `phase`, la fase en que llega: hasta entonces se ve atenuada y no se abre (#169).
type Entry = { href: string; key: "home" | "sagas" | "collection" | "discover" | "review" | "downloads" | "tools" | "steam" | "settings"; phase?: number };
const MAIN: Entry[] = [
  { href: "/", key: "home" },
  { href: "/sagas/", key: "sagas" },
  { href: "/collection/", key: "collection" },
  { href: "/discover/", key: "discover", phase: 9 },
  { href: "/review/", key: "review" },
];
const SECONDARY: Entry[] = [
  { href: "/downloads/", key: "downloads", phase: 8 },
  { href: "/tools/", key: "tools", phase: 4 },
  { href: "/steam/", key: "steam", phase: 6 },
  { href: "/settings/", key: "settings" },
];

/** Barra lateral (componente NavItem del sistema de diseño). Los contadores llegan con cada módulo; Revisar lleva el suyo (D3). */
export function Sidebar() {
  const t = useTranslations("nav");
  // Sin carpeta de datos la API responde 409: sin contador.
  // El menú se dibuja antes de saber si hay carpeta de datos (#105); sin ella no hay qué contar.
  const configured = !!useAppState().data?.configured;
  const review = useQuery({ queryKey: ["review", "count"], queryFn: () => request<number>("/api/review/count"), refetchInterval: 60_000, retry: false, enabled: configured });
  const counts: Partial<Record<string, number>> = { review: Number(review.data ?? 0) };
  const current = normalizePath(usePathname());
  const isActive = (href: string) => {
    const target = normalizePath(href);
    return target === current || (target !== "/" && current.startsWith(target)) ? "page" : undefined;
  };
  const item = (e: Entry) => e.phase ? (
    <span key={e.href} className="ot-nav-item is-future" aria-disabled="true" title={t("comingIn", { phase: e.phase })}>
      {t(e.key)}
      <span className="ot-badge">F{e.phase}</span>
    </span>
  ) : (
    <Link key={e.href} className="ot-nav-item" href={e.href} aria-current={isActive(e.href)}>
      {t(e.key)}
      {!!counts[e.key] && <span className="ot-count" aria-label={t("pending", { count: counts[e.key]! })}>{counts[e.key]}</span>}
    </Link>
  );

  return (
    <aside className="sidebar">
      <Link href="/" className="sidebar-logo">
        <img src="/nutria.png" alt="" width={32} height={32} />
        <span>otteretro</span>
      </Link>
      <nav className="ot-nav sidebar-nav" aria-label={t("label")}>
        {MAIN.map(item)}
        <div className="ot-nav-sep" />
        {SECONDARY.map(item)}
      </nav>
    </aside>
  );
}
