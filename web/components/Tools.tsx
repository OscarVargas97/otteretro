"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useFormatter } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { useConsoles } from "@/components/Catalog";
import { librariesQuery, toolsQuery } from "@/components/Libraries";
import { LoadProfile } from "@/components/ProfileShare";
import { Empty } from "@/components/Shell";
import { Tabs } from "@/components/Tabs";
import { UpdateRow, UpdatesNotice } from "@/components/ToolUpdates";
import { nestedForkIds } from "@/lib/forks";
import { toolUpdatesQuery } from "@/lib/queries";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { useTasks } from "@/components/Tasks";
import type { EmulatorInstall, RecipeRelease, RecipeSummary, RepoRelease, SystemConsole, ToolInfo } from "@/lib/types";

const KINDS = ["emulator", "port", "complement", "tool", "system"] as const;
type Kind = (typeof KINDS)[number];
/** Lo que se instala con su receta, una URL o un zip (#296): emuladores, ports y herramientas como Steam ROM Manager (decisión 311). */
type Installable = "emulator" | "port" | "tool";

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });
export const recipesQuery = { queryKey: ["recipes"], queryFn: () => request<RecipeSummary[]>("/api/recipes"), staleTime: Infinity };
export const emulatorsQuery = { queryKey: ["emulators"], queryFn: () => request<EmulatorInstall[]>("/api/emulators") };
const systemQuery = { queryKey: ["system"], queryFn: () => request<SystemConsole[]>("/api/system") };

/**
 * Herramientas (§5.1, #193, #194, #200, #296, #304, #348, #416, #432): el inventario por tipo, en pestañas; debajo, el aviso de
 * versiones nuevas con «Actualizar todo»; una lista con el detalle de lo elegido al lado. «Instalar…» va en la cabecera.
 */
export function ToolsPage() {
  const t = useTranslations("tools");
  const router = useRouter();
  const asked = useSearchParams().get("k");
  const kind: Kind = KINDS.find((k) => k === asked) ?? "emulator";
  // Cada pestaña cuenta lo pendiente: versiones nuevas (decisión 320) o, en BIOS y firmware, las consolas a las que les falta algo.
  const updates = useQuery(toolUpdatesQuery).data ?? [];
  const missing = (useQuery(systemQuery).data ?? []).filter((c) => c.missing.length > 0).length;
  const count = (k: Kind) => k === "system" ? missing : updates.filter((u) => u.kind === k).length;
  return (
    <div className="tools-page">
      {kind !== "complement" && kind !== "system" && <TopActions key={kind} kind={kind} />}
      <Tabs tabs={KINDS.map((k) => ({ id: k, label: t(`kind.${k}`), count: count(k), countLabel: t(k === "system" ? "systemMissingCount" : "updates.count", { count: count(k) }) }))}
        current={kind} label={t("sections")} panel="tools-panel" onSelect={(k) => router.push(`/tools/?k=${k}`)} />
      <UpdatesNotice />
      <section className="sec" id="tools-panel" role="tabpanel" aria-labelledby={`tab-${kind}`}>
        {kind === "system" ? <SystemPanel /> : <ToolList key={kind} kind={kind} />}
      </section>
    </div>
  );
}

/**
 * En la cabecera, como en el diseño (#432): «Instalar…» (que abre el diálogo de dos pasos, #416) y, en Emuladores, «Cargar perfil».
 * Van por un portal a la barra de arriba; con useLayoutEffect llegan antes de pintar, así no se ve el salto.
 */
function TopActions({ kind }: { kind: Installable }) {
  const t = useTranslations("tools.install");
  const dialog = useRef<HTMLDialogElement>(null);
  const [opened, setOpened] = useState(0); // cada vez que se abre, el diálogo empieza de cero
  const [slot, setSlot] = useState<HTMLElement | null>(null);
  useLayoutEffect(() => setSlot(document.getElementById("top-actions")), []);
  return (
    <>
      {slot && createPortal(<>
        {kind === "emulator" && <LoadProfile />}
        <button className="ot-btn ot-btn-primary" type="button" onClick={() => { setOpened(opened + 1); dialog.current?.showModal(); }}>{t("header")}</button>
      </>, slot)}
      <dialog ref={dialog} className="ot-dialog" aria-labelledby="install-title">
        {opened > 0 && <InstallDialog key={opened} kind={kind} onClose={() => dialog.current?.close()} />}
      </dialog>
    </>
  );
}

/** Una fila de la lista (#416): nombre, para qué es, la versión y su estado; elegirla muestra su detalle al lado. */
function ListRow({ name, sub, meta, badge, picked, onPick }: { name: string; sub?: string | null; meta?: string | null; badge: ReactNode; picked: boolean; onPick: () => void }) {
  return (
    <button type="button" className="tool-row" aria-pressed={picked} onClick={onPick}>
      <span className="grow"><b>{name}</b>{sub && <span className="small muted">{sub}</span>}</span>
      <span style={{ display: "flex", gap: 6, alignItems: "center" }}>{meta && <span className="small muted">{meta}</span>}{badge}</span>
    </button>
  );
}

/** BIOS, keys y firmware (§8.5, #199): lo que tienes por consola, con versiones, y subir otro. Otra versión nunca reemplaza la anterior. */
function SystemPanel() {
  const t = useTranslations("tools");
  const format = useFormatter();
  const describe = useDescribeError();
  const consoles = useConsoles().data ?? [];
  const list = useQuery(systemQuery);
  const [console_, setConsole] = useState("");
  const [sel, setSel] = useState<string | null>(null);
  // Lo elegido en la lista o, si no, la consola elegida arriba para subir.
  const picked = list.data?.find((c) => c.id === (sel ?? console_)) ?? list.data?.[0];
  return (
    <div style={{ display: "grid", gap: 12 }}>
      <section className="panel" style={{ display: "grid", gap: 8 }}>
        <p className="small" style={{ margin: 0 }}>{t("systemHelp")}</p>
        <div className="hero-actions">
          <select className="ot-btn ot-btn-sm" aria-label={t("systemConsole")} value={console_} onChange={(e) => { setConsole(e.target.value); setSel(null); }}>
            <option value="">{t("systemConsole")}</option>
            {consoles.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <SystemUpload console={console_} label={t("systemUpload")} />
        </div>
      </section>
      {list.isError && <p className="error small" role="alert">{describe(list.error)}</p>}
      {list.data?.length === 0 && <Empty title={t("systemEmptyTitle")} text={t("systemEmptyText")} />}
      {picked && (
        <div className="tool-split">
          <div className="panel tool-list">
            {list.data!.map((c) => (
              <ListRow key={c.id} name={c.name} picked={c === picked} onPick={() => setSel(c.id)}
                badge={c.missing.length > 0 ? <span className="ot-badge ot-badge-danger">{t("systemMissingCount", { count: c.missing.length })}</span>
                  : <span className="ot-badge ot-badge-have">{t("systemComplete")}</span>} />
            ))}
          </div>
          <section className="panel" aria-label={picked.name} style={{ gap: 8 }}>
            <h3 style={{ margin: 0 }}>{picked.name}</h3>
            {picked.missing.map((m) => <span key={m} className="ot-badge ot-badge-review" style={{ alignSelf: "start" }}>{t("systemMissing", { name: m })}</span>)}
            <SystemUpload console={picked.id} label={t("systemUploadFor", { name: picked.name })} />
            {picked.files.map((f) => (
              <div key={f.path} className="version-row small">
                <span className="folder-path">{f.name}{f.version && ` · ${f.version}`}</span>
                <span className="muted">{f.disk} · {format.dateTime(new Date(f.modified), { dateStyle: "medium" })}</span>
              </div>
            ))}
          </section>
        </div>
      )}
    </div>
  );
}

/** Subir un archivo de sistema a una consola, al lado de los que ya hay. */
function SystemUpload({ console: id, label }: { console: string; label: string }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const input = useRef<HTMLInputElement>(null);
  const upload = useMutation({
    mutationFn: (file: File) => request<void>(`/api/system/${encodeURIComponent(id)}?${new URLSearchParams({ name: file.name })}`,
      { method: "POST", body: file, headers: { "Content-Type": "application/octet-stream" } }),
    onSettled: () => {
      if (input.current) input.current.value = "";
      cache.invalidateQueries({ queryKey: ["system"] });
    },
  });
  return (
    <span className="sentence">
      <input ref={input} type="file" style={{ maxWidth: "100%" }} aria-label={label} disabled={!id || upload.isPending}
        onChange={(e) => { const file = e.target.files?.[0]; if (file) upload.mutate(file); }} />
      {upload.isPending && <span className="small muted" role="status">{t("systemUploading")}</span>}
      {upload.isError && <span className="error small" role="alert">{describe(upload.error)}</span>}
    </span>
  );
}

/** Un id propio para lo que no tiene receta, desde su nombre: «Mi Emulador» → mi-emulador. */
const slug = (name: string) => name.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const OTHER = ":other"; // «:» no va en un id de receta: no choca con ninguna
const releaseKey = (r: { channel: string; version: string }) => `${r.channel}|${r.version}`;

/**
 * Instalar (#268, #296, #350, #416), en dos pasos. 1) qué y de dónde: la receta, o lo pegado (su repo, la URL de un archivo o
 * una carpeta o un zip de este equipo), y se dice qué se reconoció. 2) confirmar; la versión, el archivo y adoptar, en
 * «Más opciones». Cada versión va al lado de las otras: nunca se reemplaza una.
 */
function InstallDialog({ kind, onClose }: { kind: Installable; onClose: () => void }) {
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
  const [step, setStep] = useState<1 | 2>(1);
  const [recipe, setRecipe] = useState("");
  const [name, setName] = useState("");
  const [origin, setOrigin] = useState("");
  const [release, setRelease] = useState("");
  const [version, setVersion] = useState("");
  const [asset, setAsset] = useState("");
  const [adopt, setAdopt] = useState(false);
  const [exe, setExe] = useState("");
  const src = origin.trim();
  // Lo pegado dice de dónde: nada es la receta; una URL (o un dominio con ruta, como github.com/x/y), su repo o su archivo; lo demás, una ruta de este equipo.
  const from = src === "" ? "recipe" : /^https?:\/\//i.test(src) || /^[\w-]+(\.[\w-]+)+\//.test(src) ? "url" : "path";
  const direct = from === "url" && (/\.(zip|7z|rar|exe|tar\.gz|tgz)$/i.test(src) || src.includes("/releases/download/"));
  const chosen = recipes.find((r) => r.id === recipe);
  // El aviso de «al lado de las otras» solo vale si ese emulador ya tiene una versión instalada (#330, decisión 338).
  const hasOther = (useQuery(emulatorsQuery).data ?? []).some((i) => i.emulatorId === (recipe === OTHER ? slug(name) : recipe));
  const releases = useQuery({
    queryKey: ["recipes", recipe, "releases"],
    queryFn: () => request<RecipeRelease[]>(`/api/recipes/${encodeURIComponent(recipe)}/releases`),
    enabled: from === "recipe" && !!chosen?.releases,
    staleTime: 5 * 60_000,
  });
  const picked = releases.data?.find((r) => releaseKey(r) === release) ?? releases.data?.[0];
  // Un repo (GitHub, Forgejo, Gitea) lista sus releases al pasar al paso 2 (#350).
  const repoReleases = useQuery({
    queryKey: ["repos", src, "releases"],
    queryFn: () => request<RepoRelease[]>(`/api/repos/releases?url=${encodeURIComponent(src)}`),
    enabled: step === 2 && from === "url" && !direct,
    staleTime: 5 * 60_000,
  });
  const repoRelease = repoReleases.data?.find((r) => releaseKey(r) === release) ?? repoReleases.data?.[0];
  // El archivo de Windows: si hay uno solo, ese; si hay varios (o ninguno parece de Windows), se elige.
  const candidates = repoRelease ? (repoRelease.assets.some((a) => a.windows) ? repoRelease.assets.filter((a) => a.windows) : repoRelease.assets) : [];
  const file = candidates.find((a) => a.url === asset) ?? candidates[0];
  const install = useMutation({
    mutationFn: (job: object) => request<number>("/api/emulators/install", post(job)),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  // Sin ejecutable claro en lo bajado, la tarea pregunta cuál (#350): se elige y se reintenta con lo que ya quedó en la librería.
  const task = useTasks().find((x) => x.id === install.data);
  const exes = task?.detail?.code === "install.pickExe" ? task.detail.params?.files?.split("|") ?? [] : [];
  // Sin receta: el id sale del nombre y la carpeta se llama como él; desde un repo, queda su receta local.
  const who = recipe === OTHER ? { emulator: slug(name), name: name.trim(), kind } : { emulator: recipe };
  const canNext = recipe === OTHER ? slug(name) !== "" && src !== "" : !!chosen && (src !== "" || chosen.releases);
  const ready = canNext && (from === "recipe" ? !!picked : from === "url" ? (direct ? src.startsWith("https://") && version.trim() !== "" : !!file) : true);
  const job = (): object =>
    from === "recipe" ? { emulator: recipe, version: picked!.version, url: picked!.download }
    : from === "url" && direct ? { ...who, version: version.trim(), url: src }
    : from === "url" ? { ...who, version: repoRelease!.version, url: file!.url, ...(recipe === OTHER ? { repo: src, console: console_ || null } : {}) }
    : { ...who, version: version.trim() || null, path: src, adopt };
  const label = (r: { version: string; channel: string; publishedAt?: string | null }) =>
    [r.version, r.channel === "dev" ? t("dev") : null, r.publishedAt ? format.dateTime(new Date(r.publishedAt), { dateStyle: "medium" }) : null].filter(Boolean).join(" · ");
  const shown = recipe === OTHER ? name.trim() : chosen?.name;
  const shownVersion = from === "recipe" ? picked?.version : from === "url" && !direct ? repoRelease?.version : version.trim();

  return (
    <section className="settings-blocks" aria-labelledby="install-title">
      <h3 id="install-title" style={{ margin: 0 }}>{t("title", { what: t(`open.${kind}`), step })}</h3>
      {step === 1 ? <>
        <div className="sentence">
          {kind === "emulator" && <>
            <label className="small muted" htmlFor="install-console">{t("console")}</label>
            <select id="install-console" value={console_} onChange={(e) => { const c = e.target.value; setConsole(c); if (recipe !== OTHER && c !== "" && !all.find((r) => r.id === recipe)?.consoles.includes(c)) setRecipe(""); }}>
              <option value="">{t("allConsoles")}</option>
              {consoles.filter((c) => recipe === OTHER || all.some((r) => r.consoles.includes(c.id))).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </>}
          <label className="small muted" htmlFor="install-recipe">{t("what")}</label>
          <select id="install-recipe" value={recipe} onChange={(e) => { setRecipe(e.target.value); setRelease(""); }}>
            <option value="">{t("pick")}</option>
            {recipes.map((r) => { const name = r.experimental ? t("experimental", { name: r.name }) : r.name; return <option key={r.id} value={r.id}>{console_ !== "" && r.baseFor.includes(console_) ? t("base", { name }) : name}</option>; })}
            {kind !== "tool" && <option value={OTHER}>{t("other")}</option>}
          </select>
          {recipe === OTHER && <input aria-label={t("name")} placeholder={t("name")} value={name} onChange={(e) => setName(e.target.value)} />}
        </div>
        <label className="small muted" htmlFor="install-origin">{t(recipe === OTHER ? "origin" : "originOptional")}</label>
        <input id="install-origin" placeholder={t("originHint")} value={origin} onChange={(e) => { setOrigin(e.target.value); setRelease(""); setAsset(""); }} />
        {(chosen || recipe === OTHER) && (
          from === "recipe" && chosen && !chosen.releases ? <p className="small muted" style={{ margin: 0 }}>{t("noReleases")}</p>
          : (from !== "recipe" || chosen) && <p className="small" role="status" style={{ margin: 0 }}>✓ {t(`recognized.${from === "url" ? (direct ? "file" : "repo") : from}`)}</p>
        )}
        <div className="hero-actions">
          <button className="ot-btn ot-btn-sm" type="button" onClick={onClose}>{t("cancel")}</button>
          <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={!canNext} onClick={() => setStep(2)}>{t("next")}</button>
        </div>
      </> : <>
        <p style={{ margin: 0 }}><b>{shown}</b>{shownVersion && ` ${shownVersion}`} · <span className="muted">{t(`fromKind.${from}`)}</span></p>
        {from === "recipe" && (releases.isPending ? <div className="skeleton" style={{ height: 32 }} aria-busy="true" />
          : releases.isError ? <p className="error small" role="alert" style={{ margin: 0 }}>{describe(releases.error)}</p>
          : releases.data.length === 0 && <p className="small muted" style={{ margin: 0 }}>{t("noReleases")}</p>)}
        {from === "url" && !direct && (repoReleases.isPending ? <div className="skeleton" style={{ height: 32 }} aria-busy="true" />
          : repoReleases.isError ? <p className="error small" role="alert" style={{ margin: 0 }}>{describe(repoReleases.error)}</p>
          : repoReleases.data.length === 0 ? <p className="small muted" style={{ margin: 0 }}>{t("noRepoReleases")}</p>
          : file && <span className="small muted">{file.name}</span>)}
        {direct && <input aria-label={t("version")} placeholder={t("version")} value={version} onChange={(e) => setVersion(e.target.value)} />}
        {!direct && (
          <details className="tool-versions">
            <summary className="small">{t("moreOptions")}</summary>
            {from === "recipe" && !!releases.data?.length && (
              <div className="sentence">
                <label className="small muted" htmlFor="install-version">{t("version")}</label>
                <select id="install-version" value={picked ? releaseKey(picked) : ""} onChange={(e) => setRelease(e.target.value)}>
                  {releases.data.map((r) => <option key={releaseKey(r)} value={releaseKey(r)}>{label(r)}</option>)}
                </select>
              </div>
            )}
            {from === "url" && !!repoReleases.data?.length && (
              <div className="sentence">
                <label className="small muted" htmlFor="install-repo-version">{t("version")}</label>
                <select id="install-repo-version" value={repoRelease ? releaseKey(repoRelease) : ""} onChange={(e) => { setRelease(e.target.value); setAsset(""); }}>
                  {repoReleases.data.map((r) => <option key={releaseKey(r)} value={releaseKey(r)}>{label(r)}</option>)}
                </select>
                {candidates.length > 1 && <>
                  <label className="small muted" htmlFor="install-asset">{t("file")}</label>
                  <select id="install-asset" value={file?.url ?? ""} onChange={(e) => setAsset(e.target.value)}>
                    {candidates.map((a) => <option key={a.url} value={a.url}>{a.name}</option>)}
                  </select>
                </>}
              </div>
            )}
            {from === "path" && <>
              <input aria-label={t("version")} placeholder={t("versionOptional")} value={version} onChange={(e) => setVersion(e.target.value)} />
              <label className="small"><input type="checkbox" checked={adopt} onChange={(e) => setAdopt(e.target.checked)} /> {t("adopt")}</label>
            </>}
          </details>
        )}
        {hasOther && <p className="small muted" style={{ margin: 0 }}>{t("beside")}</p>}
        <div className="hero-actions">
          <button className="ot-btn ot-btn-sm" type="button" onClick={() => setStep(1)}>{t("back")}</button>
          <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={!ready || install.isPending || install.isSuccess} onClick={() => install.mutate(job())}>{t("submit")}</button>
        </div>
        {install.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
        {exes.length > 0 && (
          <div className="sentence">
            <label className="small muted" htmlFor="install-exe">{t("pickExe")}</label>
            <select id="install-exe" value={exe} onChange={(e) => setExe(e.target.value)}>
              <option value="">{t("pick")}</option>
              {exes.map((x) => <option key={x} value={x}>{x}</option>)}
            </select>
            <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={exe === ""} onClick={() => install.mutate({ ...job(), detect: exe })}>{t("submit")}</button>
          </div>
        )}
        {install.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(install.error)}</p>}
      </>}
    </section>
  );
}

/** Lo de un emulador, port o herramienta: sus versiones registradas y lo que hay en disco. Un complemento, una instalación. */
type Card = { id: string; name: string; installs: EmulatorInstall[]; tools: ToolInfo[] };
type Act = { action: "use" | "remove" | "reinstall" | "open" | "launch"; i: EmulatorInstall };

/** La versión que se muestra de una tarjeta: la que se usa (o la última registrada) y su carpeta en disco. */
const currentOf = (card: Card) => {
  const current = card.installs.find((i) => i.inUse) ?? card.installs.at(-1);
  return { current, here: current ? card.tools.find((x) => x.path === current.path) : card.tools.at(-1) };
};

/**
 * La lista de emuladores, ports, herramientas o complementos con el detalle de lo elegido al lado (#268, #329, #348, #416):
 * lo registrado (`/api/emulators`) y lo encontrado en disco (`/api/tools`) juntos, por nombre; cada fila dice para qué
 * consola o juego es, la versión y su estado. Al terminar una instalación que deja una versión nueva sin usar, pregunta si
 * pasar los perfiles a ella.
 */
function ToolList({ kind }: { kind: Exclude<Kind, "system"> }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const tools = useQuery(toolsQuery);
  const emulators = useQuery(emulatorsQuery);
  const recipes = new Map((useQuery(recipesQuery).data ?? []).map((r) => [r.id, r]));
  const consoles = useConsoles().data ?? [];
  const updates = (useQuery(toolUpdatesQuery).data ?? []).filter((u) => u.kind === kind);
  const [sel, setSel] = useState<string | null>(null);
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
    if (fresh) { setAsk(fresh); setSel(fresh.emulatorId); }
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
  // Los forks van en el detalle de su original (#358); si el original no está en la lista, el fork sale solo.
  const nested = nestedForkIds(list, (id) => recipes.get(id)?.forkOf);
  const top = list.filter((k) => !nested.has(k.id));
  const forksOf = (id: string) => [...recipes.values()].filter((r) => r.forkOf === id && r.kind === kind).sort((a, b) => a.name.localeCompare(b.name));
  const picked = list.find((k) => k.id === sel) ?? top[0];

  // Para qué es: las consolas de su receta (emuladores) o la de su carpeta; un port, su juego; un complemento, su carpeta.
  const forWhat = (k: Card) => {
    const r = recipes.get(k.id);
    if (kind === "emulator" && r?.consoles.length) return consoles.filter((c) => r.consoles.includes(c.id)).map((c) => c.name).join(" · ");
    if (kind === "port") return r?.game ?? k.tools[0]?.game;
    return r?.folder || k.tools[0]?.folder;
  };
  const badge = (k: Card) => {
    const u = updates.find((u) => u.recipeId === k.id);
    const { current } = currentOf(k);
    return u ? <span className="ot-badge ot-badge-info">{t("updates.newVersion", { version: u.version })}</span>
      : current && !current.path ? <span className="ot-badge">{t("library.archived")}</span>
      : <span className="ot-badge ot-badge-have">{t("library.installed")}</span>;
  };
  return (
    <>
      {kind === "port" && <p className="small muted" style={{ margin: 0 }}>{t("library.portHelp")}</p>}
      <div className="tool-split">
        <div className="panel tool-list">
          {top.map((k) => { const { current, here } = currentOf(k); return (
            <ListRow key={k.id} name={k.name} sub={forWhat(k)} meta={current?.version ?? here?.version} badge={badge(k)}
              picked={k === picked} onPick={() => setSel(k.id)} />
          ); })}
        </div>
        <ToolDetail key={picked.id} card={picked} kind={kind} recipe={recipes.has(picked.id)} forWhat={forWhat(picked)} badge={badge(picked)}
          ask={ask?.emulatorId === picked.id ? ask : null} onAsked={() => setAsk(null)} onPick={setSel}
          forks={forksOf(picked.id).map((r) => ({ recipe: r, card: list.find((c) => c.id === r.id) }))} />
      </div>
    </>
  );
}

/**
 * El detalle de lo elegido (#348, #416; decisión 344): nombre, para qué es y su estado; la acción principal (actualizar con el
 * aviso de versión nueva, abrir su carpeta o reinstalar); plegadas, sus versiones y «Más» (mover a otro disco, quitar el emulador).
 */
function ToolDetail({ card, kind, recipe, forWhat, badge, ask, onAsked, onPick, forks }: {
  card: Card; kind: Exclude<Kind, "system">; recipe: boolean; forWhat?: string | null; badge: ReactNode; ask: EmulatorInstall | null;
  onAsked: () => void; onPick: (id: string) => void; forks: { recipe: RecipeSummary; card?: Card }[];
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
  const { current, here } = currentOf(card);
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
  // Desde la receta, no desde la librería: lo guardado puede ser lo que dejó la instalación rota (#446).
  const again = useMutation({
    mutationFn: () => installRecipe(card.id),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const uninstall = kind !== "complement" && card.installs.length > 0;
  return (
    <section className="panel emulator-card" style={{ gap: 8 }} aria-label={card.name}>
      <div className="version-row">
        <span className="grow">
          <h3 style={{ margin: 0 }}>{card.name}</h3>
          {forWhat && <span className="small muted">{forWhat}</span>}
        </span>
        {badge}
      </div>
      <span style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
        {current?.inUse ? <span className="ot-badge ot-badge-have">{t("library.current", { version: current.version })}</span>
          : <span className="ot-badge">{current?.version ?? here?.version}</span>}
        {here && <span className="ot-badge" title={here.path}>{where}</span>}
        {!recipe && !card.tools.some((x) => x.recipeId) && <span className="ot-badge ot-badge-info" title={t("noRecipeHelp")}>{t("noRecipe")}</span>}
      </span>
      {updates.map((u) => <UpdateRow key={`${u.recipeId}-${u.channel}`} u={u} />)}
      {current?.broken && <p className="small" role="status" style={{ margin: 0 }}>{t("library.broken")}</p>}
      <div className="sentence">
        {current?.broken && recipe && <button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={again.isPending || again.isSuccess} onClick={() => again.mutate()}>{t("library.installAgain")}</button>}
        {current && !current.path &&<button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={busy} onClick={() => act.mutate({ action: "reinstall", i: current })}>{t("library.reinstall")}</button>}
        {/* «Abrir» el programa y «Abrir carpeta» (#432); un complemento no se abre solo. */}
        {current?.path && !current.broken && kind !== "complement" && <button className="ot-btn ot-btn-sm" type="button" disabled={busy} onClick={() => act.mutate({ action: "launch", i: current })}>{t("library.launch")}</button>}
        {current?.path && <button className="ot-btn ot-btn-sm ot-btn-ghost" type="button" disabled={busy} onClick={() => act.mutate({ action: "open", i: current })}>{t("library.open")}</button>}
      </div>
      {/* A la vista, no en «Más» (#446): pide escribir el nombre antes de borrar. */}
      {uninstall && <Uninstall card={card} />}
      {ask && (
        <div className="sentence" role="status">
          <span className="small">{t("library.askProfiles", { version: ask.version })}</span>
          <button type="button" className="ot-btn ot-btn-sm" onClick={onAsked}>{t("library.no")}</button>
          <button type="button" className="ot-btn ot-btn-sm ot-btn-primary" disabled={busy} onClick={() => act.mutate({ action: "use", i: ask })}>{t("library.yes")}</button>
        </div>
      )}
      {kind !== "complement" && (current || others.length > 0) && (
        <details className="tool-versions">
          <summary className="small">{t("library.versions", { count: others.length + (current ? 1 : 0) })}</summary>
          {current && row(current, here)}
          {others.map(({ i, x }) => row(i, x))}
        </details>
      )}
      {here && (
        <details className="tool-versions">
          <summary className="small">{t("library.moreActions")}</summary>
          <MoveTool tool={here} />
        </details>
      )}
      {forks.length > 0 && (
        <div style={{ display: "grid", gap: 6 }}>
          <h4 style={{ margin: 0 }}>{t("forks.title")}</h4>
          {forks.map(({ recipe: r, card: c }) => (
            <div key={r.id} style={{ display: "grid", gap: 4 }}>
              {r.recommendedFor.length > 0 && <span className="small muted">{t("forks.recommended", { games: r.recommendedFor.map((g) => g.game).join(", ") })}</span>}
              {c ? <button type="button" className="link-button" onClick={() => onPick(c.id)}><b>{c.name}</b></button> : <ForkInstall recipe={r} />}
            </div>
          ))}
        </div>
      )}
      {again.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("forks.queued")}</p>}
      {again.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(again.error)}</p>}
      {act.isSuccess && act.variables.action === "reinstall" &&<p className="small muted" role="status" style={{ margin: 0 }}>{t("library.queued")}</p>}
      {act.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(act.error)}</p>}
    </section>
  );
}

/** Instala la última versión de la receta de un emulador; devuelve el id de la tarea. */
export async function installRecipe(id: string) {
  const [r] = await request<RecipeRelease[]>(`/api/recipes/${encodeURIComponent(id)}/releases`);
  return request<number>("/api/emulators/install", post({ emulator: id, version: r.version, url: r.download }));
}

/** Un fork que aún no está instalado (#358): su nombre y «Instalar», que baja la última versión de su receta. */
export function ForkInstall({ recipe }: { recipe: RecipeSummary }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const install = useMutation({
    mutationFn: () => installRecipe(recipe.id),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  return (
    <div className="version-row">
      <span className="grow">{recipe.name}</span>
      <button className="ot-btn ot-btn-sm" type="button" disabled={!recipe.releases || install.isPending || install.isSuccess} onClick={() => install.mutate()}>{t("forks.install")}</button>
      {install.isSuccess && <span className="small muted" role="status">{t("forks.queued")}</span>}
      {install.isError && <span className="error small" role="alert">{describe(install.error)}</span>}
    </div>
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
