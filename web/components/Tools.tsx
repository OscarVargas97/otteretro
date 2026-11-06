"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useFormatter } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
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

/** Herramientas (§5.1, #193, #194, #200, #296, #304): el inventario por tipo, en pestañas y una sola columna centrada, con sus versiones nuevas, instalar y una tarjeta por emulador. */
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
  const all = (useQuery(recipesQuery).data ?? []).filter((r) => r.kind === kind);
  const consoles = useConsoles().data ?? [];
  // Emuladores: elegir la consola deja solo los suyos, con el base primero (#317).
  const [console_, setConsole] = useState("");
  const recipes = console_ === "" ? all : all.filter((r) => r.consoles.includes(console_))
    .sort((a, b) => Number(b.baseFor.includes(console_)) - Number(a.baseFor.includes(console_)));
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
  // El aviso de «al lado de las otras» solo vale si ese emulador ya tiene una versión instalada (#330, decisión 338).
  const hasOther = (useQuery({ ...emulatorsQuery, enabled: open }).data ?? []).some((i) => i.emulatorId === (recipe === OTHER ? slug(name) : recipe));
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
        {kind === "emulator" && <>
          <label className="small muted" htmlFor="install-console">{t("console")}</label>
          <select id="install-console" value={console_} onChange={(e) => { const c = e.target.value; setConsole(c); if (recipe !== OTHER && c !== "" && !all.find((r) => r.id === recipe)?.consoles.includes(c)) pick(""); }}>
            <option value="">{t("allConsoles")}</option>
            {consoles.filter((c) => all.some((r) => r.consoles.includes(c.id))).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </>}
        <label className="small muted" htmlFor="install-recipe">{t("what")}</label>
        <select id="install-recipe" value={recipe} onChange={(e) => pick(e.target.value)}>
          <option value="">{t("pick")}</option>
          {recipes.map((r) => { const label = r.experimental ? t("experimental", { name: r.name }) : r.name; return <option key={r.id} value={r.id}>{console_ !== "" && r.baseFor.includes(console_) ? t("base", { name: label }) : label}</option>; })}
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
      {hasOther && <p className="small muted" style={{ margin: 0 }}>{t("beside")}</p>}
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
 * Una tarjeta por emulador, port o herramienta (#268, #329; decisiones 293, 304 y 325): sus versiones, cada una instalada o solo
 * su archivo guardado, y la que usan tus perfiles. Un port tiene una sola instalada: las anteriores quedan como su archivo.
 * Al terminar una instalación que deja una versión nueva sin usar, pregunta si pasar los perfiles a ella.
 */
function Library({ kind }: { kind: Installable }) {
  const t = useTranslations("tools.library");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const list = useQuery(emulatorsQuery);
  const [confirm, setConfirm] = useState<string | null>(null);
  const [ask, setAsk] = useState<EmulatorInstall | null>(null);
  const seen = useRef<Set<string> | null>(null);
  const act = useMutation({
    mutationFn: ({ action, i }: { action: "use" | "remove" | "reinstall" | "open"; i: EmulatorInstall }) =>
      request<unknown>(`/api/emulators/${action}`, post({ emulator: i.emulatorId, version: i.version })),
    onSuccess: (data, { action }) => {
      setConfirm(null);
      setAsk(null);
      if (Array.isArray(data)) cache.setQueryData(["emulators"], data);
      if (action === "reinstall") cache.invalidateQueries({ queryKey: ["tasks"] });
    },
  });
  const rows = (list.data ?? []).filter((i) => i.kind === kind);
  // Una versión que aparece después de la primera carga, sin usar y con otra en uso: ofrece pasar los perfiles (decisión de #329).
  useEffect(() => {
    if (!list.data) return;
    const keys = new Set(list.data.map((i) => `${i.emulatorId}|${i.version}`));
    const before = seen.current;
    seen.current = keys;
    if (!before || kind !== "emulator") return;
    const fresh = list.data.find((i) => i.kind === kind && i.path && !i.inUse && !before.has(`${i.emulatorId}|${i.version}`)
      && list.data.some((o) => o.emulatorId === i.emulatorId && o.inUse));
    if (fresh) setAsk(fresh);
  }, [list.data, kind]);
  if (list.isError) return <p className="error small" role="alert">{describe(list.error)}</p>;
  if (rows.length === 0) return null;
  const ids = [...new Set(rows.map((i) => i.emulatorId))];
  const busy = act.isPending;
  return (
    <>
      {kind === "port" && <p className="small muted" style={{ margin: 0 }}>{t("portHelp")}</p>}
      {ids.map((id) => {
        const versions = rows.filter((i) => i.emulatorId === id);
        return (
          <section key={id} className="panel emulator-card" style={{ display: "grid", gap: 6 }} aria-label={versions[0].name}>
            <h3 style={{ margin: 0 }}>{versions[0].name}</h3>
            {ask?.emulatorId === id && (
              <div className="sentence" role="status">
                <span className="small">{t("askProfiles", { version: ask.version })}</span>
                <button type="button" className="ot-btn ot-btn-sm" onClick={() => setAsk(null)}>{t("no")}</button>
                <button type="button" className="ot-btn ot-btn-sm ot-btn-primary" disabled={busy} onClick={() => act.mutate({ action: "use", i: ask })}>{t("yes")}</button>
              </div>
            )}
            {versions.map((i) => {
              const row = `${id}|${i.version}`;
              return (
                <div key={i.version} className="version-row small">
                  <span className="grow">
                    <span>{i.version} · {t(`origin.${i.origin as "recipe" | "url" | "upload" | "adopted"}`)} · {format.dateTime(new Date(i.installedAt), { dateStyle: "medium" })}</span>
                    {i.url && <span className="folder-path muted">{i.url}</span>}
                  </span>
                  <span style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
                    {i.inUse && <span className="ot-badge ot-badge-have">{t("inUse")}</span>}
                    <span className={`ot-badge${i.path ? " ot-badge-have" : ""}`}>{t(i.path ? "installed" : "archived")}</span>
                    {kind === "emulator" && i.path && !i.inUse && (
                      <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act.mutate({ action: "use", i })}>{t("use")}</button>
                    )}
                    {i.path && <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act.mutate({ action: "open", i })}>{t("open")}</button>}
                    {!i.path && <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act.mutate({ action: "reinstall", i })}>{t("reinstall")}</button>}
                    {kind === "emulator" && i.path && !i.inUse && (confirm === row ? (
                      <>
                        <button className="ot-btn ot-btn-sm" type="button" onClick={() => setConfirm(null)}>{t("keep")}</button>
                        <button className="ot-btn ot-btn-sm ot-btn-danger" type="button" disabled={busy} onClick={() => act.mutate({ action: "remove", i })}>{t("removeConfirm")}</button>
                      </>
                    ) : <button className="ot-btn ot-btn-sm ot-btn-ghost" type="button" disabled={busy} onClick={() => setConfirm(row)}>{t("remove")}</button>)}
                  </span>
                </div>
              );
            })}
          </section>
        );
      })}
      {act.isSuccess && act.variables.action === "reinstall" && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {act.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(act.error)}</p>}
    </>
  );
}
