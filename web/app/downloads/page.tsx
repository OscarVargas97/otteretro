"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { librariesQuery, useLibraryHint } from "@/components/Downloads";
import { Empty } from "@/components/Shell";
import { useTasks } from "@/components/Tasks";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { DownloadInfo, PackagePreview } from "@/lib/types";

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });

/** Descargas (§9.2, #202): la cola con progreso, velocidad y pausa, y agregar enlaces eligiendo el disco. */
export default function DownloadsScreen() {
  return <Suspense><Downloads /></Suspense>;
}

function Downloads() {
  const t = useTranslations("downloads");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const list = useQuery({ queryKey: ["downloads"], queryFn: () => request<DownloadInfo[]>("/api/downloads") });
  const live = useTasks();
  const disks = useQuery(librariesQuery).data?.filter((d) => d.type === "disk" && d.connected) ?? [];
  const hint = useLibraryHint();
  const params = useSearchParams();
  const game = params.get("game");
  const gameName = params.get("name");
  const [text, setText] = useState("");
  const [off, setOff] = useState<Set<string>>(new Set());
  const [disk, setDisk] = useState("");
  const refresh = () => {
    cache.invalidateQueries({ queryKey: ["downloads"] });
    cache.invalidateQueries({ queryKey: ["tasks"] });
  };
  // El capturador (#203): solo lo que pegas o arrastras aquí; nunca lee el portapapeles.
  const check = useMutation({
    mutationFn: () => request<PackagePreview[]>("/api/downloads/preview", post({ text })),
    onSuccess: (data) => setOff(new Set(data.filter((p) => p.links.some((l) => l.state === "unavailable" || l.state === "noConnector")).map((p) => p.name))),
  });
  const chosen = (check.data ?? []).filter((p) => !off.has(p.name));
  const add = useMutation({
    mutationFn: async () => {
      for (const p of chosen)
        await request<number>("/api/downloads", post({ name: p.name, diskId: disk || null, links: p.links.map((l) => l.url), gameId: game ? Number(game) : null }));
    },
    onSuccess: () => { setText(""); check.reset(); refresh(); },
    onError: refresh,
  });
  const toggle = (name: string) => setOff((old) => {
    const next = new Set(old);
    if (next.has(name)) next.delete(name); else next.add(name);
    return next;
  });
  const act = useMutation({
    mutationFn: ({ id, action }: { id: number; action: "pause" | "resume" | "cancel" }) => request<void>(`/api/tasks/${id}/${action}`, { method: "POST" }),
    onSettled: refresh,
  });
  const connections = useQuery({ queryKey: ["download-connections"], queryFn: () => request<number>("/api/downloads/connections") });
  const setConnections = useMutation({
    mutationFn: (value: number) => request<void>("/api/downloads/connections", { method: "PUT", body: JSON.stringify({ connections: value }) }),
    onSettled: () => cache.invalidateQueries({ queryKey: ["download-connections"] }),
  });
  const size = (bytes: string | undefined) => bytes ? format.number(Number(bytes) / 1048576, { maximumFractionDigits: 1 }) : "0";

  return (
    <div className="content" style={{ display: "grid", gap: 12 }}>
      <section className="panel" style={{ display: "grid", gap: 8 }}>
        <h2 style={{ margin: 0 }}>{game ? t("addForGame", { name: gameName ?? "" }) : t("add")}</h2>
        <label className="small muted" htmlFor="download-links">{t("links")}</label>
        <textarea id="download-links" rows={4} spellCheck={false} value={text} onChange={(e) => { setText(e.target.value); check.reset(); }} placeholder={t("linksHint")} />
        <div className="hero-actions">
          <button className="ot-btn ot-btn-sm" type="button" disabled={!text.trim() || check.isPending} onClick={() => check.mutate()}>{check.isPending ? t("checking") : t("check")}</button>
          <select className="ot-btn ot-btn-sm" aria-label={t("disk")} value={disk} onChange={(e) => setDisk(e.target.value)}>
            <option value="">{t("defaultDisk")}</option>
            {disks.map((d) => <option key={d.id} value={d.id}>{hint(d)}</option>)}
          </select>
          <label className="small muted" title={t("connectionsHelp")}>
            {t("connections")}{" "}
            <select className="ot-btn ot-btn-sm" value={connections.data ?? 2} onChange={(e) => setConnections.mutate(Number(e.target.value))}>
              {[1, 2, 4, 8].map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
          </label>
        </div>
        {check.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(check.error)}</p>}
        {check.data?.length === 0 && <p className="small muted" style={{ margin: 0 }}>{t("noLinks")}</p>}
        {check.data?.map((p) => (
          <label key={p.name} className="version-row small">
            <span>
              <input type="checkbox" checked={!off.has(p.name)} onChange={() => toggle(p.name)} /> {p.name}
              <span className="muted"> · {t("files", { count: p.links.length })}{Number(p.size) > 0 && ` · ${size(String(p.size))} MB`}</span>
            </span>
            {p.links.filter((l) => l.state !== "ok").map((l) => <span key={l.url} className="ot-badge ot-badge-review" title={l.url}>{t(`state.${l.state as "unavailable"}`)}</span>)}
          </label>
        ))}
        {check.data && check.data.length > 0 && (
          <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" style={{ justifySelf: "start" }} disabled={add.isPending || chosen.length === 0} onClick={() => add.mutate()}>{t("startCount", { count: chosen.length })}</button>
        )}
        {add.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(add.error)}</p>}
      </section>

      {list.isError && <p className="error small" role="alert">{describe(list.error)}</p>}
      {act.isError && <p className="error small" role="alert">{describe(act.error)}</p>}
      {list.data?.length === 0 && <Empty title={t("emptyTitle")} text={t("emptyText")} />}
      {list.data?.map((d) => {
        // El progreso en vivo llega por el panel de tareas (SSE); la lista da el resto.
        const task = live?.find((x) => Number(x.id) === Number(d.id));
        const status = task?.status ?? d.status;
        const progress = Number(task?.progress ?? d.progress);
        const detail = task?.detail ?? d.detail;
        const running = status === "running" || status === "queued";
        const speed = detail?.code === "download.progress" ? detail.params?.speed : undefined;
        return (
          <section key={d.id} className="panel" style={{ display: "grid", gap: 6 }}>
            <div className="version-row">
              <strong>{d.name}</strong>
              <span className="ot-badge">{t(`status.${status as "running"}`)}</span>
            </div>
            <span className="small muted">{t("summary", { disk: d.disk, done: Number(d.done), total: Number(d.links) })}</span>
            {running && <progress max={100} value={progress} aria-label={d.name} style={{ width: "100%" }} />}
            {speed && <span className="small muted">{detail?.params?.file} · {t("speed", { speed: size(speed) })} · {size(detail?.params?.done)} / {size(detail?.params?.total)} MB</span>}
            {status === "error" && detail && <p className="error small" role="alert" style={{ margin: 0 }}>{describe({ ...detail })}</p>}
            <div className="hero-actions">
              {running && <button className="ot-btn ot-btn-sm" type="button" onClick={() => act.mutate({ id: Number(d.id), action: "pause" })}>{t("pause")}</button>}
              {status === "paused" && <button className="ot-btn ot-btn-sm" type="button" onClick={() => act.mutate({ id: Number(d.id), action: "resume" })}>{t("resume")}</button>}
              {(running || status === "paused") && <button className="ot-btn ot-btn-sm" type="button" onClick={() => act.mutate({ id: Number(d.id), action: "cancel" })}>{t("cancel")}</button>}
            </div>
          </section>
        );
      })}
    </div>
  );
}
