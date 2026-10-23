"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useTranslations } from "next-intl";
import { normalizePath } from "./Shell";

// Clave en nav.* de cada entrada.
const MAIN = [
  { href: "/", key: "home" },
  { href: "/sagas/", key: "sagas" },
  { href: "/collection/", key: "collection" },
  { href: "/discover/", key: "discover" },
  { href: "/review/", key: "review" },
] as const;
const SECONDARY = [
  { href: "/downloads/", key: "downloads" },
  { href: "/tools/", key: "tools" },
  { href: "/steam/", key: "steam" },
  { href: "/settings/", key: "settings" },
] as const;

/** Barra lateral (componente NavItem del sistema de diseño). Los contadores llegan con cada módulo. */
export function Sidebar() {
  const t = useTranslations("nav");
  const current = normalizePath(usePathname());
  const isActive = (href: string) => {
    const target = normalizePath(href);
    return target === current || (target !== "/" && current.startsWith(target)) ? "page" : undefined;
  };
  const item = (e: (typeof MAIN)[number] | (typeof SECONDARY)[number]) => (
    <Link key={e.href} className="ot-nav-item" href={e.href} aria-current={isActive(e.href)}>
      {t(e.key)}
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
