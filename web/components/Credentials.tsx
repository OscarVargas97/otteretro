"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { request } from "@/lib/client";
import { useDescribeError, useDynamicT } from "@/lib/i18n";
import type { CredentialRequest, CredentialStatus } from "@/lib/types";

const KEY = ["credentials"];

/** Fase en curso: arriba van los servicios que ya se usan; los de fases posteriores, plegados. Subirla al abrir cada fase. */
const CURRENT_PHASE = 1;

/** Un campo dentro del diálogo: si está guardado y, al editarlo, el campo para pegar el valor. Nunca se muestra el valor. */
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
    cache.invalidateQueries({ queryKey: ["notices"] });
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
        <div className="cred-actions">
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

/** Estado de un servicio: «Lista», «Falta 1 de 2» o «Falta». */
function ServiceStatus({ fields }: { fields: CredentialStatus[] }) {
  const t = useTranslations("credentials");
  const missing = fields.filter((f) => !f.saved).length;
  if (missing === 0) return <span className="ot-badge ot-badge-have">{t("ready")}</span>;
  return <span className="ot-badge">{missing === fields.length && fields.length === 1 ? t("missing") : t("partial", { missing, total: fields.length })}</span>;
}

/** Diálogo de un servicio: sus campos y dónde se consigue la clave. Esc (o B con mando) lo cierra. */
function ServiceDialog({ service, fields, onClose }: { service: string; fields: CredentialStatus[]; onClose: () => void }) {
  const t = useTranslations("credentials");
  const dt = useDynamicT();
  const describe = useDescribeError();
  const open = useMutation({ mutationFn: (id: string) => request("/api/credentials/" + id + "/open", { method: "POST" }) });
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);
  const name = dt(`credentials.service.${service}`);

  return (
    <div className="overlay" onClick={onClose}>
      <div className="dialog" role="dialog" aria-modal="true" aria-label={name} onClick={(e) => e.stopPropagation()}>
        <div className="version-row">
          <h3 className="cred-name">{name}</h3>
          <ServiceStatus fields={fields} />
        </div>
        <p className="small muted" style={{ margin: 0 }}>{dt(`credentials.purpose.${service}`)}</p>
        {fields.map((c) => <CredentialRow key={c.id} c={c} />)}
        {open.error && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(open.error)}</p>}
        <div className="dialog-actions">
          <button className="ot-btn ot-btn-ghost" type="button" title={fields[0].url} onClick={() => open.mutate(fields[0].id)}>{t("getIt")}</button>
          <button className="ot-btn" type="button" onClick={onClose}>{t("close")}</button>
        </div>
      </div>
    </div>
  );
}

function ServiceCard({ service, fields, onOpen }: { service: string; fields: CredentialStatus[]; onOpen: () => void }) {
  const t = useTranslations("credentials");
  const dt = useDynamicT();
  return (
    <div className="cred-card">
      <div className="version-row">
        <h3 className="cred-name">{dt(`credentials.service.${service}`)}</h3>
        <ServiceStatus fields={fields} />
      </div>
      <p className="small muted cred-purpose">{dt(`credentials.purpose.${service}`)}</p>
      <button className="ot-btn" type="button" onClick={onOpen} aria-label={`${t("configure")}: ${dt(`credentials.service.${service}`)}`}>{t("configure")}</button>
    </div>
  );
}

/** Ajustes › Cuentas y claves (§5.6, #57): una tarjeta por servicio; lo de fases posteriores, plegado. */
export function CredentialsSettings() {
  const t = useTranslations("credentials");
  const dt = useDynamicT();
  const describe = useDescribeError();
  const list = useQuery({ queryKey: KEY, queryFn: () => request<CredentialStatus[]>("/api/credentials") });
  const [openService, setOpenService] = useState<string | null>(null);

  if (list.isError) return <div className="panel"><p className="error small" style={{ margin: 0 }}>{describe(list.error)}</p></div>;
  if (list.isPending) return <div className="panel"><p className="small muted" style={{ margin: 0 }}>{t("loading")}</p></div>;
  const all = list.data ?? [];
  const services = [...new Set(all.map((c) => c.service))];
  const fieldsOf = (s: string) => all.filter((c) => c.service === s);
  const phaseOf = (s: string) => Number(fieldsOf(s)[0].phase);
  const now = services.filter((s) => phaseOf(s) <= CURRENT_PHASE);
  const later = services.filter((s) => phaseOf(s) > CURRENT_PHASE).sort((a, b) => phaseOf(a) - phaseOf(b));

  return (
    <div className="cred-settings">
      <h3 className="small muted cred-group">{t("now")}</h3>
      <div className="cred-grid">
        {now.map((s) => <ServiceCard key={s} service={s} fields={fieldsOf(s)} onOpen={() => setOpenService(s)} />)}
      </div>
      {later.length > 0 && (
        <details className="fold">
          <summary>{t("later", { count: later.length })} <span className="small muted">{later.map((s) => dt(`credentials.service.${s}`)).join(" · ")}</span></summary>
          <div className="fold-body">
            <p className="small muted" style={{ margin: 0 }}>{t("laterHint")}</p>
            {later.map((s) => (
              <div key={s} className="version-row">
                <span><b>{dt(`credentials.service.${s}`)}</b> <span className="small muted">· {dt(`credentials.purpose.${s}`)}</span></span>
                <span className="cred-actions">
                  <span className="ot-badge">{t("phase", { phase: phaseOf(s) })}</span>
                  <ServiceStatus fields={fieldsOf(s)} />
                  <button className="ot-btn ot-btn-ghost" type="button" onClick={() => setOpenService(s)} aria-label={`${t("configure")}: ${dt(`credentials.service.${s}`)}`}>{t("configure")}</button>
                </span>
              </div>
            ))}
          </div>
        </details>
      )}
      <p className="small muted" style={{ margin: 0 }}>{t("bitwarden")}</p>
      {openService && <ServiceDialog service={openService} fields={fieldsOf(openService)} onClose={() => setOpenService(null)} />}
    </div>
  );
}
