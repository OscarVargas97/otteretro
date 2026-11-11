"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { Fragment, useState } from "react";
import { useConsoleName } from "@/components/Catalog";
import { ProfileSettingsForm } from "@/components/ProfileSettings";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { LoadProfile, ShareProfile } from "@/components/ProfileShare";
import { emulatorsQuery, ForkInstall, recipesQuery } from "@/components/Tools";
import { useTasks } from "@/components/Tasks";
import type { BestEmulatorStatus, EmulatorPick, ProfileSwitch, RecipeSummary, VersionProfiles } from "@/lib/types";

type Shown = { console: string; recipe?: RecipeSummary; repo?: string | null; source?: string | null; origin: "manual" | "claude" | "catalog" };

const picksQuery = (gameId: number) => ({ queryKey: ["emulatorPicks", gameId], queryFn: () => request<EmulatorPick[]>(`/api/games/${gameId}/emulator-picks`) });

/**
 * Un recomendado por consola (#375): lo elegido a mano, lo que encontró Claude (los dos solo locales) o, si no hay, lo del catálogo
 * (las recetas que traen el juego en `recommendedFor`, #366).
 */
function useRecommended(gameId: number, consoles: string[]) {
  const recipes = useQuery(recipesQuery).data ?? [];
  const picks = useQuery(picksQuery(gameId)).data ?? [];
  return consoles.flatMap((console): Shown[] => {
    const pick = picks.find((p) => p.console === console);
    if (pick) return [{ console, recipe: recipes.find((r) => r.id === pick.recipeId), repo: pick.repo, source: pick.source, origin: pick.origin as Shown["origin"] }];
    const recipe = recipes.find((r) => r.consoles.includes(console) && r.recommendedFor.some((g) => Number(g.gameId) === gameId));
    return recipe ? [{ console, recipe, source: recipe.recommendedFor.find((g) => Number(g.gameId) === gameId)?.source, origin: "catalog" }] : [];
  });
}

/** Perfiles del juego (§8.0, #269): por versión del juego (decisión 301), cuál está activo, crear, renombrar, elegir ROM y activar. */
export function GameProfiles({ gameId, consoles }: { gameId: number; consoles: string[] }) {
  const t = useTranslations("profiles");
  const versions = useQuery({ queryKey: ["profiles", gameId], queryFn: () => request<VersionProfiles[]>(`/api/games/${gameId}/profiles`) });
  const recipes = useQuery(recipesQuery).data ?? [];
  // Solo las consolas del juego que corre algún emulador con receta: en las demás no hay nada que buscar ni elegir.
  const emulated = consoles.filter((c) => recipes.some((r) => r.kind === "emulator" && r.consoles.includes(c)));
  const recommended = useRecommended(gameId, emulated);
  if (!versions.data?.length && !emulated.length) return null;
  return (
    <section className="sec">
      <div className="sec-h"><h2>{t("title")}</h2></div>
      {!!emulated.length && <BestEmulatorSearch gameId={gameId} />}
      {emulated.map((c) => <Recommended key={c} gameId={gameId} console={c} shown={recommended.find((x) => x.console === c)} />)}
      {versions.data?.map((v) => <VersionPanel key={String(v.versionId)} gameId={gameId} version={v}
        recommended={recommended.flatMap((x) => (x.console === v.platform && x.recipe ? [x.recipe.id] : []))} />)}
      {!!versions.data?.length && <LoadProfile />}
    </section>
  );
}

/** «Buscar el mejor emulador» (#375): encola la búsqueda de Claude (#373) y muestra su avance o la fecha de la última. */
function BestEmulatorSearch({ gameId }: { gameId: number }) {
  const t = useTranslations("profiles.best");
  const tt = useTranslations("tasks.status");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const status = useQuery({ queryKey: ["bestEmulator", gameId], queryFn: () => request<BestEmulatorStatus>(`/api/games/${gameId}/best-emulator`) });
  const taskId = status.data?.task;
  const task = useTasks().find((x) => taskId != null && String(x.id) === String(taskId));
  const search = useMutation({
    mutationFn: () => request<number>(`/api/games/${gameId}/best-emulator`, { method: "POST" }),
    onSuccess: () => { cache.invalidateQueries({ queryKey: ["bestEmulator", gameId] }); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });
  const busy = taskId != null;
  return (
    <div className="sentence small">
      <button className="ot-btn ot-btn-sm" type="button" disabled={busy || search.isPending} onClick={() => search.mutate()}>{t("search")}</button>
      {busy
        ? <span role="status" className="ot-badge ot-badge-info">{task ? tt(task.status as "queued", { progress: Number(task.progress) }) : t("queued")}</span>
        : status.data?.searchedAt && <span className="muted">{t("searched", { date: format.dateTime(new Date(status.data.searchedAt), { dateStyle: "medium" }) })}</span>}
      {search.isError && <span className="error" role="alert">{describe(search.error)}</span>}
    </div>
  );
}

/**
 * El recomendado de una consola (#366, #375): de dónde sale, su fuente, «Instalar» si falta, «Elegir a mano» y, si es local,
 * «Volver al del catálogo». Sin receta muestra su repo y no se ofrece al crear un perfil.
 */
function Recommended({ gameId, console, shown }: { gameId: number; console: string; shown?: Shown }) {
  const t = useTranslations("profiles");
  const consoleName = useConsoleName();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const installs = useQuery(emulatorsQuery);
  const options = (useQuery(recipesQuery).data ?? []).filter((r) => r.kind === "emulator" && r.consoles.includes(console));
  const [choosing, setChoosing] = useState(false);
  const url = `/api/games/${gameId}/emulator-picks/${encodeURIComponent(console)}`;
  const change = useMutation({
    mutationFn: (recipeId: string | null) => request<EmulatorPick[]>(url, recipeId === null ? { method: "DELETE" } : { method: "PUT", body: JSON.stringify({ recipeId }) }),
    onSuccess: (picks) => { cache.setQueryData(["emulatorPicks", gameId], picks); setChoosing(false); },
  });
  const recipe = shown?.recipe;
  const source = shown?.source;
  const name = recipe?.name ?? shown?.repo;
  return (
    <div className="sentence small" role="group" aria-label={consoleName(console)}>
      <span>{consoleName(console)}:</span>
      {name ? <span>{t("recommended", { emulator: name })}</span> : <span className="muted">{t("best.none")}</span>}
      {shown && shown.origin !== "catalog" && <span className="ot-badge">{t(`best.origin.${shown.origin}`)}</span>}
      {!recipe && shown?.repo && <a href={shown.repo} target="_blank" rel="noreferrer">{t("best.repo")}</a>}
      {source && (source.startsWith("https://")
        ? <a href={source} target="_blank" rel="noreferrer">{t("recommendedSource", { source: new URL(source).hostname })}</a>
        : <span className="muted">{t("recommendedSource", { source })}</span>)}
      {recipe && installs.data && !installs.data.some((i) => i.emulatorId === recipe.id) && <ForkInstall recipe={recipe} />}
      {choosing
        ? (
          <select aria-label={t("best.chooseFor", { console: consoleName(console) })} defaultValue="" disabled={change.isPending}
            onChange={(e) => { if (e.target.value) change.mutate(e.target.value); }}>
            <option value="" disabled>{t("best.choose")}</option>
            {options.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
        )
        : <button className="ot-btn ot-btn-sm" type="button" onClick={() => setChoosing(true)}>{t("best.choose")}</button>}
      {shown && shown.origin !== "catalog" && (
        <button className="ot-btn ot-btn-sm" type="button" disabled={change.isPending} onClick={() => change.mutate(null)}>{t("best.reset")}</button>
      )}
      {change.isError && <span className="error" role="alert">{describe(change.error)}</span>}
    </div>
  );
}

function VersionPanel({ gameId, version: v, recommended }: { gameId: number; version: VersionProfiles; recommended: string[] }) {
  const t = useTranslations("profiles");
  const describe = useDescribeError();
  const consoleName = useConsoleName();
  const cache = useQueryClient();
  const refresh = () => cache.invalidateQueries({ queryKey: ["profiles", gameId] });
  const activate = useMutation({
    mutationFn: ({ profileId, saveUser }: { profileId: string | null; saveUser?: string }) =>
      request<ProfileSwitch>(`/api/versions/${v.versionId}/profile`, { method: "PUT", body: JSON.stringify({ profileId: profileId === null ? null : Number(profileId), saveUser: saveUser ?? null }) }),
    onSettled: refresh,
  });
  // Qué pasó con las partidas al cambiar de emulador (#270). Con varios usuarios de consola en Eden o Citron, se elige a cuál van.
  const saves = activate.data?.saves;
  const [user, setUser] = useState("");
  // El perfil cuyos ajustes se están viendo (#276), con su nombre.
  const [editing, setEditing] = useState<{ id: string; name: string } | null>(null);
  const settingsButton = (id: string, name: string) => (
    <button className="ot-btn ot-btn-sm" type="button" aria-expanded={editing?.id === id} aria-label={t("settings.open") + ": " + name}
      onClick={() => setEditing(editing?.id === id ? null : { id, name })}>
      {editing?.id === id ? t("settings.close") : t("settings.open")}
    </button>
  );
  const save = useMutation({
    mutationFn: ({ id, name, sourceId }: { id: string; name: string; sourceId: string | null }) =>
      request<void>(`/api/profiles/${id}`, { method: "PUT", body: JSON.stringify({ name, sourceId: sourceId === null ? null : Number(sourceId) }) }),
    onSettled: refresh,
  });
  // Borrar un perfil (#351), con confirmación: su configuración y mods; si era el activo, el juego vuelve al por defecto.
  const [deleting, setDeleting] = useState<string | null>(null);
  const remove = useMutation({
    mutationFn: (id: string) => request<void>(`/api/profiles/${id}`, { method: "DELETE" }),
    onSuccess: () => { setDeleting(null); setEditing(null); },
    onSettled: refresh,
  });
  const emulatorName = (id: string) => v.emulators.find((e) => e.id === id)?.name ?? id;
  const base = v.emulators.find((e) => e.base);
  const active = v.profiles.find((p) => p.active);
  const failure = activate.error ?? save.error ?? remove.error;
  const romName = (r: VersionProfiles["roms"][number]) => r.modified ? (r.label ?? r.path) : t("baseRom", { file: r.path });

  return (
    <div className="panel" style={{ gap: 10, marginBottom: 12 }}>
      <div className="sec-h">
        <b>{consoleName(v.platform)}</b>
        <span className="small muted" role="status">
          {active ? t("active", { name: active.name, emulator: emulatorName(active.emulatorId) })
            : base ? t("noneActive", { name: base.defaultName, emulator: base.name }) : t("noEmulator")}
        </span>
      </div>
      {failure && <p className="error small" role="alert">{describe(failure)}</p>}
      {saves?.code === "saves.chooseUser" && activate.variables ? (
        <form className="sentence" onSubmit={(e) => { e.preventDefault(); activate.mutate({ profileId: activate.variables!.profileId, saveUser: user || saves.users?.[0] }); }}>
          <label className="small" htmlFor={`user-${v.versionId}`}>{t("saves.chooseUser")}</label>
          <select id={`user-${v.versionId}`} value={user || saves.users?.[0]} onChange={(e) => setUser(e.target.value)}>
            {saves.users?.map((u) => <option key={u} value={u}>{u}</option>)}
          </select>
          <button className="ot-btn ot-btn-sm ot-btn-primary" type="submit" disabled={activate.isPending}>{t("saves.chooseUserConfirm")}</button>
        </form>
      ) : saves && (
        <p className={`small${saves.code === "saves.copied" || saves.code === "saves.none" ? " muted" : " error"}`} role="status" style={{ margin: 0 }}>
          {t(`saves.${saves.code.slice(6) as "copied"}`, { backup: saves.backup ?? "" })}
        </p>
      )}
      {v.emulators.length === 0 ? <p className="small muted" style={{ margin: 0 }}>{t("noEmulatorHint")}</p> : (
        <>
          <div className="table-wrap">
            <table className="t">
              <thead><tr><th>{t("name")}</th><th>{t("emulator")}</th><th>{t("rom")}</th><th><span className="sr-only">{t("activate")}</span></th></tr></thead>
              <tbody>
                {v.profiles.map((p) => (
                  <Fragment key={String(p.id)}>
                  <tr>
                    <td><NameField label={t("rename", { name: p.name })} value={p.name} onSave={(name) => save.mutate({ id: String(p.id), name, sourceId: p.sourceId === null ? null : String(p.sourceId) })} /></td>
                    <td>{emulatorName(p.emulatorId)}{p.emulatorVersion ? ` · ${p.emulatorVersion}` : ""}</td>
                    <td>
                      {v.roms.length > 1 ? (
                        <select aria-label={t("romOf", { name: p.name })} value={p.sourceId === null ? "" : String(p.sourceId)}
                          onChange={(e) => save.mutate({ id: String(p.id), name: p.name, sourceId: e.target.value || null })}>
                          <option value="">{t("baseRomDefault")}</option>
                          {v.roms.map((r) => <option key={String(r.id)} value={String(r.id)}>{romName(r)}</option>)}
                        </select>
                      ) : <span className="small muted">{t("baseRomDefault")}</span>}
                    </td>
                    <td className="sentence">
                      {p.active ? <span className="ot-badge ot-badge-have">{t("isActive")}</span>
                        : <button className="ot-btn ot-btn-sm" type="button" disabled={activate.isPending} onClick={() => activate.mutate({ profileId: String(p.id) })}>{t("activate")}</button>}
                      {settingsButton(String(p.id), p.name)}
                      {" "}<ShareProfile id={String(p.id)} name={p.name} />
                      {" "}<button className="ot-btn ot-btn-sm ot-btn-ghost" type="button" aria-label={t("deleteOf", { name: p.name })}
                        onClick={() => setDeleting(String(p.id))}>{t("delete")}</button>
                    </td>
                  </tr>
                  {deleting === String(p.id) && (
                    <tr>
                      <td colSpan={4}>
                        <span className="sentence">
                          <span className="small">{t("deleteAsk", { name: p.name })}</span>
                          <button className="ot-btn ot-btn-sm" type="button" onClick={() => setDeleting(null)}>{t("deleteKeep")}</button>
                          <button className="ot-btn ot-btn-sm ot-btn-danger" type="button" disabled={remove.isPending} onClick={() => remove.mutate(String(p.id))}>{t("deleteConfirm")}</button>
                        </span>
                      </td>
                    </tr>
                  )}
                  </Fragment>
                ))}
                {active && base && (
                  <tr>
                    <td colSpan={3} className="small muted">{t("useDefault", { name: base.defaultName, emulator: base.name })}</td>
                    <td><button className="ot-btn ot-btn-sm" type="button" disabled={activate.isPending} onClick={() => activate.mutate({ profileId: null })}>{t("activate")}</button></td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          {editing && <ProfileSettingsForm key={editing.id} profileId={editing.id} name={editing.name} />}
          {/* La clave vuelve a elegir el recomendado cuando llegan las recetas o se instala uno. */}
          <CreateProfile key={recommended.join() + v.emulators.map((e) => e.id).join()} version={v} recommended={recommended} onCreated={refresh} />
          <details className="fold-line">
            <summary className="small muted">{t("defaults")}</summary>
            {v.emulators.map((e) => (
              <div key={e.id} className="version-row small">
                <span>{e.name}{e.base ? ` · ${t("base")}` : ""}</span>
                <NameField label={t("rename", { name: e.defaultName })} value={e.defaultName}
                  onSave={(name) => save.mutate({ id: String(e.defaultProfileId), name, sourceId: null })} />
                {settingsButton(String(e.defaultProfileId), e.defaultName)}
              </div>
            ))}
          </details>
        </>
      )}
    </div>
  );
}

/** Un nombre que se guarda al salir del campo o con Enter. */
function NameField({ label, value, onSave }: { label: string; value: string; onSave: (name: string) => void }) {
  const [name, setName] = useState(value);
  const commit = () => { if (name.trim() && name !== value) onSave(name.trim()); else setName(value); };
  return <input aria-label={label} value={name} maxLength={80} onChange={(e) => setName(e.target.value)} onBlur={commit}
    onKeyDown={(e) => { if (e.key === "Enter") e.currentTarget.blur(); }} />;
}

/** Crear: vacío, con los valores del perfil por defecto o duplicando otro de esta versión del juego. */
function CreateProfile({ version: v, recommended, onCreated }: { version: VersionProfiles; recommended: string[]; onCreated: () => void }) {
  const t = useTranslations("profiles");
  const describe = useDescribeError();
  const [name, setName] = useState("");
  // El recomendado para el juego viene elegido si está instalado (#366).
  const [emulator, setEmulator] = useState(v.emulators.find((e) => recommended.includes(e.id))?.id ?? v.emulators[0]?.id ?? "");
  const [from, setFrom] = useState("empty");
  const create = useMutation({
    mutationFn: () => request<number>("/api/profiles", {
      method: "POST",
      body: JSON.stringify(from.startsWith("copy:")
        ? { versionId: Number(v.versionId), emulator, name, from: "copy", copy: Number(from.slice(5)) }
        : { versionId: Number(v.versionId), emulator, name, from }),
    }),
    onSuccess: () => { setName(""); setFrom("empty"); onCreated(); },
  });
  const copying = from.startsWith("copy:");
  return (
    <form className="sentence" onSubmit={(e) => { e.preventDefault(); if (name.trim()) create.mutate(); }}>
      <input aria-label={t("newName")} placeholder={t("newName")} value={name} maxLength={80} onChange={(e) => setName(e.target.value)} />
      {!copying && (
        <select aria-label={t("emulator")} value={emulator} onChange={(e) => setEmulator(e.target.value)}>
          {v.emulators.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}
        </select>
      )}
      <select aria-label={t("from")} value={from} onChange={(e) => setFrom(e.target.value)}>
        <option value="empty">{t("fromEmpty")}</option>
        <option value="default">{t("fromDefault")}</option>
        {v.profiles.map((p) => <option key={String(p.id)} value={`copy:${p.id}`}>{t("fromCopy", { name: p.name })}</option>)}
      </select>
      <button className="ot-btn ot-btn-sm ot-btn-primary" type="submit" disabled={!name.trim() || create.isPending}>{t("create")}</button>
      {create.isError && <span className="error small" role="alert">{describe(create.error)}</span>}
    </form>
  );
}
