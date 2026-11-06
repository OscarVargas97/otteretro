"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { ApiError, ProfileSettings } from "@/lib/types";

type Names = "resolution" | "renderer" | "aspectRatio" | "filter" | "vsync" | "speed" | "anisotropy" | "fps" | "antialiasing";

/**
 * Ajustes y mandos de un perfil (#276, decisión 298): los que describe la receta para la versión del emulador que usa, y el botón
 * que abre el emulador en ese juego para lo demás.
 */
export function ProfileSettingsForm({ profileId, name }: { profileId: string; name: string }) {
  const settings = useQuery({ queryKey: ["profileSettings", profileId], queryFn: () => request<ProfileSettings>(`/api/profiles/${profileId}/settings`) });
  if (!settings.data) return null;
  return <Form key={settings.data.version ?? ""} profileId={profileId} name={name} data={settings.data} />;
}

function Form({ profileId, name, data }: { profileId: string; name: string; data: ProfileSettings }) {
  const t = useTranslations("profiles.settings");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [version, setVersion] = useState(data.emulatorVersion ?? "");
  const [options, setOptions] = useState(() => Object.fromEntries(data.options.map((o) => [o.name, o.value ?? ""])));
  const [input, setInput] = useState(() => Object.fromEntries(data.input.map((i) => [i.name, i.value ?? ""])));
  const save = useMutation({
    mutationFn: () => request<void>(`/api/profiles/${profileId}/settings`, { method: "PUT", body: JSON.stringify({ options, input, emulatorVersion: version || null }) }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["profileSettings", profileId] }),
  });
  const open = useMutation({ mutationFn: () => request<ApiError>(`/api/profiles/${profileId}/open`, { method: "POST" }) });
  const id = (key: string) => `ps-${profileId}-${key}`;

  return (
    <form className="panel" style={{ gap: 10 }} aria-label={t("title", { name })}
      onSubmit={(e) => { e.preventDefault(); save.mutate(); }}>
      <b>{t("title", { name })}</b>
      {data.versions.length > 0 ? (
        <div className="sentence">
          <label className="small" htmlFor={id("version")}>{t("version")}</label>
          <select id={id("version")} value={version} onChange={(e) => setVersion(e.target.value)}>
            <option value="">{t("latest", { version: data.versions[data.versions.length - 1] })}</option>
            {data.versions.map((v) => <option key={v} value={v}>{v}</option>)}
          </select>
        </div>
      ) : <p className="small muted" style={{ margin: 0 }}>{t("notInstalled")}</p>}
      {data.undescribed && <p className="small error" role="status" style={{ margin: 0 }}>{t("undescribed", { version: data.version ?? "" })}</p>}
      {data.options.length === 0 ? <p className="small muted" style={{ margin: 0 }}>{t("noOptions")}</p> : (
        <div className="fields-row">
          {data.options.map((o) => (
            <div key={o.name} className="sentence">
              <label className="small" htmlFor={id(o.name)}>{t(`names.${o.name as Names}`)}</label>
              <input id={id(o.name)} value={options[o.name] ?? ""} maxLength={200}
                placeholder={o.current ? t("current", { value: o.current }) : t("emulatorValue")}
                onChange={(e) => setOptions({ ...options, [o.name]: e.target.value })} />
            </div>
          ))}
        </div>
      )}
      {data.kept.length > 0 && (
        <p className="small muted" style={{ margin: 0 }}>{t("kept", { names: data.kept.map((n) => t(`names.${n as Names}`)).join(", ") })}</p>
      )}
      {data.input.length > 0 && (
        <fieldset className="fields-row" style={{ border: 0, padding: 0, margin: 0 }}>
          <legend className="small">{t("input")}</legend>
          {data.input.map((i) => (
            <div key={i.name} className="sentence">
              <label className="small" htmlFor={id(i.name)}>{i.name.startsWith("-") ? t("inputArg") : i.name}</label>
              <select id={id(i.name)} value={input[i.name] ?? ""} onChange={(e) => setInput({ ...input, [i.name]: e.target.value })}>
                <option value="">{t("inputNone")}</option>
                {[...new Set([...data.inputProfiles, ...(i.value ? [i.value] : [])])].map((p) => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
          ))}
        </fieldset>
      )}
      <div className="sentence">
        <button className="ot-btn ot-btn-sm ot-btn-primary" type="submit" disabled={save.isPending}>{t("save")}</button>
        <button className="ot-btn ot-btn-sm" type="button" disabled={open.isPending} onClick={() => open.mutate()}>{t("openEmulator")}</button>
      </div>
      <p className="small muted" style={{ margin: 0 }}>{t("openHint")}</p>
      {save.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("saved")}</p>}
      {(save.error ?? open.error) && <p className="small error" role="alert" style={{ margin: 0 }}>{describe(save.error ?? open.error)}</p>}
      {open.data && <p className={`small${open.data.code === "settings.opened" ? " muted" : " error"}`} role="status" style={{ margin: 0 }}>{describe(open.data)}</p>}
    </form>
  );
}
