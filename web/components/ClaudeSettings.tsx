"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { AutomationSuggestion, ClaudeSettingsRequest, ClaudeStatus, ConsolesPage } from "@/lib/types";

/** Ajustes › Claude (§10): horario, tope de día, «Ejecutar ahora» y el registro de uso (§11.4). */
export function ClaudeSettings() {
  const t = useTranslations("settings.claude");
  const describe = useDescribeError();
  const format = useFormatter();
  const cache = useQueryClient();
  const status = useQuery({ queryKey: ["claude"], queryFn: () => request<ClaudeStatus>("/api/claude"), refetchInterval: (q) => (q.state.data?.task ? 5_000 : 30_000) });
  const [form, setForm] = useState<ClaudeSettingsRequest | null>(null);
  const [platform, setPlatform] = useState("");
  const consoles = useQuery({ queryKey: ["consoles", "settings"], queryFn: () => request<ConsolesPage>("/api/consoles/settings") });
  const iconic = useMutation({
    mutationFn: () => request<number>(`/api/claude/iconic${platform ? `?console=${encodeURIComponent(platform)}` : ""}`, { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  useEffect(() => {
    if (status.data && !form) setForm({ scheduleStart: status.data.scheduleStart, scheduleEnd: status.data.scheduleEnd, windowShare: Number(status.data.windowShare), capped: status.data.capped });
  }, [status.data, form]);
  const done = (data: ClaudeStatus) => cache.setQueryData(["claude"], data);
  const save = useMutation({ mutationFn: (body: ClaudeSettingsRequest) => request<ClaudeStatus>("/api/claude", { method: "PUT", body: JSON.stringify(body) }), onSuccess: done });
  const runNow = useMutation({
    mutationFn: () => request<ClaudeStatus>("/api/claude/run-now", { method: "POST" }),
    onSuccess: (data) => { done(data); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });

  if (!status.data || !form) return status.isError ? <p className="error" role="alert">{describe(status.error)}</p> : null;
  const s = status.data;
  const at = (iso: string) => format.dateTime(new Date(iso), { weekday: "long", hour: "2-digit", minute: "2-digit" });
  const task = s.task;

  return (
    <div className="panel">
      <p className="small" role="status" style={{ margin: 0 }}>
        {s.pausedUntil ? t("paused", { at: at(s.pausedUntil) })
          : s.runNowUntil ? t("runNowUntil", { at: at(s.runNowUntil) })
            : s.inSchedule ? t("inSchedule") : t("outSchedule", { start: s.scheduleStart })}
        {task && <> {task.status === "running" ? t("taskRunning", { id: Number(task.id), progress: Number(task.progress) })
          : task.resumeAt ? t("taskWaiting", { id: Number(task.id), at: at(task.resumeAt) })
            : t("taskQueued", { id: Number(task.id) })}</>}
      </p>
      <form className="chips" style={{ alignItems: "end" }} onSubmit={(e) => { e.preventDefault(); save.mutate(form); }}>
        <label className="small" style={{ display: "grid", gap: 4 }}>{t("from")}<input type="time" value={form.scheduleStart ?? ""} onChange={(e) => setForm({ ...form, scheduleStart: e.target.value })} /></label>
        <label className="small" style={{ display: "grid", gap: 4 }}>{t("to")}<input type="time" value={form.scheduleEnd ?? ""} onChange={(e) => setForm({ ...form, scheduleEnd: e.target.value })} /></label>
        <label className="small" style={{ display: "flex", gap: 6, alignItems: "center" }}><input type="checkbox" checked={form.capped ?? true} onChange={(e) => setForm({ ...form, capped: e.target.checked })} />{t("capped")}</label>
        <label className="small" style={{ display: "grid", gap: 4 }}>{t("share")}<input type="number" min={10} max={100} disabled={form.capped === false} value={Number(form.windowShare ?? 50)} onChange={(e) => setForm({ ...form, windowShare: Number(e.target.value) })} /></label>
        <button className="ot-btn" type="submit" disabled={save.isPending}>{t("save")}</button>
        {save.isSuccess && <span className="small muted">{t("saved")}</span>}
      </form>
      <div className="hero-actions">
        <button className="ot-btn ot-btn-primary" type="button" disabled={runNow.isPending || task?.status === "running"} onClick={() => runNow.mutate()}>{t("runNow")}</button>
      </div>
      <div className="chips" style={{ alignItems: "center" }}>
        <select aria-label={t("iconic")} value={platform} onChange={(e) => setPlatform(e.target.value)}>
          <option value="">{t("iconicAll")}</option>
          {consoles.data?.consoles.filter((c) => Number(c.games) > 0).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <button className="ot-btn" type="button" disabled={iconic.isPending} onClick={() => iconic.mutate()}>{t("iconic")}</button>
        {iconic.isSuccess && <span className="small muted">{t("iconicQueued")}</span>}
        {iconic.isError && <span className="small muted">{t("iconicNone")}</span>}
      </div>
      {(save.error || runNow.error) && <p className="error small" role="alert">{describe(save.error ?? runNow.error)}</p>}
      <AutomationSuggestions />
      <b>{t("usage")}</b>
      {s.usage.length === 0 ? <p className="small muted" style={{ margin: 0 }}>{t("noUsage")}</p> : (
        <div className="table-wrap">
          <table className="t">
            <thead><tr><th>{t("date")}</th><th>{t("prompt")}</th><th>{t("model")}</th><th>{t("duration")}</th><th>{t("result")}</th><th>{t("tokens")}</th></tr></thead>
            <tbody>
              {s.usage.map((u, i) => (
                <tr key={i}>
                  <td className="small">{format.dateTime(new Date(u.createdAt), { dateStyle: "short", timeStyle: "short" })}</td>
                  <td>{u.prompt}</td>
                  <td>{u.model ?? "—"}</td>
                  <td>{Math.round(Number(u.durationMs) / 1000)} s</td>
                  <td>{u.result}</td>
                  <td>{Number(u.inputTokens).toLocaleString()} / {Number(u.outputTokens).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

/** Lo que Claude repite y conviene automatizar (§11.5): «Crear issue» lo deja en GitHub con la etiqueta automatizar. */
function AutomationSuggestions() {
  const t = useTranslations("settings.claude");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const list = useQuery({ queryKey: ["claude", "automation"], queryFn: () => request<AutomationSuggestion[]>("/api/claude/automation") });
  const create = useMutation({
    mutationFn: (key: string) => request<AutomationSuggestion[]>(`/api/claude/automation/${key}/issue`, { method: "POST" }),
    onSuccess: (data) => cache.setQueryData(["claude", "automation"], data),
  });
  return (
    <>
      <b>{t("automation")}</b>
      {list.data?.length === 0 && <p className="small muted" style={{ margin: 0 }}>{t("noAutomation", { count: 10 })}</p>}
      {list.data?.map((a) => (
        <div key={a.key} className="chips" style={{ alignItems: "center" }}>
          <span className="small"><b>{t("automationTimes", { count: Number(a.count) })}</b> · {a.change} · {a.reason}</span>
          {a.issueUrl
            ? <a className="small" href={a.issueUrl} target="_blank" rel="noreferrer">{t("issueCreated")}</a>
            : <button className="ot-btn" type="button" disabled={create.isPending} onClick={() => create.mutate(a.key)}>{t("createIssue")}</button>}
        </div>
      ))}
      {create.error && <p className="error small" role="alert">{describe(create.error)}</p>}
    </>
  );
}
