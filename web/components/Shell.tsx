"use client";

import { QueryClient, QueryClientProvider, useQuery } from "@tanstack/react-query";
import { NextIntlClientProvider, useTranslations } from "next-intl";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { AppState } from "@/lib/types";
import { CATALOGS, isLocale, systemLocale } from "@/messages";
import { Gamepad } from "./Gamepad";
import { UpdateNotice } from "./Update";
import { Sidebar } from "./Sidebar";
import { TopBar } from "./TopBar";

// Clave del título (nav.*) de cada pantalla.
const TITLES = {
  "/": "home",
  "/sagas": "sagas",
  "/saga": "saga",
  "/collection": "collection",
  "/game": "game",
  "/discover": "discover",
  "/review": "review",
  "/downloads": "downloads",
  "/tools": "tools",
  "/steam": "steam",
  "/settings": "settings",
} as const;

/** Quita la barra final que añade trailingSlash, salvo en la raíz. */
export const normalizePath = (path: string) => (path.length > 1 ? path.replace(/\/$/, "") : path);

const queryClient = new QueryClient({ defaultOptions: { queries: { refetchOnWindowFocus: false } } });

export const useAppState = () => useQuery({ queryKey: ["state"], queryFn: () => request<AppState>("/api/state") });

export function Shell({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <Translations>
        <Content>{children}</Content>
        <Gamepad />
      </Translations>
    </QueryClientProvider>
  );
}

/** Carga el catálogo del idioma elegido (#140). Hasta saberlo, el del sistema. */
function Translations({ children }: { children: ReactNode }) {
  const language = useAppState().data?.language;
  const locale = isLocale(language) ? language : systemLocale();
  useEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);
  return (
    <NextIntlClientProvider locale={locale} messages={CATALOGS[locale]} timeZone={Intl.DateTimeFormat().resolvedOptions().timeZone}>
      {children}
    </NextIntlClientProvider>
  );
}

function Content({ children }: { children: ReactNode }) {
  const t = useTranslations();
  const describe = useDescribeError();
  const path = normalizePath(usePathname());
  const router = useRouter();
  const state = useAppState();
  const inWizard = path === "/welcome";

  // Sin carpeta de datos, todo lleva al asistente de primer arranque.
  useEffect(() => {
    if (state.data && !state.data.configured && !inWizard) router.replace("/welcome/");
  }, [state.data, inWizard, router]);

  if (state.isError) return <p className="content">{t("app.unreachable", { detail: describe(state.error) })}</p>;
  if (!state.data) return null;
  if (inWizard) return <>{children}</>;

  const title = TITLES[path as keyof typeof TITLES];
  return (
    <div className="app">
      <Sidebar />
      <main className="app-main">
        <TopBar title={title ? t(`nav.${title}`) : "Otteretro"} />
        <UpdateNotice />
        {children}
      </main>
    </div>
  );
}

/** Estado vacío con la nutria y, si la hay, la acción que lo resuelve. */
export function Empty({ title, text, children }: { title: string; text: string; children?: ReactNode }) {
  return (
    <div className="ot-empty">
      <img src="/nutria.png" alt="" width={64} height={64} />
      <h3>{title}</h3>
      <p>{text}</p>
      {children}
    </div>
  );
}

/** Para pantallas que se activan en una fase posterior. `screen` es su clave en comingSoon.*. */
export function ComingSoon({ screen, phase }: { screen: "home" | "sagas" | "saga" | "collection" | "game" | "discover" | "review" | "downloads" | "tools" | "steam"; phase: number }) {
  const t = useTranslations("comingSoon");
  return (
    <div className="content">
      <Empty title={t(`${screen}.title`)} text={t(`${screen}.text`)}>
        <span className="ot-badge">{t("badge", { phase })}</span>
      </Empty>
    </div>
  );
}
