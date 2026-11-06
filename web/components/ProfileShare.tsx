"use client";

import { useMutation } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
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

/** «Cargar perfil»: instala el emulador del paquete y crea el perfil, en una tarea. */
export function LoadProfile() {
  const t = useTranslations("profiles");
  const describe = useDescribeError();
  const [path, setPath] = useState("");
  const load = useMutation({
    mutationFn: () => request<number>("/api/profiles/load", { method: "POST", body: JSON.stringify({ path: path.trim() }) }),
  });
  return (
    <form className="sentence" onSubmit={(e) => { e.preventDefault(); if (path.trim()) load.mutate(); }}>
      <input aria-label={t("loadPath")} placeholder={t("loadPath")} value={path} onChange={(e) => { setPath(e.target.value); load.reset(); }} />
      <button className="ot-btn ot-btn-sm" type="submit" disabled={!path.trim() || load.isPending}>{t("load")}</button>
      {load.isSuccess && <span className="small muted" role="status">{t("loaded")}</span>}
      {load.isError && <span className="error small" role="alert">{describe(load.error)}</span>}
    </form>
  );
}
