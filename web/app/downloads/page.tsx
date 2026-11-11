"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { librariesQuery, useLibraryHint } from "@/components/Downloads";
import { Empty } from "@/components/Shell";
import { Tabs } from "@/components/Tabs";
import { useTasks } from "@/components/Tasks";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { DownloadInfo, LinkPackage, SiteLimit } from "@/lib/types";

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });
const linksQuery = { queryKey: ["download-links"], queryFn: () => request<LinkPackage[]>("/api/downloads/links") };
const FINISHED = ["done", "canceled"];

/** Descargas (§9.2, #202, #210, #417): «Añadir» arriba, la cola en En curso y Terminadas, y los ajustes en «Avanzado». */
export default function DownloadsScreen() {
  return <Suspense><Downloads /></Suspense>;
}

function Downloads() {
  const t = useTranslations("downloads");
  const router = useRouter();
  const params = useSearchParams();
  const tab = params.get("tab") === "done" ? "done" : "active";
  // Lo que copias entra solo: la caja lo muestra aunque no la estés mirando.
  const links = useQuery({ ...linksQuery, refetchInterval: 3000 });
  // El avance de cada enlace sale de esta lista: se refresca mientras algo está bajando.
  const list = useQuery({
    queryKey: ["downloads"],
    queryFn: () => request<DownloadInfo[]>("/api/downloads"),
    refetchInterval: (q) => q.state.data?.some((d) => d.status === "running" || d.status === "queued") ? 3000 : false,
  });
  const live = useTasks();
  // El estado en vivo llega por el panel de tareas (SSE); la lista da el resto.
  const rows = (list.data ?? []).map((d) => ({ d, task: live?.find((x) => Number(x.id) === Number(d.id)) }))
    .map((r) => ({ ...r, status: r.task?.status ?? r.d.status }));
  const active = rows.filter((r) => !FINISHED.includes(r.status));
  const shown = tab === "done" ? rows.filter((r) => FINISHED.includes(r.status)) : active;
  const describe = useDescribeError();
  const keep = new URLSearchParams(params);

  return (
    <div className="content" style={{ display: "grid", gap: 12 }}>
      <Capture packages={links.data ?? []} />
      <Tabs tabs={[{ id: "active", label: t("tabActive"), count: active.length, countLabel: t("activeCount", { count: active.length }) }, { id: "done", label: t("tabDone") }]}
        current={tab} label={t("sections")} panel="downloads-panel"
        onSelect={(id) => { keep.set("tab", id); router.push(`/downloads/?${keep}`); }} />
      <div id="downloads-panel" role="tabpanel" aria-labelledby={`tab-${tab}`} style={{ display: "grid", gap: 12 }}>
        {list.isError && <p className="error small" role="alert">{describe(list.error)}</p>}
        {list.data && shown.length === 0 && (tab === "done" ? <p className="small muted" style={{ margin: 0 }}>{t("doneEmpty")}</p> : <Empty title={t("emptyTitle")} text={t("emptyText")} />)}
        {shown.map((r) => <Row key={r.d.id} d={r.d} status={r.status} detail={r.task?.detail ?? r.d.detail} progress={Number(r.task?.progress ?? r.d.progress)} />)}
      </div>
      <Advanced />
    </div>
  );
}

const PROBLEMS = ["unavailable", "noConnector", "blocked"];

/** Añadir (#203, #210, #417): lo que pegas o copias queda guardado hasta que lo mandas a la cola o lo quitas, con el disco de destino en la misma línea. Comprobar consulta los sitios, solo cuando lo pides. */
function Capture({ packages }: { packages: LinkPackage[] }) {
  const t = useTranslations("downloads");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
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
    },
  });
  const remove = useMutation({ mutationFn: (id: number) => request<void>(`/api/downloads/links/${id}`, { method: "DELETE" }), onSettled: reload });
  // Un captcha o un sitio sin conector los resuelves tú en el navegador (#228); lo que bajes, déjalo en Entrada.
  const open = useMutation({ mutationFn: (url: string) => request<void>("/api/downloads/open", post({ url })) });
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
      <div className="hero-actions" style={{ alignItems: "center" }}>
        <button className="ot-btn ot-btn-sm" type="button" disabled={!text.trim() || add.isPending} onClick={() => add.mutate(text)}>{t("addLinks")}</button>
        {packages.length > 0 && <button className="ot-btn ot-btn-sm" type="button" disabled={check.isPending} title={t("checkHelp")} onClick={() => check.mutate()}>{check.isPending ? t("checking") : t("check")}</button>}
        <label className="small muted">
          {t("goesTo")}{" "}
          <select className="ot-btn ot-btn-sm" aria-label={t("disk")} value={disk} onChange={(e) => setDisk(e.target.value)}>
            <option value="">{t("defaultDisk")}</option>
            {disks.map((d) => <option key={d.id} value={d.id}>{hint(d)}</option>)}
          </select>
        </label>
        <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={queue.isPending || chosen.length === 0} onClick={() => queue.mutate()}>{t("startCount", { count: chosen.length })}</button>
      </div>
    </section>
  );
}

/** Avanzado (#417): lo que se toca poco, plegado. Conexiones por archivo, descargas a la vez por sitio (#202, decisión 248), el portapapeles (#210) y las cuentas. */
function Advanced() {
  const t = useTranslations("downloads");
  const describe = useDescribeError();
  const cache = useQueryClient();
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
  const sites = useQuery({ queryKey: ["download-sites"], queryFn: () => request<SiteLimit[]>("/api/downloads/sites") });
  const save = useMutation({
    mutationFn: (body: { site: string; value: number }) => request<SiteLimit[]>("/api/downloads/sites", { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: (data) => cache.setQueryData(["download-sites"], data),
  });
  const failure = setClipboard.error ?? setConnections.error ?? save.error;
  return (
    <details className="panel small download-sites">
      <summary>{t("advanced")}</summary>
      <label><input type="checkbox" checked={clipboard.data ?? true} onChange={(e) => setClipboard.mutate(e.target.checked)} /> {t("clipboard")}</label>
      <div className="download-sites-list">
        <label>
          <span>{t("connections")}</span>
          <select className="ot-btn ot-btn-sm" value={connections.data ?? 2} onChange={(e) => setConnections.mutate(Number(e.target.value))}>
            {[1, 2, 4, 8].map((n) => <option key={n} value={n}>{n}</option>)}
          </select>
          <span className="muted">{t("connectionsHelp")}</span>
        </label>
      </div>
      <strong>{t("parallelTitle")}</strong>
      <p className="muted" style={{ margin: 0 }}>{t("parallelHelp")}</p>
      <div className="download-sites-list">
        {sites.data?.map((s) => (
          <label key={s.site}>
            <span>{t(`site.${s.site as "direct"}`)}</span>
            <select className="ot-btn ot-btn-sm" value={Number(s.value)} disabled={save.isPending} onChange={(e) => save.mutate({ site: s.site, value: Number(e.target.value) })}>
              {Array.from({ length: Number(s.max) }, (_, i) => i + 1).map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
            <span className="muted">{s.site === "1fichier" && Number(s.max) === 1 ? t("parallelFree") : t("parallelMax", { max: Number(s.max) })}</span>
          </label>
        ))}
      </div>
      <div className="hero-actions" style={{ alignItems: "center" }}>
        <span>{t("accounts")}</span>
        <Link className="ot-btn ot-btn-sm" href="/settings/?s=credentials">{t("openAccounts")}</Link>
      </div>
      {failure && <p className="error" role="alert" style={{ margin: 0 }}>{describe(failure)}</p>}
    </details>
  );
}

/** Una descarga de la cola (§9.2, #202): progreso, velocidad, sus enlaces y lo que se puede hacer según cómo va. */
function Row({ d, status, detail, progress }: { d: DownloadInfo; status: string; detail: DownloadInfo["detail"]; progress: number }) {
  const t = useTranslations("downloads");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const act = useMutation({
    mutationFn: (action: "pause" | "resume" | "cancel") => request<void>(`/api/tasks/${d.id}/${action}`, { method: "POST" }),
    onSettled: () => {
      cache.invalidateQueries({ queryKey: ["downloads"] });
      cache.invalidateQueries({ queryKey: ["tasks"] });
    },
  });
  // Lo que llegó dañado vuelve a «Añadir», para bajarlo otra vez (#202); lo dañado no se borra.
  const again = useMutation({
    mutationFn: () => request<number>("/api/downloads/links", post({ text: d.items.map((i) => i.url).join("\n") })),
    onSuccess: () => { cache.invalidateQueries({ queryKey: linksQuery.queryKey }); window.scrollTo({ top: 0 }); },
  });
  const size = (bytes: string | undefined) => bytes ? format.number(Number(bytes) / 1048576, { maximumFractionDigits: 1 }) : "0";
  const running = status === "running" || status === "queued";
  const speed = status === "running" && detail?.code === "download.progress" ? detail.params?.speed : undefined;
  const failure = act.error ?? again.error;
  return (
    <section className="panel" style={{ display: "grid", gap: 6 }}>
      <div className="version-row">
        <strong>{d.name}</strong>
        <span className={status === "done" ? "ot-badge ot-badge-have" : status === "error" ? "ot-badge ot-badge-danger" : "ot-badge"}>{t(`status.${status as "running"}`)}</span>
      </div>
      <span className="small muted">{t("summary", { disk: d.disk, done: Number(d.done), total: Number(d.links) })}</span>
      {(running || status === "paused") && <progress max={100} value={progress} aria-label={d.name} style={{ width: "100%" }} />}
      {speed && <span className="small muted">{detail?.params?.file} · {t("speed", { speed: size(speed) })} · {size(detail?.params?.done)} / {size(detail?.params?.total)} MB</span>}
      {status === "error" && detail && <p className="error small" role="alert" style={{ margin: 0 }}>{describe({ ...detail })}</p>}
      {failure && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(failure)}</p>}
      {/* Cada enlace y cómo va (#202). `details` recuerda solo si está abierto mientras la lista se refresca. */}
      <details className="download-items small">
        <summary>{t("itemsSummary", { count: Number(d.links) })}</summary>
        {d.items.map((item, i) => {
          const index = Number(d.done);
          const state = i < index ? "done" : i === index ? status : "pending";
          const bytes = state === "running" && speed ? { done: Number(detail?.params?.done), total: Number(detail?.params?.total) } : null;
          return (
            <div key={item.url} className="download-item">
              <span title={item.url}>{item.name}</span>
              <span className="hero-actions" style={{ alignItems: "center" }}>
                {bytes ? <span className="muted tabular">{size(String(bytes.done))} / {size(String(bytes.total))} MB</span>
                  : item.size != null && <span className="muted tabular">{size(String(item.size))} MB</span>}
                {state === "done" && <span className="muted">{t(`verified.${(item.check ?? "none") as "none"}`)}</span>}
                <span className={state === "done" ? "ot-badge ot-badge-have" : state === "error" ? "ot-badge ot-badge-danger" : "ot-badge"}>{state === "pending" ? t("itemPending") : t(`status.${state as "running"}`)}</span>
              </span>
              {bytes && <progress max={bytes.total} value={bytes.done} aria-label={item.name} />}
            </div>
          );
        })}
      </details>
      <div className="hero-actions">
        {running && <button className="ot-btn ot-btn-sm" type="button" onClick={() => act.mutate("pause")}>{t("pause")}</button>}
        {status === "paused" && <button className="ot-btn ot-btn-sm" type="button" onClick={() => act.mutate("resume")}>{t("resume")}</button>}
        {(running || status === "paused") && <button className="ot-btn ot-btn-sm" type="button" onClick={() => act.mutate("cancel")}>{t("cancel")}</button>}
        {status === "error" && <button className="ot-btn ot-btn-sm" type="button" disabled={again.isPending} onClick={() => again.mutate()}>{t("downloadAgain")}</button>}
      </div>
    </section>
  );
}
