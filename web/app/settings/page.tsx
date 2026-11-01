"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Suspense } from "react";
import { ClaudeSettings } from "@/components/ClaudeSettings";
import { ConsoleSettings } from "@/components/ConsoleSettings";
import { CredentialsSettings } from "@/components/Credentials";
import { DataFolderSettings } from "@/components/DataFolder";
import { LanguagePicker } from "@/components/LanguagePicker";
import { StorageSettings } from "@/components/Libraries";
import { useAppState } from "@/components/Shell";
import { SideNav } from "@/components/SideNav";
import { UpdateChannel, VersionAndUpdate } from "@/components/Update";

/** Las secciones de Ajustes en grupos, en orden (#97, #111, #196). La que viene en `?s=` se abre; sin ella, Carpeta de datos. */
const GROUPS = [
  { id: "library", items: ["storage", "consoles"] },
  { id: "accounts", items: ["credentials", "claude"] },
  { id: "app", items: ["dataFolder", "general"] },
] as const;
type Section = (typeof GROUPS)[number]["items"][number];
const SECTIONS: readonly Section[] = GROUPS.flatMap((g) => g.items);
/** Las secciones de antes (#111, #199). */
const OLD: Record<string, Section> = { language: "general", about: "general", libraries: "storage" };

export default function Settings() {
  return <Suspense><SettingsSections /></Suspense>;
}

function SettingsSections() {
  const t = useTranslations("settings");
  const router = useRouter();
  const asked = useSearchParams().get("s");
  const current: Section = SECTIONS.find((s) => s === asked) ?? OLD[asked ?? ""] ?? "dataFolder";
  const title = (s: Section) => t(s === "consoles" ? "consoles.title" : s === "claude" ? "claude.title" : s);

  return (
    <div className="content">
      {/* Sin segundo título (#108): la sección elegida ya lo dice. */}
      <SideNav groups={GROUPS.map((g) => ({ label: t(`groups.${g.id}`), items: g.items.map((s) => ({ id: s, label: title(s) })) }))}
        current={current} label={t("sections")} panel="settings-panel" onSelect={(s) => router.push(`/settings/?s=${s}`)}>
        <Panel section={current} />
      </SideNav>
    </div>
  );
}

function Panel({ section }: { section: Section }) {
  const t = useTranslations("settings");
  const { data } = useAppState();
  switch (section) {
    case "dataFolder": return <DataFolderSettings />;
    case "storage": return <StorageSettings />;
    case "credentials": return <CredentialsSettings />;
    case "consoles": return <ConsoleSettings />;
    case "claude": return <ClaudeSettings />;
    case "general": return (
      <div className="settings-blocks">
        <section className="panel"><h2>{t("language")}</h2><LanguagePicker /></section>
        <VersionAndUpdate version={data?.version} />
        <UpdateChannel />
        <p className="small muted" style={{ margin: 0 }}>{t("later")}</p>
      </div>
    );
  }
}
