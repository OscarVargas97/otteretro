"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { LanguagePicker } from "@/components/LanguagePicker";
import { CredentialsSettings } from "@/components/Credentials";
import { TaskRows } from "@/components/Tasks";
import { request } from "@/lib/client";
import { gb } from "@/lib/format";
import { useDescribeError } from "@/lib/i18n";
import { SteamAccount, SteamGroups, steamExportQuery, steamQuery, useExport } from "@/components/Steam";
import type { AdoptResult, AppState, Drive, FolderRequest, DiskInfo, TaskInfo } from "@/lib/types";

// Clave en welcome.steps.* de cada paso (#418: eran 8, quedan 4; lo demás va plegado o en el paso opcional).
const STEPS = ["welcome", "where", "steam", "load"] as const;

/** Asistente de primer arranque (pantallas.md §1, #418): Bienvenida, Dónde guardar, Steam y cuentas (opcional) y Carga inicial. */
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
          {STEPS.map((key, i) => (
            <li key={key} className="ot-nav-item" aria-current={i + 1 === step ? "page" : undefined}>
              <span>{i + 1}. {t(`steps.${key}`)}</span>
              {i + 1 < step ? <span className="small">✓</span> : <span />}
            </li>
          ))}
        </ol>
      </aside>
      <main className="wizard-body">
        <span className="small muted">{t("stepOf", { step, total: STEPS.length })}</span>
        {step === 1 ? <WelcomeStep onNext={() => setStep(2)} />
          : step === 2 ? <WhereStep onBack={() => setStep(1)} onNext={() => setStep(3)} />
          : step === 3 ? <SteamStep onBack={() => setStep(2)} onNext={() => setStep(4)} />
          : <LoadStep />}
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

/** Paso 2 (#418): los discos a la vista; la carpeta de datos y adoptar una ruta con la estructura antigua (§5.5), plegadas; el respaldo es de la F7.
 *  Los discos viven en la base de la carpeta de datos, así que «Seguir» la guarda primero, después agrega los discos y ofrece adoptar lo que tenga la estructura antigua (§5.5). */
function WhereStep({ onBack, onNext }: { onBack: () => void; onNext: () => void }) {
  const t = useTranslations("welcome");
  const locale = useLocale();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const drives = useQuery({ queryKey: ["drives"], queryFn: () => request<Drive[]>("/api/drives") });
  // Ya guardada si se volvió a este paso: con el escaneo en marcha el backend no deja cambiarla.
  const saved = cache.getQueryData<AppState>(["welcome-state"])?.dataFolder ?? undefined;
  const [path, setPath] = useState("");
  // La sugerida la decide el servidor: ~/Otteretro en Linux, el disco con más espacio libre en Windows.
  const folder = saved ?? (path || drives.data?.find((d) => d.suggested)?.folder || "");
  const [marked, setMarked] = useState<string[]>([]);
  const [added, setAdded] = useState<string[]>([]);
  const [legacy, setLegacy] = useState("");
  const [found, setFound] = useState<string[] | null>(null);

  const go = useMutation({
    mutationFn: async () => {
      if (!saved) {
        const body: FolderRequest = { path: folder };
        cache.setQueryData(["welcome-state"], await request<AppState>("/api/data-folder", { method: "POST", body: JSON.stringify(body) }));
      }
      for (const d of drives.data?.filter((d) => marked.includes(d.folder) && !added.includes(d.folder)) ?? []) {
        await request<DiskInfo[]>("/api/disks", { method: "POST", body: JSON.stringify({ path: d.folder, name: d.name.replace(/[\\/]+$/, "") || d.name }) });
        setAdded((a) => [...a, d.folder]);
      }
      if (legacy) {
        await request<AdoptResult>("/api/locations/adopt", { method: "POST", body: JSON.stringify({ path: legacy }) });
        setLegacy("");
      }
      // Lo que tenga la estructura antigua se ofrece una vez antes de seguir.
      if (found === null) {
        const list = await request<string[]>("/api/locations/detect");
        setFound(list);
        if (list.length > 0) return;
      }
      onNext();
    },
  });
  const adopt = useMutation({
    mutationFn: (p: string) => request<AdoptResult>("/api/locations/adopt", { method: "POST", body: JSON.stringify({ path: p }) }),
    onSuccess: onNext,
  });
  const error = go.error ?? adopt.error;

  return (
    <>
      <h1>{t("whereTitle")}</h1>
      <p className="muted">{t("whereText")}</p>
      <div className="panel" style={{ gap: 0, padding: "0 16px" }}>
        {drives.data?.map((d) => (
          <label key={d.name} className="drive-option">
            <span>
              <input type="checkbox" checked={marked.includes(d.folder)} disabled={added.includes(d.folder)}
                onChange={(e) => setMarked((m) => (e.target.checked ? [...m, d.folder] : m.filter((x) => x !== d.folder)))} /> <b>{d.folder}</b>
            </span>
            <span className="small muted">{t("freeOf", { free: gb(d.free, locale), total: gb(d.total, locale) })}</span>
          </label>
        ))}
      </div>
      {found && found.length > 0 && (
        <div className="panel">
          <span className="small">{t("librariesFound")}</span>
          {found.map((p) => (
            <button key={p} className="ot-btn ot-btn-primary" type="button" disabled={adopt.isPending} onClick={() => adopt.mutate(p)}>{t("adoptThis", { path: p })}</button>
          ))}
        </div>
      )}
      <details className="fold-line">
        <summary className="small muted">{t("more")}</summary>
        <div style={{ display: "flex", flexDirection: "column", gap: 8, paddingBottom: 8 }}>
          <label className="small muted" htmlFor="path">{t("dataFolder")}</label>
          <input id="path" value={folder} disabled={!!saved} onChange={(e) => setPath(e.target.value)} />
          <p className="small muted">{t("dataFolderText")}</p>
          {/* Adoptar una ruta escrita a mano solo existe aquí. */}
          <label className="small muted" htmlFor="legacy">{t("legacy")}</label>
          <input id="legacy" value={legacy} onChange={(e) => setLegacy(e.target.value)} />
        </div>
      </details>
      {error && <p className="error">{describe(error)}</p>}
      <div style={{ display: "flex", gap: 8 }}>
        <button className="ot-btn" type="button" onClick={onBack}>{t("back")}</button>
        <button className="ot-btn ot-btn-primary" type="button" disabled={!folder || go.isPending || adopt.isPending} onClick={() => go.mutate()}>
          {go.isPending ? t("preparing") : t("next")}
        </button>
      </div>
    </>
  );
}

/** Paso 3 (#402, #418), opcional: la cuenta de Steam detectada, marcar qué llevar y exportar; las claves y cuentas de descarga, plegadas.
 *  Todo se puede hacer después en Steam y en Ajustes › Cuentas y claves. */
function SteamStep({ onBack, onNext }: { onBack: () => void; onNext: () => void }) {
  const t = useTranslations("welcome");
  const tSteam = useTranslations("steam");
  const describe = useDescribeError();
  const status = useQuery(steamQuery);
  const changes = useQuery(steamExportQuery);
  const { run } = useExport();
  const s = status.data;
  const marked = changes.data?.entries ?? [];
  return (
    <>
      <h1>{t("steamTitle")}</h1>
      <p className="muted">{t("steamText")}</p>
      {s && <div className="panel"><SteamAccount status={s} /></div>}
      {s?.account && (marked.length + s.available.length === 0
        ? <p className="small muted">{t("steamNothing")}</p>
        : <><SteamGroups entries={marked} marked /><SteamGroups entries={s.available} marked={false} /></>)}
      <details className="fold-line">
        <summary className="small muted">{t("keys")}</summary>
        <CredentialsSettings />
      </details>
      {(status.error ?? changes.error ?? run.error) && <p className="error">{describe((status.error ?? changes.error ?? run.error)!)}</p>}
      <div style={{ display: "flex", gap: 8 }}>
        <button className="ot-btn" type="button" onClick={onBack}>{t("back")}</button>
        <button className="ot-btn" type="button" onClick={onNext}>{t("later")}</button>
        <button className="ot-btn ot-btn-primary" type="button" disabled={!s?.account || marked.length === 0 || run.isPending}
          onClick={() => run.mutate(undefined, { onSuccess: onNext })}>{tSteam("export")}</button>
      </div>
    </>
  );
}

/** Paso 4 (#418): el escaneo de los discos marcados (se encola solo al agregarlos, #212) sigue en segundo plano; se puede entrar ya. */
function LoadStep() {
  const t = useTranslations("welcome");
  const router = useRouter();
  const cache = useQueryClient();
  // El asistente no escucha los eventos de la app: pregunta cada 2 s.
  const tasks = useQuery({ queryKey: ["tasks"], queryFn: () => request<TaskInfo[]>("/api/tasks"), refetchInterval: 2000 });
  const finish = () => {
    const state = cache.getQueryData<AppState>(["welcome-state"]);
    if (state) cache.setQueryData(["state"], state);
    router.replace("/");
  };
  return (
    <>
      <h1>{t("loadTitle")}</h1>
      <p className="muted">{t("loadText")}</p>
      {tasks.data?.length ? <div className="panel"><TaskRows /></div> : <p className="small muted">{t("loadNone")}</p>}
      <div>
        <button className="ot-btn ot-btn-primary" type="button" onClick={finish}>{t("enter")}</button>
      </div>
    </>
  );
}
