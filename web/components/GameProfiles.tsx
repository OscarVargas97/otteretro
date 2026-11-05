"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useConsoleName } from "@/components/Catalog";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { VersionProfiles } from "@/lib/types";

/** Perfiles del juego (§8.0, #269): por versión del juego (decisión 301), cuál está activo, crear, renombrar, elegir ROM y activar. */
export function GameProfiles({ gameId }: { gameId: number }) {
  const t = useTranslations("profiles");
  const versions = useQuery({ queryKey: ["profiles", gameId], queryFn: () => request<VersionProfiles[]>(`/api/games/${gameId}/profiles`) });
  if (!versions.data?.length) return null;
  return (
    <section className="sec">
      <div className="sec-h"><h2>{t("title")}</h2></div>
      {versions.data.map((v) => <VersionPanel key={String(v.versionId)} gameId={gameId} version={v} />)}
    </section>
  );
}

function VersionPanel({ gameId, version: v }: { gameId: number; version: VersionProfiles }) {
  const t = useTranslations("profiles");
  const describe = useDescribeError();
  const consoleName = useConsoleName();
  const cache = useQueryClient();
  const refresh = () => cache.invalidateQueries({ queryKey: ["profiles", gameId] });
  const activate = useMutation({
    mutationFn: (profileId: string | null) => request<void>(`/api/versions/${v.versionId}/profile`, { method: "PUT", body: JSON.stringify({ profileId: profileId === null ? null : Number(profileId) }) }),
    onSettled: refresh,
  });
  const save = useMutation({
    mutationFn: ({ id, name, sourceId }: { id: string; name: string; sourceId: string | null }) =>
      request<void>(`/api/profiles/${id}`, { method: "PUT", body: JSON.stringify({ name, sourceId: sourceId === null ? null : Number(sourceId) }) }),
    onSettled: refresh,
  });
  const emulatorName = (id: string) => v.emulators.find((e) => e.id === id)?.name ?? id;
  const base = v.emulators.find((e) => e.base);
  const active = v.profiles.find((p) => p.active);
  const failure = activate.error ?? save.error;
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
      {v.emulators.length === 0 ? <p className="small muted" style={{ margin: 0 }}>{t("noEmulatorHint")}</p> : (
        <>
          <div className="table-wrap">
            <table className="t">
              <thead><tr><th>{t("name")}</th><th>{t("emulator")}</th><th>{t("rom")}</th><th><span className="sr-only">{t("activate")}</span></th></tr></thead>
              <tbody>
                {v.profiles.map((p) => (
                  <tr key={String(p.id)}>
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
                    <td>
                      {p.active ? <span className="ot-badge ot-badge-have">{t("isActive")}</span>
                        : <button className="ot-btn ot-btn-sm" type="button" disabled={activate.isPending} onClick={() => activate.mutate(String(p.id))}>{t("activate")}</button>}
                    </td>
                  </tr>
                ))}
                {active && base && (
                  <tr>
                    <td colSpan={3} className="small muted">{t("useDefault", { name: base.defaultName, emulator: base.name })}</td>
                    <td><button className="ot-btn ot-btn-sm" type="button" disabled={activate.isPending} onClick={() => activate.mutate(null)}>{t("activate")}</button></td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <CreateProfile version={v} onCreated={refresh} />
          <details className="fold-line">
            <summary className="small muted">{t("defaults")}</summary>
            {v.emulators.map((e) => (
              <div key={e.id} className="version-row small">
                <span>{e.name}{e.base ? ` · ${t("base")}` : ""}</span>
                <NameField label={t("rename", { name: e.defaultName })} value={e.defaultName}
                  onSave={(name) => save.mutate({ id: String(e.defaultProfileId), name, sourceId: null })} />
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
function CreateProfile({ version: v, onCreated }: { version: VersionProfiles; onCreated: () => void }) {
  const t = useTranslations("profiles");
  const describe = useDescribeError();
  const [name, setName] = useState("");
  const [emulator, setEmulator] = useState(v.emulators[0]?.id ?? "");
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
