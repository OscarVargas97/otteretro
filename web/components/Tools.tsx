"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useFormatter } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { useRef, useState } from "react";
import { useConsoles } from "@/components/Catalog";
import { librariesQuery, toolsQuery } from "@/components/Libraries";
import { LoadProfile } from "@/components/ProfileShare";
import { Empty } from "@/components/Shell";
import { Tabs } from "@/components/Tabs";
import { ToolUpdates } from "@/components/ToolUpdates";
import { toolUpdatesQuery } from "@/lib/queries";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { EmulatorInstall, RecipeRelease, RecipeSummary, SystemConsole, ToolInfo } from "@/lib/types";

const KINDS = ["emulator", "port", "complement", "tool", "system"] as const;
type Kind = (typeof KINDS)[number];
/** Lo que se instala con su receta, una URL o un zip (#296): emuladores, ports y herramientas como Steam ROM Manager (decisión 311). */
type Installable = "emulator" | "port" | "tool";

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });
const recipesQuery = { queryKey: ["recipes"], queryFn: () => request<RecipeSummary[]>("/api/recipes"), staleTime: Infinity };
const emulatorsQuery = { queryKey: ["emulators"], queryFn: () => request<EmulatorInstall[]>("/api/emulators") };

/** Herramientas (§5.1, #193, #194, #200, #296, #304): el inventario por tipo, en pestañas y una sola columna centrada, con sus versiones nuevas, instalar y la librería. */
export function ToolsPage() {
  const t = useTranslations("tools");
  const router = useRouter();
  const asked = useSearchParams().get("k");
  const kind: Kind = KINDS.find((k) => k === asked) ?? "emulator";
  // Cada pestaña cuenta sus avisos de versiones nuevas (decisión 320).
  const updates = useQuery(toolUpdatesQuery).data ?? [];
  const count = (k: Kind) => updates.filter((u) => u.kind === k).length;
  return (
    <div className="centered">
      <Tabs tabs={KINDS.map((k) => ({ id: k, label: t(`kind.${k}`), count: count(k), countLabel: t("updates.count", { count: count(k) }) }))} current={kind} label={t("sections")} panel="tools-panel"
        onSelect={(k) => router.push(`/tools/?k=${k}`)} />
      <section className="sec" id="tools-panel" role="tabpanel" aria-labelledby={`tab-${kind}`}>
        {kind === "system" ? <SystemPanel /> : (
          <div style={{ display: "grid", gap: 12 }}>
            {kind !== "complement" && <ToolUpdates kind={kind} />}
            {kind !== "complement" && <InstallPanel key={kind} kind={kind} />}
            {kind === "emulator" && <LoadProfile />}
            <ToolList kind={kind} />
            {kind !== "complement" && <Library kind={kind} />}
          </div>
        )}
      </section>
    </div>
  );
}

/** BIOS, keys y firmware (§8.5, #199): lo que tienes por consola, con versiones, y subir otro. Otra versión nunca reemplaza la anterior. */
function SystemPanel() {
  const t = useTranslations("tools");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const consoles = useConsoles().data ?? [];
  const list = useQuery({ queryKey: ["system"], queryFn: () => request<SystemConsole[]>("/api/system") });
  const [console_, setConsole] = useState("");
  const input = useRef<HTMLInputElement>(null);
  const upload = useMutation({
    mutationFn: (file: File) => request<void>(`/api/system/${encodeURIComponent(console_)}?${new URLSearchParams({ name: file.name })}`,
      { method: "POST", body: file, headers: { "Content-Type": "application/octet-stream" } }),
    onSettled: () => {
      if (input.current) input.current.value = "";
      cache.invalidateQueries({ queryKey: ["system"] });
    },
  });
  return (
    <div style={{ display: "grid", gap: 12 }}>
      <section className="panel" style={{ display: "grid", gap: 8 }}>
        <p className="small" style={{ margin: 0 }}>{t("systemHelp")}</p>
        <div className="hero-actions">
          <select className="ot-btn ot-btn-sm" aria-label={t("systemConsole")} value={console_} onChange={(e) => setConsole(e.target.value)}>
            <option value="">{t("systemConsole")}</option>
            {consoles.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <input ref={input} type="file" aria-label={t("systemUpload")} disabled={!console_ || upload.isPending}
            onChange={(e) => { const file = e.target.files?.[0]; if (file) upload.mutate(file); }} />
        </div>
        {upload.isPending && <p className="small muted" role="status" style={{ margin: 0 }}>{t("systemUploading")}</p>}
        {upload.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(upload.error)}</p>}
      </section>
      {list.isError && <p className="error small" role="alert">{describe(list.error)}</p>}
      {list.data?.length === 0 && <Empty title={t("systemEmptyTitle")} text={t("systemEmptyText")} />}
      {list.data?.map((c) => (
        <section className="panel" key={c.id} style={{ display: "grid", gap: 6 }}>
          <h3 style={{ margin: 0 }}>{c.name}</h3>
          {c.missing.map((m) => <span key={m} className="ot-badge ot-badge-review" style={{ justifySelf: "start" }}>{t("systemMissing", { name: m })}</span>)}
          {c.files.map((f) => (
            <div key={f.path} className="version-row small">
              <span className="folder-path">{f.name}{f.version && ` · ${f.version}`}</span>
              <span className="muted">{f.disk} · {format.dateTime(new Date(f.modified), { dateStyle: "medium" })}</span>
            </div>
          ))}
        </section>
      ))}
    </div>
  );
}

function ToolList({ kind }: { kind: Exclude<Kind, "system"> }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const tools = useQuery(toolsQuery);
  const updated = new Set((useQuery(toolUpdatesQuery).data ?? []).map((u) => u.recipeId));
  const [open, setOpen] = useState<string | null>(null);
  if (tools.isError) return <p className="error small" role="alert">{describe(tools.error)}</p>;
  if (!tools.data) return <div className="panel skeleton" style={{ height: 160 }} aria-busy="true" />;
  if (tools.data.length === 0) return <Empty title={t("emptyTitle")} text={t("emptyText")} />;
  const rows = tools.data.filter((x) => x.kind === kind);
  if (rows.length === 0) return <p className="missing small">{t("none")}</p>;
  return (
    <section className="panel">
      <ul className="detail-list">
        {rows.map((x) => (
          <li key={x.path}>
            <div className="version-row" style={{ flex: 1 }}>
              <button type="button" className="link-button grow" aria-expanded={open === x.path} onClick={() => setOpen(open === x.path ? null : x.path)}>
                <b>{x.name}</b>
                <span className="small muted">{x.game ? `${x.folder} · ${x.game}` : x.folder}</span>
              </button>
              <span style={{ display: "flex", gap: 6 }}>
                <span className="ot-badge">{x.version}</span>
                {x.recipeId && updated.has(x.recipeId) && <span className="ot-badge ot-badge-info">{t("updates.badge")}</span>}
                {!x.recipeId && <span className="ot-badge ot-badge-info" title={t("noRecipeHelp")}>{t("noRecipe")}</span>}
              </span>
            </div>
            {open === x.path && <ToolDetail tool={x} />}
          </li>
        ))}
      </ul>
    </section>
  );
}

/** El detalle de lo instalado: consola, juego que cubre, en qué disco está, y moverlo a otro (#198, #205). */
function ToolDetail({ tool }: { tool: ToolInfo }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const locations = (useQuery(librariesQuery).data ?? []).filter((l) => (l.type === "disk" || l.type === tool.kind) && l.connected);
  const where = locations.find((l) => l.id === tool.locationId)?.name;
  const targets = locations.filter((l) => l.type === "disk" && l.id !== tool.locationId);
  const [moving, setMoving] = useState(false);
  const [to, setTo] = useState("");
  const move = useMutation({
    mutationFn: () => request<number>("/api/tools/move", { method: "POST", body: JSON.stringify({ path: tool.path, toLocation: to || null }) }),
    onSuccess: () => { setMoving(false); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });
  return (
    <div className="tool-detail">
      <dl className="facts small">
        {tool.kind !== "tool" && <><dt>{t("console")}</dt><dd>{tool.folder}</dd></>}
        {tool.game && <><dt>{t("covers")}</dt><dd>{tool.game}</dd></>}
        <dt>{t("version")}</dt><dd>{tool.version}</dd>
        <dt>{t("location")}</dt><dd>{where ?? t("dataFolder")}</dd>
        <dt>{t("path")}</dt><dd className="folder-path">{tool.path}</dd>
      </dl>
      {move.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {!moving && targets.length > 0 && (
        <button className="ot-btn ot-btn-sm" type="button" onClick={() => { setMoving(true); setTo(targets[0].id); move.reset(); }}>{t("move")}</button>
      )}
      {moving && (
        <span className="sentence">
          <label className="small muted" htmlFor={`move-${tool.path}`}>{t("to")}</label>
          <select id={`move-${tool.path}`} value={to} onChange={(e) => setTo(e.target.value)}>
            {targets.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </select>
          <button className="ot-btn ot-btn-sm" type="button" onClick={() => setMoving(false)}>{t("cancel")}</button>
          <button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={move.isPending} onClick={() => move.mutate()}>{t("confirm")}</button>
        </span>
      )}
      {move.isError && <p className="error small" style={{ margin: 0 }}>{describe(move.error)}</p>}
    </div>
  );
}

/** Un id propio para lo que no tiene receta, desde su nombre: «Mi Emulador» → mi-emulador. */
const slug = (name: string) => name.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const OTHER = ":other"; // «:» no va en un id de receta: no choca con ninguna

/**
 * Instalar (#268, #296): desde la receta eligiendo una versión publicada, desde la URL de una release o desde una carpeta o un
 * zip de este equipo (o adoptando uno instalado a mano). Cada versión va al lado de las otras: nunca se reemplaza una.
 */
function InstallPanel({ kind }: { kind: Installable }) {
  const t = useTranslations("tools.install");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const recipes = (useQuery(recipesQuery).data ?? []).filter((r) => r.kind === kind);
  const [open, setOpen] = useState(false);
  const [from, setFrom] = useState<"recipe" | "url" | "path">("recipe");
  const [recipe, setRecipe] = useState("");
  const [name, setName] = useState("");
  const [release, setRelease] = useState("");
  const [version, setVersion] = useState("");
  const [url, setUrl] = useState("");
  const [path, setPath] = useState("");
  const [adopt, setAdopt] = useState(false);
  const chosen = recipes.find((r) => r.id === recipe);
  const releases = useQuery({
    queryKey: ["recipes", recipe, "releases"],
    queryFn: () => request<RecipeRelease[]>(`/api/recipes/${encodeURIComponent(recipe)}/releases`),
    enabled: open && from === "recipe" && !!chosen?.releases,
    staleTime: 5 * 60_000,
  });
  const key = (r: RecipeRelease) => `${r.channel}|${r.version}`;
  const picked = releases.data?.find((r) => key(r) === release) ?? releases.data?.[0];
  const install = useMutation({
    mutationFn: (job: object) => request<number>("/api/emulators/install", post(job)),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  // Sin receta: el id sale del nombre y la carpeta se llama como él.
  const who = recipe === OTHER ? { emulator: slug(name), name: name.trim(), kind } : { emulator: recipe };
  const ready = (recipe === OTHER ? slug(name) !== "" : !!chosen)
    && (from === "recipe" ? !!picked : from === "url" ? url.trim().startsWith("https://") && version.trim() !== "" : path.trim() !== "");
  const submit = () => install.mutate(
    from === "recipe" ? { emulator: recipe, version: picked!.version, url: picked!.download }
    : from === "url" ? { ...who, version: version.trim(), url: url.trim() }
    : { ...who, version: version.trim() || null, path: path.trim(), adopt });
  const pick = (id: string) => {
    setRecipe(id);
    setRelease("");
    if (id === OTHER && from === "recipe") setFrom("url");
  };

  if (!open) return <div><button className="ot-btn ot-btn-primary ot-btn-sm" type="button" onClick={() => setOpen(true)}>{t(`open.${kind}`)}</button></div>;
  return (
    <section className="panel" style={{ display: "grid", gap: 8 }} aria-labelledby="install-title">
      <h3 id="install-title" style={{ margin: 0 }}>{t(`open.${kind}`)}</h3>
      <div className="sentence">
        <label className="small muted" htmlFor="install-recipe">{t("what")}</label>
        <select id="install-recipe" value={recipe} onChange={(e) => pick(e.target.value)}>
          <option value="">{t("pick")}</option>
          {recipes.map((r) => <option key={r.id} value={r.id}>{r.experimental ? t("experimental", { name: r.name }) : r.name}</option>)}
          {kind !== "tool" && <option value={OTHER}>{t("other")}</option>}
        </select>
        {recipe === OTHER && <input aria-label={t("name")} placeholder={t("name")} value={name} onChange={(e) => setName(e.target.value)} />}
      </div>
      <div className="sentence" role="radiogroup" aria-label={t("from")}>
        {(["recipe", "url", "path"] as const).filter((x) => x !== "recipe" || recipe !== OTHER).map((x) => (
          <label key={x} className="small"><input type="radio" name="install-from" checked={from === x} onChange={() => setFrom(x)} /> {t(`fromKind.${x}`)}</label>
        ))}
      </div>
      {from === "recipe" && chosen && (
        !chosen.releases ? <p className="small muted" style={{ margin: 0 }}>{t("noReleases")}</p>
        : releases.isPending ? <div className="skeleton" style={{ height: 32 }} aria-busy="true" />
        : releases.isError ? <p className="error small" role="alert" style={{ margin: 0 }}>{describe(releases.error)}</p>
        : releases.data.length === 0 ? <p className="small muted" style={{ margin: 0 }}>{t("noReleases")}</p>
        : (
          <div className="sentence">
            <label className="small muted" htmlFor="install-version">{t("version")}</label>
            <select id="install-version" value={picked ? key(picked) : ""} onChange={(e) => setRelease(e.target.value)}>
              {releases.data.map((r) => (
                <option key={key(r)} value={key(r)}>
                  {[r.version, r.channel === "dev" ? t("dev") : null, r.publishedAt ? format.dateTime(new Date(r.publishedAt), { dateStyle: "medium" }) : null].filter(Boolean).join(" · ")}
                </option>
              ))}
            </select>
          </div>
        )
      )}
      {from === "url" && (
        <div className="sentence">
          <input aria-label={t("url")} placeholder="https://…" value={url} onChange={(e) => setUrl(e.target.value)} style={{ flex: 1, minWidth: 240 }} />
          <input aria-label={t("version")} placeholder={t("version")} value={version} onChange={(e) => setVersion(e.target.value)} />
        </div>
      )}
      {from === "path" && (
        <>
          <p className="small muted" style={{ margin: 0 }}>{t("pathHelp")}</p>
          <div className="sentence">
            <input aria-label={t("path")} placeholder={t("path")} value={path} onChange={(e) => setPath(e.target.value)} style={{ flex: 1, minWidth: 240 }} />
            <input aria-label={t("version")} placeholder={t("versionOptional")} value={version} onChange={(e) => setVersion(e.target.value)} />
          </div>
          <label className="small"><input type="checkbox" checked={adopt} onChange={(e) => setAdopt(e.target.checked)} /> {t("adopt")}</label>
        </>
      )}
      <p className="small muted" style={{ margin: 0 }}>{t("beside")}</p>
      <div className="hero-actions">
        <button className="ot-btn ot-btn-sm" type="button" onClick={() => { setOpen(false); install.reset(); }}>{t("cancel")}</button>
        <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={!ready || install.isPending} onClick={submit}>{t("submit")}</button>
      </div>
      {install.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {install.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(install.error)}</p>}
    </section>
  );
}

/**
 * La librería (#268, decisiones 293 y 304): lo instalado por emulador, cada versión con su origen y si ya tiene la segunda copia.
 * Una versión cuya carpeta ya no está vuelve con «Reinstalar», desde el archivo guardado y sin bajar nada.
 */
function Library({ kind }: { kind: Installable }) {
  const t = useTranslations("tools.library");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const list = useQuery(emulatorsQuery);
  const reinstall = useMutation({
    mutationFn: (i: EmulatorInstall) => request<number>("/api/emulators/reinstall", post({ emulator: i.emulatorId, version: i.version })),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const rows = (list.data ?? []).filter((i) => i.kind === kind);
  if (list.isError) return <p className="error small" role="alert">{describe(list.error)}</p>;
  if (rows.length === 0) return null;
  const names = [...new Set(rows.map((i) => i.name))];
  return (
    <section className="panel" style={{ display: "grid", gap: 8 }} aria-labelledby="library-title">
      <h3 id="library-title" style={{ margin: 0 }}>{t("title")}</h3>
      <p className="small muted" style={{ margin: 0 }}>{t("help")}</p>
      {names.map((name) => (
        <div key={name} style={{ display: "grid", gap: 4 }}>
          <b>{name}</b>
          {rows.filter((i) => i.name === name).map((i) => (
            <div key={i.version} className="version-row small">
              <span className="grow">
                <span>{i.version} · {t(`origin.${i.origin as "recipe" | "url" | "upload" | "adopted"}`)} · {format.dateTime(new Date(i.installedAt), { dateStyle: "medium" })}</span>
                {i.url && <span className="folder-path muted">{i.url}</span>}
              </span>
              <span style={{ display: "flex", gap: 6, alignItems: "center" }}>
                <span className={`ot-badge${i.backupLibraryId ? "" : " ot-badge-review"}`}>{t(i.backupLibraryId ? "twoCopies" : "oneCopy")}</span>
                {!i.path && <span className="ot-badge ot-badge-danger">{t("missing")}</span>}
                {!i.path && (
                  <button className="ot-btn ot-btn-sm" type="button" disabled={reinstall.isPending} onClick={() => reinstall.mutate(i)}>{t("reinstall")}</button>
                )}
              </span>
            </div>
          ))}
        </div>
      ))}
      {reinstall.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {reinstall.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(reinstall.error)}</p>}
    </section>
  );
}
