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
import { UpdateRow, UpdatesStatus } from "@/components/ToolUpdates";
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

/** Herramientas (§5.1, #193, #194, #200, #296, #304, #348): el inventario por tipo, en pestañas y una sola columna centrada: una barra con instalar, cargar perfil y las versiones nuevas, y una tarjeta por emulador, port o herramienta. */
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
            {kind !== "complement" && <ToolBar key={kind} kind={kind} />}
            <ToolCards kind={kind} />
          </div>
        )}
      </section>
    </div>
  );
}

/** La barra de arriba (#348): «Instalar…», «Cargar perfil» (Emuladores), cuántas versiones nuevas hay y «Revisar ahora», en una línea. */
function ToolBar({ kind }: { kind: Installable }) {
  const t = useTranslations("tools.install");
  const [open, setOpen] = useState(false);
  return (
    <>
      <div className="panel version-row">
        <span className="sentence">
          <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" aria-expanded={open} onClick={() => setOpen(!open)}>{t(`open.${kind}`)}</button>
          {kind === "emulator" && <LoadProfile />}
        </span>
        <UpdatesStatus kind={kind} />
      </div>
      {open && <InstallPanel kind={kind} onClose={() => setOpen(false)} />}
    </>
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

/** Un id propio para lo que no tiene receta, desde su nombre: «Mi Emulador» → mi-emulador. */
const slug = (name: string) => name.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const OTHER = ":other"; // «:» no va en un id de receta: no choca con ninguna

/**
 * Instalar (#268, #296): desde la receta eligiendo una versión publicada, desde la URL de una release o desde una carpeta o un
 * zip de este equipo (o adoptando uno instalado a mano). Cada versión va al lado de las otras: nunca se reemplaza una.
 */
function InstallPanel({ kind, onClose }: { kind: Installable; onClose: () => void }) {
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
  const hasOther = (useQuery(emulatorsQuery).data ?? []).some((i) => i.emulatorId === (recipe === OTHER ? slug(name) : recipe));
  const releases = useQuery({
    queryKey: ["recipes", recipe, "releases"],
    queryFn: () => request<RecipeRelease[]>(`/api/recipes/${encodeURIComponent(recipe)}/releases`),
    enabled: from === "recipe" && !!chosen?.releases,
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
        <button className="ot-btn ot-btn-sm" type="button" onClick={onClose}>{t("cancel")}</button>
        <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={!ready || install.isPending} onClick={submit}>{t("submit")}</button>
      </div>
      {install.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {install.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(install.error)}</p>}
    </section>
  );
}


/** Lo de un emulador, port o herramienta: sus versiones registradas y lo que hay en disco. Un complemento, una instalación. */
type Card = { id: string; name: string; installs: EmulatorInstall[]; tools: ToolInfo[] };
type Act = { action: "use" | "remove" | "reinstall" | "open"; i: EmulatorInstall };

/**
 * Una tarjeta por emulador, port, herramienta o complemento (#268, #329, #348; decisiones 293, 304, 325 y 343 a 345): lo
 * registrado (`/api/emulators`) y lo encontrado en disco (`/api/tools`) juntos. Emuladores y ports, en secciones por consola en
 * el orden de las consolas: un emulador de varias consolas aparece en cada una y un port va en la de su juego.
 * Al terminar una instalación que deja una versión nueva sin usar, pregunta si pasar los perfiles a ella.
 */
function ToolCards({ kind }: { kind: Exclude<Kind, "system"> }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const tools = useQuery(toolsQuery);
  const emulators = useQuery(emulatorsQuery);
  const recipes = new Map((useQuery(recipesQuery).data ?? []).map((r) => [r.id, r]));
  const consoles = useConsoles().data ?? [];
  const [ask, setAsk] = useState<EmulatorInstall | null>(null);
  const seen = useRef<Set<string> | null>(null);
  // Una versión que aparece después de la primera carga, sin usar y con otra en uso: ofrece pasar los perfiles (decisión de #329).
  useEffect(() => {
    const list = emulators.data;
    if (!list) return;
    const keys = new Set(list.map((i) => `${i.emulatorId}|${i.version}`));
    const before = seen.current;
    seen.current = keys;
    if (!before || kind !== "emulator") return;
    const fresh = list.find((i) => i.kind === kind && i.path && !i.inUse && !before.has(`${i.emulatorId}|${i.version}`)
      && list.some((o) => o.emulatorId === i.emulatorId && o.inUse));
    if (fresh) setAsk(fresh);
  }, [emulators.data, kind]);
  const error = tools.error ?? emulators.error;
  if (error) return <p className="error small" role="alert">{describe(error)}</p>;
  if (!tools.data || !emulators.data) return <div className="panel skeleton" style={{ height: 160 }} aria-busy="true" />;
  if (tools.data.length === 0 && emulators.data.length === 0) return <Empty title={t("emptyTitle")} text={t("emptyText")} />;

  const installs = emulators.data.filter((i) => i.kind === kind);
  const cards = new Map<string, Card>();
  const add = (id: string, name: string) => cards.get(id) ?? cards.set(id, { id, name, installs: [], tools: [] }).get(id)!;
  for (const i of installs) add(i.emulatorId, i.name).installs.push(i);
  // Lo de disco va con su versión registrada (la misma carpeta); si no está registrado, con su receta o, sin ella, por su nombre.
  for (const x of tools.data.filter((x) => x.kind === kind))
    add(kind === "complement" ? `${x.folder}|${x.name}|${x.version}` : installs.find((i) => i.path === x.path)?.emulatorId ?? x.recipeId ?? `:${x.name}`, x.name).tools.push(x);
  const list = [...cards.values()].sort((a, b) => a.name.localeCompare(b.name));
  if (list.length === 0) return <p className="missing small">{t("none")}</p>;

  // Las consolas de una tarjeta: las de su receta (emuladores) o la de su carpeta (ports, y emuladores de antes sin receta).
  const consolesOf = (k: Card) => {
    const r = recipes.get(k.id);
    if (kind === "emulator" && r?.consoles.length) return r.consoles;
    const folder = r?.folder || k.tools[0]?.folder;
    return consoles.filter((c) => c.name === folder).map((c) => c.id);
  };
  const sections = kind === "emulator" || kind === "port"
    ? [...consoles.map((c) => ({ key: c.id, title: c.name, cards: list.filter((k) => consolesOf(k).includes(c.id)) })),
       { key: OTHER, title: t("otherConsole"), cards: list.filter((k) => !consolesOf(k).some((id) => consoles.some((c) => c.id === id))) }]
      .filter((s) => s.cards.length > 0)
    : [{ key: kind, title: null, cards: list }];
  return (
    <>
      {kind === "port" && <p className="small muted" style={{ margin: 0 }}>{t("library.portHelp")}</p>}
      {sections.map((s) => (
        <div key={s.key} style={{ display: "grid", gap: 8 }}>
          {s.title && <h2 className="console-group">{s.title}</h2>}
          {s.cards.map((k) => (
            <ToolCard key={k.id} card={k} kind={kind} recipe={recipes.has(k.id)} game={recipes.get(k.id)?.game ?? k.tools[0]?.game}
              ask={ask?.emulatorId === k.id ? ask : null} onAsked={() => setAsk(null)} />
          ))}
        </div>
      ))}
    </>
  );
}

/**
 * La tarjeta (#348, decisión 344): en la cabecera el nombre, la versión en uso, su disco y el aviso de versión nueva (o «Sin
 * receta»), con abrir su carpeta y mover a otro disco; debajo los avisos, y plegadas las demás versiones con sus URL.
 */
function ToolCard({ card, kind, recipe, game, ask, onAsked }: {
  card: Card; kind: Exclude<Kind, "system">; recipe: boolean; game?: string | null; ask: EmulatorInstall | null; onAsked: () => void;
}) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const updates = (useQuery(toolUpdatesQuery).data ?? []).filter((u) => u.recipeId === card.id && u.kind === kind);
  const locations = useQuery(librariesQuery).data ?? [];
  const [confirm, setConfirm] = useState<string | null>(null);
  const act = useMutation({
    mutationFn: ({ action, i }: Act) => request<unknown>(`/api/emulators/${action}`, post({ emulator: i.emulatorId, version: i.version })),
    onSuccess: (data, { action }) => {
      setConfirm(null);
      onAsked();
      if (Array.isArray(data)) cache.setQueryData(["emulators"], data);
      if (action === "reinstall") cache.invalidateQueries({ queryKey: ["tasks"] });
    },
  });
  const current = card.installs.find((i) => i.inUse) ?? card.installs.at(-1);
  const here = current ? card.tools.find((x) => x.path === current.path) : card.tools.at(-1);
  const others = [
    ...card.installs.filter((i) => i !== current).map((i) => ({ i, x: card.tools.find((x) => x.path === i.path) })),
    ...card.tools.filter((x) => x !== here && !card.installs.some((i) => i.path === x.path)).map((x) => ({ i: undefined, x })),
  ];
  const where = here && (locations.find((l) => l.id === here.locationId)?.name ?? t("dataFolder"));
  const busy = act.isPending;
  const row = (i: EmulatorInstall | undefined, x: ToolInfo | undefined) => (
    <VersionRow key={i?.version ?? x!.path} kind={kind} i={i} x={x} busy={busy} confirm={!!i && confirm === i.version}
      onConfirm={(on) => setConfirm(on ? i!.version : null)} act={(action) => act.mutate({ action, i: i! })} />
  );
  return (
    <section className="panel emulator-card" style={{ display: "grid", gap: 6 }} aria-label={card.name}>
      <div className="version-row">
        <span className="grow">
          <h3 style={{ margin: 0 }}>{card.name}</h3>
          {(game || kind === "complement") && <span className="small muted">{game ?? here?.folder}</span>}
        </span>
        <span style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
          {current?.inUse ? <span className="ot-badge ot-badge-have">{t("library.current", { version: current.version })}</span>
            : <span className="ot-badge">{current?.version ?? here?.version}</span>}
          {current && !current.path && <span className="ot-badge">{t("library.archived")}</span>}
          {here && <span className="ot-badge" title={here.path}>{where}</span>}
          {updates.length > 0 && <span className="ot-badge ot-badge-info">{t("updates.badge")}</span>}
          {!recipe && !card.tools.some((x) => x.recipeId) && <span className="ot-badge ot-badge-info" title={t("noRecipeHelp")}>{t("noRecipe")}</span>}
        </span>
      </div>
      <div className="sentence">
        {current?.path && <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act.mutate({ action: "open", i: current })}>{t("library.open")}</button>}
        {current && !current.path && <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act.mutate({ action: "reinstall", i: current })}>{t("library.reinstall")}</button>}
        {here && <MoveTool tool={here} />}
        {kind === "emulator" && card.installs.length > 0 && <Uninstall card={card} />}
      </div>
      {ask && (
        <div className="sentence" role="status">
          <span className="small">{t("library.askProfiles", { version: ask.version })}</span>
          <button type="button" className="ot-btn ot-btn-sm" onClick={onAsked}>{t("library.no")}</button>
          <button type="button" className="ot-btn ot-btn-sm ot-btn-primary" disabled={busy} onClick={() => act.mutate({ action: "use", i: ask })}>{t("library.yes")}</button>
        </div>
      )}
      {updates.map((u) => <UpdateRow key={`${u.recipeId}-${u.channel}`} u={u} />)}
      {kind !== "complement" && (current || others.length > 0) && (
        <details className="tool-versions">
          <summary className="small">{others.length > 0 ? t("library.more", { count: others.length }) : t("library.details")}</summary>
          {current && row(current, here)}
          {others.map(({ i, x }) => row(i, x))}
        </details>
      )}
      {act.isSuccess && act.variables.action === "reinstall" && <p className="small muted" role="status" style={{ margin: 0 }}>{t("library.queued")}</p>}
      {act.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(act.error)}</p>}
    </section>
  );
}

/** «Quitar el emulador» (#351): todas sus versiones; se confirma escribiendo su nombre. Partidas, configuración y perfiles se quedan. */
function Uninstall({ card }: { card: Card }) {
  const t = useTranslations("tools.library");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [open, setOpen] = useState(false);
  const [typed, setTyped] = useState("");
  const remove = useMutation({
    mutationFn: () => request<EmulatorInstall[]>(`/api/emulators/${encodeURIComponent(card.id)}`, { method: "DELETE" }),
    onSuccess: (data) => {
      cache.setQueryData(["emulators"], data);
      cache.invalidateQueries({ queryKey: ["tools"] });
    },
  });
  if (!open) return <button className="ot-btn ot-btn-sm ot-btn-ghost" type="button" onClick={() => { setOpen(true); setTyped(""); }}>{t("uninstall")}</button>;
  return (
    <form className="sentence" onSubmit={(e) => { e.preventDefault(); if (typed.trim() === card.name) remove.mutate(); }}>
      <span className="small">{t("uninstallHelp", { name: card.name })}</span>
      <input aria-label={t("uninstallName")} value={typed} onChange={(e) => setTyped(e.target.value)} />
      <button className="ot-btn ot-btn-sm" type="button" onClick={() => setOpen(false)}>{t("keep")}</button>
      <button className="ot-btn ot-btn-sm ot-btn-danger" type="submit" disabled={typed.trim() !== card.name || remove.isPending}>{t("uninstallConfirm")}</button>
      {remove.isError && <span className="error small" role="alert">{describe(remove.error)}</span>}
    </form>
  );
}

/** Una versión en el plegable: su origen, fecha y URL, y usarla, abrirla, borrarla o reinstalarla; lo que no está registrado, moverlo. */
function VersionRow({ kind, i, x, busy, confirm, onConfirm, act }: {
  kind: Kind; i?: EmulatorInstall; x?: ToolInfo; busy: boolean; confirm: boolean; onConfirm: (on: boolean) => void; act: (action: Act["action"]) => void;
}) {
  const t = useTranslations("tools.library");
  const format = useFormatter();
  if (!i) return (
    <div className="version-row small">
      <span className="grow"><span>{x!.version}</span><span className="folder-path muted">{x!.path}</span></span>
      <MoveTool tool={x!} />
    </div>
  );
  return (
    <div className="version-row small">
      <span className="grow">
        <span>{i.version} · {t(`origin.${i.origin as "recipe" | "url" | "upload" | "adopted"}`)} · {format.dateTime(new Date(i.installedAt), { dateStyle: "medium" })}</span>
        {i.url && <span className="folder-path muted">{i.url}</span>}
      </span>
      <span style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
        {i.inUse && <span className="ot-badge ot-badge-have">{t("inUse")}</span>}
        <span className={`ot-badge${i.path ? " ot-badge-have" : ""}`}>{t(i.path ? "installed" : "archived")}</span>
        {!i.inUse && <>
          {kind === "emulator" && i.path && <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act("use")}>{t("use")}</button>}
          {i.path && <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act("open")}>{t("open")}</button>}
          {!i.path && <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act("reinstall")}>{t("reinstall")}</button>}
          {kind === "emulator" && i.path && (confirm ? (
            <>
              <button className="ot-btn ot-btn-sm" type="button" onClick={() => onConfirm(false)}>{t("keep")}</button>
              <button className="ot-btn ot-btn-sm ot-btn-danger" type="button" disabled={busy} onClick={() => act("remove")}>{t("removeConfirm")}</button>
            </>
          ) : <button className="ot-btn ot-btn-sm ot-btn-ghost" type="button" disabled={busy} onClick={() => onConfirm(true)}>{t("remove")}</button>)}
          {x && <MoveTool tool={x} />}
        </>}
      </span>
    </div>
  );
}

/** Mover una instalación a otro disco (#198, #205), en una tarea. */
function MoveTool({ tool }: { tool: ToolInfo }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const targets = (useQuery(librariesQuery).data ?? []).filter((l) => l.type === "disk" && l.connected && l.id !== tool.locationId);
  const [moving, setMoving] = useState(false);
  const [to, setTo] = useState("");
  const move = useMutation({
    mutationFn: () => request<number>("/api/tools/move", post({ path: tool.path, toLocation: to || null })),
    onSuccess: () => { setMoving(false); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });
  if (targets.length === 0) return null;
  return (
    <span className="sentence">
      {!moving && <button className="ot-btn ot-btn-sm" type="button" onClick={() => { setMoving(true); setTo(targets[0].id); move.reset(); }}>{t("move")}</button>}
      {moving && <>
        <label className="small muted" htmlFor={`move-${tool.path}`}>{t("to")}</label>
        <select id={`move-${tool.path}`} value={to} onChange={(e) => setTo(e.target.value)}>
          {targets.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
        </select>
        <button className="ot-btn ot-btn-sm" type="button" onClick={() => setMoving(false)}>{t("cancel")}</button>
        <button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={move.isPending} onClick={() => move.mutate()}>{t("confirm")}</button>
      </>}
      {move.isSuccess && <span className="small muted" role="status">{t("queued")}</span>}
      {move.isError && <span className="error small" role="alert">{describe(move.error)}</span>}
    </span>
  );
}
