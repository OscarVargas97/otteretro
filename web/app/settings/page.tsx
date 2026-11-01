"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Suspense } from "react";
import { ClaudeSettings } from "@/components/ClaudeSettings";
import { ConsoleSettings } from "@/components/ConsoleSettings";
import { CredentialsSettings } from "@/components/Credentials";
import { DataFolderSettings } from "@/components/DataFolder";
import { LanguagePicker } from "@/components/LanguagePicker";
import { useAppState } from "@/components/Shell";
import { Tabs } from "@/components/Tabs";
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

  return (
    <div className="content">
      <Tabs tabs={SECTIONS.map((s) => ({ id: s, label: title(s) }))} current={current} label={t("sections")} panel="settings-panel"
        onSelect={(s) => router.push(`/settings/?s=${s}`)} />
      {/* Sin segundo título (#108): la pestaña ya lo dice. */}
      <section className="sec" id="settings-panel" role="tabpanel" aria-labelledby={`tab-${current}`}>
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
