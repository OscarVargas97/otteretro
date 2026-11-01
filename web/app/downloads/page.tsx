"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { librariesQuery, useLibraryHint } from "@/components/Downloads";
import { Empty } from "@/components/Shell";
import { Tabs } from "@/components/Tabs";
import { useTasks } from "@/components/Tasks";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { DownloadInfo, LinkPackage } from "@/lib/types";

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });
const linksQuery = { queryKey: ["download-links"], queryFn: () => request<LinkPackage[]>("/api/downloads/links") };

/** Descargas (§9.2, #202, #210): dos pestañas, la cola y el capturador de enlaces, como en JDownloader. */
export default function DownloadsScreen() {
  return <Suspense><Downloads /></Suspense>;
}

function Downloads() {
  const t = useTranslations("downloads");
  const router = useRouter();
  const params = useSearchParams();
  const game = params.get("game");
  const asked = params.get("tab");
  const tab = asked === "queue" || asked === "links" ? asked : game ? "links" : "queue";
  // Lo que copias entra solo: la pestaña lo cuenta aunque estés mirando la cola.
  const links = useQuery({ ...linksQuery, refetchInterval: 3000 });
  const count = links.data?.reduce((n, p) => n + p.links.length, 0) ?? 0;
  return (
    <div className="content" style={{ display: "grid", gap: 12 }}>
      <Tabs tabs={[{ id: "queue", label: t("tabQueue") }, { id: "links", label: count ? t("tabLinksCount", { count }) : t("tabLinks") }]} current={tab} label={t("sections")} panel="downloads-panel"
        onSelect={(id) => router.push(`/downloads/?tab=${id}${game ? `&${new URLSearchParams({ game, name: params.get("name") ?? "" })}` : ""}`)} />
      <div id="downloads-panel" role="tabpanel" aria-labelledby={`tab-${tab}`} style={{ display: "grid", gap: 12 }}>
        {tab === "links" ? <Capture packages={links.data ?? []} /> : <Queue />}
      </div>
    </div>
  );
}

const PROBLEMS = ["unavailable", "noConnector", "blocked"];

/** Capturador (#203, #210): lo que pegas o copias queda guardado hasta que lo mandas a la cola o lo quitas. Comprobar consulta los sitios, solo cuando lo pides. */
function Capture({ packages }: { packages: LinkPackage[] }) {
  const t = useTranslations("downloads");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const router = useRouter();
  const disks = useQuery(librariesQuery).data?.filter((d) => d.type === "disk" && d.connected) ?? [];
  const hint = useLibraryHint();
  const params = useSearchParams();
  const game = params.get("game");
  const gameName = params.get("name");
  const [text, setText] = useState("");
  const [picked, setPicked] = useState<Record<string, boolean>>({});
  const [disk, setDisk] = useState("");
  const [none, setNone] = useState(false);
  const reload = () => cache.invalidateQueries({ queryKey: linksQuery.queryKey });
  const add = useMutation({
    mutationFn: (value: string) => request<number>("/api/downloads/links", post({ text: value, gameId: game ? Number(game) : null })),
    onSuccess: (found) => { setText(""); setNone(found === 0); reload(); },
  });
  const check = useMutation({
    mutationFn: () => request<LinkPackage[]>("/api/downloads/links/check", { method: "POST" }),
    onSuccess: (data) => cache.setQueryData(linksQuery.queryKey, data),
  });
  // Un paquete con problemas viene sin marcar; lo que marques tú manda.
  const isOn = (p: LinkPackage) => picked[p.name] ?? !p.links.some((l) => PROBLEMS.includes(l.state));
  const chosen = packages.filter(isOn);
  const queue = useMutation({
    mutationFn: () => request<void>("/api/downloads/links/queue", post({ packages: chosen.map((p) => p.name), diskId: disk || null })),
    onSuccess: () => {
      reload();
      cache.invalidateQueries({ queryKey: ["downloads"] });
      cache.invalidateQueries({ queryKey: ["tasks"] });
      router.push("/downloads/?tab=queue");
    },
  });
  const remove = useMutation({ mutationFn: (id: number) => request<void>(`/api/downloads/links/${id}`, { method: "DELETE" }), onSettled: reload });
  // Un captcha o un sitio sin conector los resuelves tú en el navegador (#228); lo que bajes, déjalo en Entrada.
  const open = useMutation({ mutationFn: (url: string) => request<void>("/api/downloads/open", post({ url })) });
  const clipboard = useQuery({ queryKey: ["download-clipboard"], queryFn: () => request<boolean>("/api/downloads/clipboard") });
  const setClipboard = useMutation({
    mutationFn: (enabled: boolean) => request<void>("/api/downloads/clipboard", { method: "PUT", body: JSON.stringify({ enabled }) }),
    onSettled: () => cache.invalidateQueries({ queryKey: ["download-clipboard"] }),
  });
  const connections = useQuery({ queryKey: ["download-connections"], queryFn: () => request<number>("/api/downloads/connections") });
  const setConnections = useMutation({
    mutationFn: (value: number) => request<void>("/api/downloads/connections", { method: "PUT", body: JSON.stringify({ connections: value }) }),
    onSettled: () => cache.invalidateQueries({ queryKey: ["download-connections"] }),
  });
  const failure = add.error ?? check.error ?? queue.error ?? open.error ?? remove.error;

  return (
    <section className="panel" style={{ display: "grid", gap: 8 }}>
      <h2 style={{ margin: 0 }}>{game ? t("addForGame", { name: gameName ?? "" }) : t("add")}</h2>
      <label className="small muted" htmlFor="download-links">{t("links")}</label>
      {/* Lo pegado o arrastrado se guarda en el momento: cambiar de ventana no lo pierde. */}
      <textarea id="download-links" rows={3} spellCheck={false} value={text} placeholder={t("linksHint")}
        onChange={(e) => { setText(e.target.value); setNone(false); }}
        onPaste={(e) => { const pasted = e.clipboardData.getData("text"); if (pasted.trim()) { e.preventDefault(); add.mutate(pasted); } }}
        onDrop={(e) => { const dropped = e.dataTransfer.getData("text"); if (dropped.trim()) { e.preventDefault(); add.mutate(dropped); } }} />
      <div className="hero-actions" style={{ alignItems: "center" }}>
        <button className="ot-btn ot-btn-sm" type="button" disabled={!text.trim() || add.isPending} onClick={() => add.mutate(text)}>{t("addLinks")}</button>
        <label className="small muted"><input type="checkbox" checked={clipboard.data ?? true} onChange={(e) => setClipboard.mutate(e.target.checked)} /> {t("clipboard")}</label>
      </div>
      {none && <p className="small muted" style={{ margin: 0 }}>{t("noLinks")}</p>}
      {failure && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(failure)}</p>}
      {packages.length === 0 && <p className="small muted" style={{ margin: 0 }}>{t("captureEmpty")}</p>}
      {packages.map((p) => (
        <div key={p.name} className="download-package small">
          <label>
            <input type="checkbox" checked={isOn(p)} onChange={() => setPicked((old) => ({ ...old, [p.name]: !isOn(p) }))} /> {p.name}
            <span className="muted"> · {t("files", { count: p.links.length })}{Number(p.size) > 0 && ` · ${format.number(Number(p.size) / 1048576, { maximumFractionDigits: 1 })} MB`}</span>
          </label>
          {p.links.map((l) => (
            <div key={l.id} className="download-problem">
              {p.links.length > 1 && <span title={l.url}>{l.name}</span>}
              {l.state !== "ok" && <span className={l.state === "unchecked" ? "ot-badge" : "ot-badge ot-badge-review"} title={l.url}>{l.detail ? describe({ ...l.detail }) : t(`state.${l.state as "unavailable"}`)}</span>}
              {(l.state === "blocked" || l.state === "noConnector") && <button className="ot-btn ot-btn-sm" type="button" onClick={() => open.mutate(l.url)}>{t("openBrowser")}</button>}
              <button className="ot-btn ot-btn-sm" type="button" aria-label={t("removeLink", { name: l.name })} onClick={() => remove.mutate(Number(l.id))}>{t("remove")}</button>
            </div>
          ))}
        </div>
      ))}
      {packages.length > 0 && (
        <div className="hero-actions" style={{ alignItems: "center" }}>
          <button className="ot-btn ot-btn-sm" type="button" disabled={check.isPending} title={t("checkHelp")} onClick={() => check.mutate()}>{check.isPending ? t("checking") : t("check")}</button>
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
          <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={queue.isPending || chosen.length === 0} onClick={() => queue.mutate()}>{t("startCount", { count: chosen.length })}</button>
        </div>
      )}
    </section>
  );
}

/** La cola (§9.2, #202): progreso, velocidad y pausa. */
function Queue() {
  const t = useTranslations("downloads");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const list = useQuery({ queryKey: ["downloads"], queryFn: () => request<DownloadInfo[]>("/api/downloads") });
  const live = useTasks();
  const refresh = () => {
    cache.invalidateQueries({ queryKey: ["downloads"] });
    cache.invalidateQueries({ queryKey: ["tasks"] });
  };
  const act = useMutation({
    mutationFn: ({ id, action }: { id: number; action: "pause" | "resume" | "cancel" }) => request<void>(`/api/tasks/${id}/${action}`, { method: "POST" }),
    onSettled: refresh,
  });
  const size = (bytes: string | undefined) => bytes ? format.number(Number(bytes) / 1048576, { maximumFractionDigits: 1 }) : "0";

  return (
    <>
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
    </>
  );
}
