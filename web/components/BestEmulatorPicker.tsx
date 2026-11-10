"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useEffect, useRef, useState } from "react";
import { ShortPlatforms } from "@/components/Catalog";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { usePaged } from "@/lib/paged";
import { sagaQuery, sagasUrl } from "@/lib/queries";
import type { BestEmulatorGame, SagaPage } from "@/lib/types";

const FILTERS = ["owned", "following", "favorite", "all"] as const;
const gamesQuery = (sagaId: string) => ({ queryKey: ["saga", sagaId, "bestEmulator"], queryFn: () => request<BestEmulatorGame[]>(`/api/sagas/${sagaId}/best-emulator`) });

/**
 * «Buscar el mejor emulador» en los juegos que elijas (#374): sagas y, dentro de cada una, sus juegos. Nunca en todo el catálogo.
 * Lo ya buscado sale con su fecha y desmarcado; antes de encolar se ve cuántos juegos son. `sagaId` llega ya elegida.
 */
export function BestEmulatorButton({ sagaId }: { sagaId?: string }) {
  const t = useTranslations("catalog.bestEmulator");
  const dialog = useRef<HTMLDialogElement>(null);
  const [open, setOpen] = useState(false);
  useEffect(() => { if (open) dialog.current?.showModal(); }, [open]);
  return (
    <>
      <button className="ot-btn" type="button" onClick={() => setOpen(true)}>{t("open")}</button>
      <dialog ref={dialog} className="ot-dialog" aria-labelledby="best-emulator" onClose={() => setOpen(false)}>
        {open && <Picker sagaId={sagaId} onClose={() => dialog.current?.close()} />}
      </dialog>
    </>
  );
}

function Picker({ sagaId, onClose }: { sagaId?: string; onClose: () => void }) {
  const t = useTranslations("catalog.bestEmulator");
  const tf = useTranslations("catalog.sagas.filter");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [filter, setFilter] = useState<(typeof FILTERS)[number]>("owned");
  const sagas = usePaged(["sagas", filter, "", "name"], (offset) => sagasUrl(filter, "", "name", offset), (p: SagaPage) => ({ items: p.items, total: Number(p.total) }));
  // La saga desde la que se abrió va primero, aunque el filtro no la incluya.
  const current = useQuery({ ...sagaQuery(sagaId ?? ""), enabled: !!sagaId }).data?.saga;
  const rows = [...(current ? [current] : []), ...sagas.items.filter((s) => String(s.id) !== sagaId)];
  // Por saga elegida, sus juegos marcados.
  const [picked, setPicked] = useState<Record<string, number[]>>({});
  const count = Object.values(picked).reduce((n, ids) => n + ids.length, 0);

  const toggleSaga = async (id: string) => {
    if (picked[id]) return setPicked((p) => Object.fromEntries(Object.entries(p).filter(([k]) => k !== id)));
    const games = await cache.fetchQuery(gamesQuery(id));
    setPicked((p) => ({ ...p, [id]: games.filter((g) => !g.searchedAt).map((g) => Number(g.id)) }));
  };
  const toggleGame = (sid: string, gid: number) =>
    setPicked((p) => ({ ...p, [sid]: p[sid]?.includes(gid) ? p[sid].filter((x) => x !== gid) : [...(p[sid] ?? []), gid] }));
  useEffect(() => { if (sagaId && sagaId !== "0") void toggleSaga(sagaId); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const search = useMutation({
    mutationFn: () => request<number[]>("/api/best-emulator", { method: "POST", body: JSON.stringify({ gameIds: Object.values(picked).flat() }) }),
    onSuccess: () => { cache.invalidateQueries({ queryKey: ["tasks"] }); onClose(); },
  });

  return (
    <section className="settings-blocks">
      <h2 id="best-emulator" style={{ margin: 0 }}>{t("title")}</h2>
      <p className="small muted" style={{ margin: 0 }}>{t("help")}</p>
      <div className="chips" role="group" aria-label={t("filters")}>
        {FILTERS.map((f) => <button key={f} className="chip" type="button" aria-pressed={filter === f} onClick={() => setFilter(f)}>{tf(f)}</button>)}
      </div>
      {sagas.isError && <p className="error small" role="alert">{describe(sagas.error)}</p>}
      <div style={{ maxHeight: "50vh", overflow: "auto" }}>
      <ul style={{ listStyle: "none", margin: 0, padding: 0 }}>
        {rows.map((s) => {
          const id = String(s.id);
          return (
            <li key={id}>
              <label><input type="checkbox" checked={!!picked[id]} onChange={() => void toggleSaga(id)} /> <b>{s.name}</b></label>
              {picked[id] && <SagaGames sagaId={id} picked={picked[id]} onToggle={(gid) => toggleGame(id, gid)} />}
            </li>
          );
        })}
      </ul>
      <div ref={sagas.end} aria-hidden="true" />
      </div>
      {search.isError && <p className="error small" role="alert">{describe(search.error)}</p>}
      <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
        <span className="small muted" role="status">{t("count", { count })}</span>
        <span style={{ flex: 1 }} />
        <button className="ot-btn" type="button" onClick={onClose}>{t("cancel")}</button>
        <button className="ot-btn ot-btn-primary" type="button" disabled={count === 0 || search.isPending} onClick={() => search.mutate()}>{t("search", { count })}</button>
      </div>
    </section>
  );
}

function SagaGames({ sagaId, picked, onToggle }: { sagaId: string; picked: number[]; onToggle: (id: number) => void }) {
  const t = useTranslations("catalog.bestEmulator");
  const format = useFormatter();
  const games = useQuery(gamesQuery(sagaId));
  if (!games.data) return null;
  if (games.data.length === 0) return <p className="small muted" style={{ margin: "0 0 0 24px" }}>{t("noGames")}</p>;
  return (
    <ul style={{ listStyle: "none", margin: "0 0 0 24px", padding: 0 }}>
      {games.data.map((g) => (
        <li key={g.id} className="small">
          <label>
            <input type="checkbox" checked={picked.includes(Number(g.id))} onChange={() => onToggle(Number(g.id))} /> {g.name}
            {g.year != null && <span className="muted"> · {g.year}</span>} <span className="chips" style={{ gap: 4, display: "inline-flex" }}><ShortPlatforms ids={g.platforms} /></span>
            {g.searchedAt && <span className="muted"> · {t("searched", { date: format.dateTime(new Date(g.searchedAt), { dateStyle: "medium" }) })}</span>}
          </label>
        </li>
      ))}
    </ul>
  );
}
