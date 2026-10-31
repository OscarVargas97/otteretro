"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Suspense } from "react";
import { ClaudeSettings } from "@/components/ClaudeSettings";
import { ConsoleSettings } from "@/components/ConsoleSettings";
import { CredentialsSettings } from "@/components/Credentials";
import { DataFolderSettings } from "@/components/DataFolder";
import { LanguagePicker } from "@/components/LanguagePicker";
import { useAppState } from "@/components/Shell";
import { VersionAndUpdate } from "@/components/Update";

/** Las pestañas de Ajustes, en orden (#97). La que viene en `?s=` se abre; sin ella, la primera. */
const SECTIONS = ["dataFolder", "credentials", "consoles", "claude", "language", "about"] as const;
type Section = (typeof SECTIONS)[number];

export default function Settings() {
  return <Suspense><SettingsTabs /></Suspense>;
}

function SettingsTabs() {
  const t = useTranslations("settings");
  const router = useRouter();
  const asked = useSearchParams().get("s");
  const current: Section = SECTIONS.find((s) => s === asked) ?? SECTIONS[0];
  const title = (s: Section) => t(s === "consoles" ? "consoles.title" : s === "claude" ? "claude.title" : s);
  const href = (s: Section) => `/settings/?s=${s}`;

  // Flechas, Inicio y Fin mueven entre pestañas, como pide el patrón de pestañas accesible.
  const onKeyDown = (e: React.KeyboardEvent) => {
    const at = SECTIONS.indexOf(current);
    const to = { ArrowRight: at + 1, ArrowLeft: at - 1, Home: 0, End: SECTIONS.length - 1 }[e.key];
    if (to === undefined) return;
    e.preventDefault();
    const next = SECTIONS[(to + SECTIONS.length) % SECTIONS.length];
    router.push(href(next));
    document.getElementById(`tab-${next}`)?.focus();
  };

  return (
    <div className="content">
      <nav className="tabs tabs-scroll" role="tablist" aria-label={t("sections")} onKeyDown={onKeyDown}>
        {SECTIONS.map((s) => (
          <Link key={s} id={`tab-${s}`} href={href(s)} className="tab" role="tab" aria-selected={s === current} aria-controls="settings-panel" tabIndex={s === current ? 0 : -1}>
            {title(s)}
          </Link>
        ))}
      </nav>
      <section className="sec" id="settings-panel" role="tabpanel">
        <div className="sec-h"><h2>{title(current)}</h2></div>
        <Panel section={current} />
      </section>
    </div>
  );
}

function Panel({ section }: { section: Section }) {
  const t = useTranslations("settings");
  const { data } = useAppState();
  switch (section) {
    case "dataFolder": return <DataFolderSettings />;
    case "credentials": return <CredentialsSettings />;
    case "consoles": return <ConsoleSettings />;
    case "claude": return <ClaudeSettings />;
    case "language": return <div className="panel"><LanguagePicker /></div>;
    case "about": return (
      <>
        <VersionAndUpdate version={data?.version} />
        <p className="small muted" style={{ margin: 0 }}>{t("later")}</p>
      </>
    );
  }
}
