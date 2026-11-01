"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { LanguagePicker } from "@/components/LanguagePicker";
import { request } from "@/lib/client";
import { gb } from "@/lib/format";
import { useDescribeError } from "@/lib/i18n";
import type { AdoptResult, AppState, Drive, FolderRequest, DiskInfo } from "@/lib/types";

// Clave en welcome.steps.* y fase en que se activa cada paso.
const STEPS = [
  { key: "welcome" },
  { key: "dataFolder" },
  { key: "backup", phase: 7 },
  { key: "libraries" },
  { key: "steam", phase: 8 },
  { key: "keys", phase: 1 },
  { key: "initialLoad", phase: 2 },
] as const;

/** Asistente de primer arranque (pantallas.md §1). Funcionan los pasos 1, 2 y 4 (bibliotecas, F3); el resto llega con su fase. */
export default function Welcome() {
  const t = useTranslations("welcome");
  const [step, setStep] = useState(1);
  return (
    <div className="wizard">
      <aside className="wizard-steps">
        <span className="sidebar-logo" style={{ padding: 0 }}>
          <img src="/nutria.png" alt="" width={32} height={32} />
          <span>otteretro</span>
        </span>
        <ol>
          {STEPS.map((s, i) => {
            const phase = "phase" in s ? s.phase : undefined;
            return (
              <li key={s.key} className="ot-nav-item" aria-current={i + 1 === step ? "page" : undefined} aria-disabled={phase ? "true" : undefined}>
                <span>{i + 1}. {t(`steps.${s.key}`)}</span>
                {i + 1 < step ? <span className="small">✓</span> : phase ? <span className="ot-badge" title={t("phase", { phase })}>F{phase}</span> : <span />}
              </li>
            );
          })}
        </ol>
      </aside>
      <main className="wizard-body">
        <span className="small muted">{t("stepOf", { step, total: STEPS.length })}</span>
        {step === 1 ? <WelcomeStep onNext={() => setStep(2)} />
          : step === 2 ? <FolderStep onBack={() => setStep(1)} onNext={() => setStep(4)} />
          : <LibrariesStep />}
      </main>
    </div>
  );
}

function WelcomeStep({ onNext }: { onNext: () => void }) {
  const t = useTranslations("welcome");
  return (
    <>
      {/* La nutria da la bienvenida (#172). */}
      <img className="welcome-otter" src="/nutria.png" alt="" width={96} height={96} />
      <h1>Otteretro</h1>
      <p className="muted">{t("intro")}</p>
      <LanguagePicker />
      <div>
        <button className="ot-btn ot-btn-primary" type="button" onClick={onNext}>{t("start")}</button>
      </div>
    </>
  );
}

function FolderStep({ onBack, onNext }: { onBack: () => void; onNext: () => void }) {
  const t = useTranslations("welcome");
  const locale = useLocale();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const setChosen = (state: AppState) => cache.setQueryData(["welcome-state"], state);
  const drives = useQuery({ queryKey: ["drives"], queryFn: () => request<Drive[]>("/api/drives") });
  const [path, setPath] = useState("");
  // La sugerida la decide el servidor: ~/Otteretro en Linux, el disco con más espacio libre en Windows.
  const chosen = path || drives.data?.find((d) => d.suggested)?.folder || "";

  const save = useMutation({
    mutationFn: (body: FolderRequest) => request<AppState>("/api/data-folder", { method: "POST", body: JSON.stringify(body) }),
    // La app queda configurada recién al terminar el asistente: el paso 4 usa la carpeta de datos ya elegida.
    onSuccess: (state) => {
      setChosen(state);
      onNext();
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
    </>
  );
}

/** Paso 4 (§5.5, #201, #202): elegir los discos, adoptar la carpeta con la estructura antigua que se encuentre (pasa al estándar sola) o saltar. */
function LibrariesStep() {
  const t = useTranslations("welcome");
  const describe = useDescribeError();
  const router = useRouter();
  const cache = useQueryClient();
  const found = useQuery({ queryKey: ["libraries", "detect"], queryFn: () => request<string[]>("/api/locations/detect") });
  const [path, setPath] = useState("");
  const drives = useQuery({ queryKey: ["drives"], queryFn: () => request<Drive[]>("/api/drives") });
  const [added, setAdded] = useState<string[]>([]);
  const finish = () => {
    const state = cache.getQueryData<AppState>(["welcome-state"]);
    if (state) cache.setQueryData(["state"], state);
    router.replace("/");
  };
  const adopt = useMutation({
    mutationFn: (p: string) => request<AdoptResult>("/api/locations/adopt", { method: "POST", body: JSON.stringify({ path: p }) }),
    onSuccess: finish,
  });
  const create = useMutation({
    mutationFn: (d: Drive) => request<DiskInfo[]>("/api/disks", { method: "POST", body: JSON.stringify({ path: d.folder, name: d.name.replace(/[\\/]+$/, "") || d.name }) }),
    onSuccess: (_, d) => setAdded((a) => [...a, d.folder]),
  });
  const error = adopt.error ?? create.error;

  return (
    <>
      <h1>{t("librariesTitle")}</h1>
      <p className="muted">{t("librariesText")}</p>
      {found.data && found.data.length > 0 && (
        <div className="panel">
          <span className="small">{t("librariesFound")}</span>
          {found.data.map((p) => (
            <button key={p} className="ot-btn ot-btn-primary" type="button" disabled={adopt.isPending} onClick={() => adopt.mutate(p)}>{t("adoptThis", { path: p })}</button>
          ))}
        </div>
      )}
      <div className="panel">
        <span className="small">{t("disksText")}</span>
        {drives.data?.map((d) => (
          <button key={d.name} className="ot-btn" type="button" disabled={create.isPending || added.includes(d.folder)} onClick={() => create.mutate(d)}>
            {added.includes(d.folder) ? t("diskAdded", { path: d.folder }) : t("useDisk", { path: d.folder })}
          </button>
        ))}
      </div>
      <label className="small muted" htmlFor="library">{t("otherPath")}</label>
      <input id="library" value={path} onChange={(e) => setPath(e.target.value)} />
      {error && <p className="error">{describe(error)}</p>}
      <div style={{ display: "flex", gap: 8 }}>
        <button className="ot-btn" type="button" onClick={finish}>{added.length > 0 ? t("done") : t("skip")}</button>
        <button className="ot-btn ot-btn-primary" type="button" disabled={!path || adopt.isPending} onClick={() => adopt.mutate(path)}>{t("adoptThis", { path })}</button>
      </div>
      <p className="small muted">{t("nextSteps")}</p>
    </>
  );
}
