"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useState } from "react";
import { librariesQuery, useLibraryHint } from "@/components/Downloads";
import { Empty } from "@/components/Shell";
import { useTasks } from "@/components/Tasks";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { DownloadInfo } from "@/lib/types";

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });

/** Descargas (§9.2, #202): la cola con progreso, velocidad y pausa, y agregar enlaces eligiendo el disco. */
export default function Downloads() {
  const t = useTranslations("downloads");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const list = useQuery({ queryKey: ["downloads"], queryFn: () => request<DownloadInfo[]>("/api/downloads") });
  const live = useTasks();
  const disks = useQuery(librariesQuery).data?.filter((d) => d.type === "disk" && d.connected) ?? [];
  const hint = useLibraryHint();
  const [name, setName] = useState("");
  const [links, setLinks] = useState("");
  const [disk, setDisk] = useState("");
  const refresh = () => {
    cache.invalidateQueries({ queryKey: ["downloads"] });
    cache.invalidateQueries({ queryKey: ["tasks"] });
  };
  const add = useMutation({
    mutationFn: () => request<number>("/api/downloads", post({ name, diskId: disk || null, links: links.split("\n").map((l) => l.trim()).filter(Boolean) })),
    onSuccess: () => { setName(""); setLinks(""); refresh(); },
  });
  const act = useMutation({
    mutationFn: ({ id, action }: { id: number; action: "pause" | "resume" | "cancel" }) => request<void>(`/api/tasks/${id}/${action}`, { method: "POST" }),
    onSettled: refresh,
  });
  const size = (bytes: string | undefined) => bytes ? format.number(Number(bytes) / 1048576, { maximumFractionDigits: 1 }) : "0";

  return (
    <div className="content" style={{ display: "grid", gap: 12 }}>
      <form className="panel" style={{ display: "grid", gap: 8 }} onSubmit={(e) => { e.preventDefault(); add.mutate(); }}>
        <h2 style={{ margin: 0 }}>{t("add")}</h2>
        <label className="small muted" htmlFor="download-name">{t("name")}</label>
        <input id="download-name" value={name} onChange={(e) => setName(e.target.value)} />
        <label className="small muted" htmlFor="download-links">{t("links")}</label>
        <textarea id="download-links" rows={4} spellCheck={false} value={links} onChange={(e) => setLinks(e.target.value)} placeholder={t("linksHint")} />
        <div className="hero-actions">
          <select className="ot-btn ot-btn-sm" aria-label={t("disk")} value={disk} onChange={(e) => setDisk(e.target.value)}>
            <option value="">{t("defaultDisk")}</option>
            {disks.map((d) => <option key={d.id} value={d.id}>{hint(d)}</option>)}
          </select>
          <button className="ot-btn ot-btn-primary ot-btn-sm" type="submit" disabled={!name.trim() || !links.trim() || add.isPending}>{t("start")}</button>
        </div>
        {add.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(add.error)}</p>}
      </form>

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
