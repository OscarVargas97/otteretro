"use client";

import { useTranslations } from "next-intl";
import { CredentialsSettings } from "@/components/Credentials";
import { DataFolderSettings } from "@/components/DataFolder";
import { LanguagePicker } from "@/components/LanguagePicker";
import { useAppState } from "@/components/Shell";
import { VersionAndUpdate } from "@/components/Update";

export default function Settings() {
  const t = useTranslations("settings");
  const { data } = useAppState();
  return (
    <div className="content">
      <section className="sec">
        <div className="sec-h"><h2>{t("dataFolder")}</h2></div>
        <DataFolderSettings />
      </section>
      <section className="sec">
        <div className="sec-h"><h2>{t("credentials")}</h2></div>
        <CredentialsSettings />
      </section>
      <section className="sec">
        <div className="sec-h"><h2>{t("language")}</h2></div>
        <div className="panel"><LanguagePicker /></div>
      </section>
      <section className="sec">
        <div className="sec-h"><h2>{t("about")}</h2></div>
        <VersionAndUpdate version={data?.version} />
        <p className="small muted" style={{ margin: 0 }}>{t("later")}</p>
      </section>
    </div>
  );
}
