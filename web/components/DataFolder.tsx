"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { request } from "@/lib/client";
import { gb } from "@/lib/format";
import { useDescribeError, useDynamicT } from "@/lib/i18n";
import type { AppState, Drive, FolderInfo, FolderRequest, MoveState } from "@/lib/types";

/** Estado de la mudanza de la carpeta de datos; llega en vivo por el evento "move". */
export const useMove = () =>
  useQuery({ queryKey: ["move"], queryFn: () => request<MoveState>("/api/data-folder/move") }).data;

const percent = (m: MoveState) => (Number(m.total) > 0 ? Math.round((Number(m.done) / Number(m.total)) * 100) : 0);

/** Nombre del paso en el idioma activo; el backend manda su código (move.step.*). */
function useStepName() {
  const t = useDynamicT();
  return (step: string) => (t.has(`move.step.${step}`) ? t(`move.step.${step}`) : step);
}

/** Fila del panel de tareas mientras se mueve la carpeta de datos (o con su resultado). */
export function MoveTask() {
  const t = useTranslations("dataFolder");
  const describe = useDescribeError();
  const stepName = useStepName();
  const m = useMove();
  if (!m || !(m.running || m.step === "done" || m.step === "error")) return null;
  const destination = m.destination ?? "";
  return (
    <div className={`ot-task${m.error ? " is-error" : ""}`}>
      <span className="ot-task-title">{t("taskTitle")}</span>
      <span className="ot-task-state">{m.running ? `${percent(m)} %` : m.error ? t("error") : t("taskDone")}</span>
      {m.running && <div className="ot-progress"><span style={{ width: `${percent(m)}%` }} /></div>}
      <span className="ot-task-detail">
        {m.error ? describe(m.error) : m.running ? t("progress", { step: stepName(m.step), destination }) : t("taskNowIn", { destination })}
      </span>
    </div>
  );
}

/** Ajustes › Carpeta de datos: dónde está, cuánto ocupa, y moverla o usar otra (S7). */
export function DataFolderSettings() {
  const t = useTranslations("dataFolder");
  const locale = useLocale();
  const describe = useDescribeError();
  const stepName = useStepName();
  const cache = useQueryClient();
  const info = useQuery({ queryKey: ["folder"], queryFn: () => request<FolderInfo>("/api/data-folder") }).data;
  const drives = useQuery({ queryKey: ["drives"], queryFn: () => request<Drive[]>("/api/drives") }).data;
  const m = useMove();
  const [mode, setMode] = useState<"move" | "use" | null>(null);
  const [path, setPath] = useState("");

  const refresh = () => {
    cache.invalidateQueries({ queryKey: ["state"] });
    cache.invalidateQueries({ queryKey: ["folder"] });
  };
  const move = useMutation({
    mutationFn: (body: FolderRequest) => request<MoveState>("/api/data-folder/move", { method: "POST", body: JSON.stringify(body) }),
    onSuccess: (state) => {
      cache.setQueryData(["move"], state);
      setMode(null);
    },
  });
  const switchTo = useMutation({
    mutationFn: (body: FolderRequest) => request<AppState>("/api/data-folder", { method: "POST", body: JSON.stringify(body) }),
    onSuccess: () => {
      refresh();
      setMode(null);
    },
  });
  const action = mode === "move" ? move : switchTo;

  // Al terminar la mudanza, la ruta mostrada pasa a ser la nueva.
  const finishedAt = m?.step === "done" ? m.destination : null;
  useEffect(() => {
    if (finishedAt) {
      cache.invalidateQueries({ queryKey: ["state"] });
      cache.invalidateQueries({ queryKey: ["folder"] });
    }
  }, [finishedAt, cache]);

  const destination = m?.destination ?? "";
  return (
    <div className="panel">
      <div className="version-row">
        <div style={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
          <span>{info?.path}</span>
          {info && <span className="small muted">{t("usage", { used: gb(info.used, locale), free: gb(info.freeOnDrive, locale) })}</span>}
        </div>
        {!m?.running && !mode && (
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <button className="ot-btn" type="button" onClick={() => { setMode("move"); setPath(""); action.reset(); }}>{t("moveButton")}</button>
            <button className="ot-btn ot-btn-ghost" type="button" onClick={() => { setMode("use"); setPath(""); action.reset(); }}>{t("useButton")}</button>
          </div>
        )}
      </div>
      <span className="small muted">{t("contents")}</span>

      {mode && (
        <div className="form-block">
          <p className="small" style={{ margin: 0 }}>{mode === "move" ? t("moveHelp") : t("useHelp")}</p>
          <div className="chips">
            {drives?.map((d) => (
              <button key={d.name} className="chip" type="button" onClick={() => setPath(d.folder)}>
                {t("driveChip", { path: d.folder, free: gb(d.free, locale) })}
              </button>
            ))}
          </div>
          <label className="small muted" htmlFor="new-path">{t("folderLabel")}</label>
          <input id="new-path" value={path} onChange={(e) => setPath(e.target.value)} placeholder={t("placeholder")} />
          {action.isError && <p className="error small" style={{ margin: 0 }}>{describe(action.error)}</p>}
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => setMode(null)}>{t("cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="button" disabled={!path || action.isPending} onClick={() => action.mutate({ path })}>
              {mode === "move" ? t("move") : t("useThis")}
            </button>
          </div>
        </div>
      )}

      {m?.running && (
        <div className="form-block" role="status">
          <div className="version-row">
            <span><span className="spinner" aria-hidden="true" style={{ display: "inline-block", marginRight: 8 }} />{t("progress", { step: stepName(m.step), destination })}</span>
            <span className="small muted">{percent(m)} %</span>
          </div>
          <div className="ot-progress"><span style={{ width: `${percent(m)}%` }} /></div>
          <span className="small muted">{t("dontClose")}</span>
        </div>
      )}
      {!m?.running && m?.step === "done" && (
        <div className="result" role="status">
          <span className="ot-badge ot-badge-have">{t("moved")}</span>
          <span className="small muted">{m.warning ? describe(m.warning) : t("nowIn", { destination })}</span>
        </div>
      )}
      {!m?.running && m?.error && (
        <div className="result" role="status">
          <span className="ot-badge ot-badge-danger">{t("error")}</span>
          <span className="small muted">{describe(m.error)}</span>
        </div>
      )}
    </div>
  );
}
