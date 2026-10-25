"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useState } from "react";
import { request } from "@/lib/client";
import { useDescribeError, useDynamicT } from "@/lib/i18n";
import type { CredentialRequest, CredentialStatus } from "@/lib/types";

const KEY = ["credentials"];

/** Un campo: si está guardado y, al editarlo, el campo para pegar el valor. Nunca se muestra el valor. */
function CredentialRow({ c }: { c: CredentialStatus }) {
  const t = useTranslations("credentials");
  const dt = useDynamicT();
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState("");
  const done = (list: CredentialStatus[]) => {
    cache.setQueryData(KEY, list);
    setEditing(false);
    setValue("");
  };
  const save = useMutation({
    mutationFn: (body: CredentialRequest) => request<CredentialStatus[]>(`/api/credentials/${c.id}`, { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: done,
  });
  const remove = useMutation({
    mutationFn: () => request<CredentialStatus[]>(`/api/credentials/${c.id}`, { method: "DELETE" }),
    onSuccess: done,
  });
  const label = dt(`credentials.field.${c.field}`);
  const inputId = `cred-${c.id}`;
  const error = save.error ?? remove.error;

  return (
    <div className="cred-row">
      <div className="version-row">
        <span>{label}</span>
        <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
          {c.saved ? (
            <span className="ot-badge ot-badge-have" title={c.updatedAt ? t("savedOn", { date: format.dateTime(new Date(c.updatedAt), { dateStyle: "medium" }) }) : undefined}>{t("saved")}</span>
          ) : (
            <span className="ot-badge">{t("missing")}</span>
          )}
          {!editing && (
            <>
              <button className="ot-btn" type="button" onClick={() => { setEditing(true); save.reset(); }} aria-label={`${c.saved ? t("change") : t("paste")}: ${label}`}>
                {c.saved ? t("change") : t("paste")}
              </button>
              {c.saved && (
                <button className="ot-btn ot-btn-ghost" type="button" disabled={remove.isPending} onClick={() => remove.mutate()} aria-label={`${t("delete")}: ${label}`}>
                  {t("delete")}
                </button>
              )}
            </>
          )}
        </div>
      </div>
      {editing && (
        <form className="form-block" onSubmit={(e) => { e.preventDefault(); save.mutate({ value }); }}>
          <label className="small muted" htmlFor={inputId}>{label}</label>
          <input id={inputId} type="password" autoComplete="off" spellCheck={false} value={value} onChange={(e) => setValue(e.target.value)} autoFocus />
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => { setEditing(false); setValue(""); }}>{t("cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="submit" disabled={!value.trim() || save.isPending}>{t("save")}</button>
          </div>
        </form>
      )}
      {error && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(error)}</p>}
    </div>
  );
}

/** Ajustes › Cuentas y claves (§5.6): cada servicio con sus campos, guardados cifrados en este equipo. */
export function CredentialsSettings() {
  const t = useTranslations("credentials");
  const dt = useDynamicT();
  const describe = useDescribeError();
  const list = useQuery({ queryKey: KEY, queryFn: () => request<CredentialStatus[]>("/api/credentials") });
  const open = useMutation({ mutationFn: (id: string) => request("/api/credentials/" + id + "/open", { method: "POST" }) });

  if (list.isError) return <div className="panel"><p className="error small" style={{ margin: 0 }}>{describe(list.error)}</p></div>;
  if (list.isPending) return <div className="panel"><p className="small muted" style={{ margin: 0 }}>{t("loading")}</p></div>;
  const services = [...new Set(list.data?.map((c) => c.service))];
  return (
    <div className="panel">
      {services.map((service) => {
        const fields = list.data!.filter((c) => c.service === service);
        return (
          <div key={service} className="cred-service">
            <div className="version-row">
              <h3 className="cred-name">{dt(`credentials.service.${service}`)}</h3>
              <button className="ot-btn ot-btn-ghost" type="button" title={fields[0].url} onClick={() => open.mutate(fields[0].id)}>{t("getIt")}</button>
            </div>
            {fields.map((c) => <CredentialRow key={c.id} c={c} />)}
          </div>
        );
      })}
      {open.error && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(open.error)}</p>}
      <p className="small muted" style={{ margin: 0 }}>{t("bitwarden")}</p>
    </div>
  );
}
