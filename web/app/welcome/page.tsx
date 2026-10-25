"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { LanguagePicker } from "@/components/LanguagePicker";
import { request } from "@/lib/client";
import { gb } from "@/lib/format";
import { useDescribeError } from "@/lib/i18n";
import type { AppState, Drive, FolderRequest } from "@/lib/types";

// Clave en welcome.steps.* y fase en que se activa cada paso.
const STEPS = [
  { key: "welcome" },
  { key: "dataFolder" },
  { key: "backup", phase: 5 },
  { key: "libraries", phase: 3 },
  { key: "steam", phase: 6 },
  { key: "keys", phase: 1 },
  { key: "initialLoad", phase: 2 },
] as const;

/** Asistente de primer arranque (pantallas.md §1). En F0 funcionan los pasos 1 y 2. */
export default function Welcome() {
  const t = useTranslations("welcome");
  const [step, setStep] = useState(1);
  return (
    <div className="wizard">
      <aside className="wizard-steps">
        <span className="sidebar-logo" style={{ padding: 0 }}>
          <span>otteretro</span>
        </span>
        <ol>
          {STEPS.map((s, i) => {
            const phase = "phase" in s ? s.phase : undefined;
            return (
              <li key={s.key} className="ot-nav-item" aria-current={i + 1 === step ? "page" : undefined} aria-disabled={phase ? "true" : undefined}>
                <span>{i + 1}. {t(`steps.${s.key}`)}</span>
                <span className="small">{i + 1 < step ? "✓" : phase ? t("phase", { phase }) : ""}</span>
              </li>
            );
          })}
        </ol>
      </aside>
      <main className="wizard-body">
        <span className="small muted">{t("stepOf", { step, total: STEPS.length })}</span>
        {step === 1 ? <WelcomeStep onNext={() => setStep(2)} /> : <FolderStep onBack={() => setStep(1)} />}
      </main>
    </div>
  );
}

function WelcomeStep({ onNext }: { onNext: () => void }) {
  const t = useTranslations("welcome");
  return (
    <>
      <h1>Otteretro</h1>
      <p className="muted">{t("intro")}</p>
      <LanguagePicker />
      <div>
        <button className="ot-btn ot-btn-primary" type="button" onClick={onNext}>{t("start")}</button>
      </div>
    </>
  );
}

function FolderStep({ onBack }: { onBack: () => void }) {
  const t = useTranslations("welcome");
  const locale = useLocale();
  const describe = useDescribeError();
  const router = useRouter();
  const cache = useQueryClient();
  const drives = useQuery({ queryKey: ["drives"], queryFn: () => request<Drive[]>("/api/drives") });
  const [path, setPath] = useState("");
  // La sugerida la decide el servidor: ~/Otteretro en Linux, el disco con más espacio libre en Windows.
  const chosen = path || drives.data?.find((d) => d.suggested)?.folder || "";

  const save = useMutation({
    mutationFn: (body: FolderRequest) => request<AppState>("/api/data-folder", { method: "POST", body: JSON.stringify(body) }),
    onSuccess: (state) => {
      cache.setQueryData(["state"], state);
      router.replace("/");
    },
  });

  return (
    <>
      <h1>{t("chooseTitle")}</h1>
      <p className="muted">{t("chooseText")}</p>
      <div className="panel" style={{ gap: 0, padding: "0 16px" }}>
        {drives.data?.map((d) => {
          const option = d.folder;
          return (
            <label key={d.name} className="drive-option">
              <span>
                <input type="radio" name="drive" checked={chosen === option} onChange={() => setPath(option)} /> <b>{option}</b>
              </span>
              <span className="small muted">{t("freeOf", { free: gb(d.free, locale), total: gb(d.total, locale) })}</span>
            </label>
          );
        })}
      </div>
      <label className="small muted" htmlFor="path">{t("otherPath")}</label>
      <input id="path" value={chosen} onChange={(e) => setPath(e.target.value)} />
      {save.isError && <p className="error">{describe(save.error)}</p>}
      <div style={{ display: "flex", gap: 8 }}>
        <button className="ot-btn" type="button" onClick={onBack}>{t("back")}</button>
        <button className="ot-btn ot-btn-primary" type="button" disabled={!chosen || save.isPending} onClick={() => save.mutate({ path: chosen })}>
          {save.isPending ? t("preparing") : t("use", { path: chosen })}
        </button>
      </div>
      <p className="small muted">{t("nextSteps")}</p>
    </>
  );
}
