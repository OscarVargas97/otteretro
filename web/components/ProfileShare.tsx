"use client";

import { useMutation } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useRef, useState } from "react";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";

/** «Compartir» un perfil (#282): guarda su .zip en una ruta de este equipo (el servidor es local). */
export function ShareProfile({ id, name }: { id: number | string; name: string }) {
  const t = useTranslations("profiles");
  const describe = useDescribeError();
  const [open, setOpen] = useState(false);
  const [path, setPath] = useState("");
  const share = useMutation({
    mutationFn: () => request<void>(`/api/profiles/${id}/share`, { method: "POST", body: JSON.stringify({ path: path.trim() }) }),
  });
  if (!open) return <button className="ot-btn ot-btn-sm" type="button" aria-label={t("shareOf", { name })} onClick={() => setOpen(true)}>{t("share")}</button>;
  return (
    <form className="sentence" onSubmit={(e) => { e.preventDefault(); if (path.trim()) share.mutate(); }}>
      <input aria-label={t("sharePath", { name })} placeholder={t("sharePlaceholder")} value={path} onChange={(e) => { setPath(e.target.value); share.reset(); }} />
      <button className="ot-btn ot-btn-sm ot-btn-primary" type="submit" disabled={!path.trim() || share.isPending}>{t("shareConfirm")}</button>
      {share.isSuccess && <span className="small muted" role="status">{t("shared", { path: path.trim() })}</span>}
      {share.isError && <span className="error small" role="alert">{describe(share.error)}</span>}
    </form>
  );
}

/** «Cargar un perfil (.zip)» (#330): elige el archivo; instala el emulador del paquete y crea el perfil, en una tarea. */
export function LoadProfile() {
  const t = useTranslations("profiles");
  const describe = useDescribeError();
  const input = useRef<HTMLInputElement>(null);
  const load = useMutation({
    mutationFn: (file: File) => request<number>("/api/profiles/load-file", { method: "POST", body: file, headers: { "Content-Type": "application/octet-stream" } }),
    onSettled: () => { if (input.current) input.current.value = ""; },
  });
  return (
    <div className="sentence">
      <button className="ot-btn ot-btn-sm" type="button" disabled={load.isPending} onClick={() => input.current?.click()}>{t("loadZip")}</button>
      <input ref={input} type="file" accept=".zip" hidden aria-label={t("loadZip")} onChange={(e) => { const f = e.target.files?.[0]; if (f) load.mutate(f); }} />
      {load.isSuccess && <span className="small muted" role="status">{t("loaded")}</span>}
      {load.isError && <span className="error small" role="alert">{describe(load.error)}</span>}
    </div>
  );
}
