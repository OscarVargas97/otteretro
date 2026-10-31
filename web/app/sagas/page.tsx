"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { SagaTile, useConsoles } from "@/components/Catalog";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { SagaCard } from "@/lib/types";

const FILTERS = ["all", "favorite", "following", "iconic", "exclusive", "owned", "pc"] as const;
const COMPANIES = ["Nintendo", "Sony", "Microsoft"];
const SORTS = ["recognized", "name", "played", "activity"] as const;

/**
 * Sagas (§3.1): cuadrícula con filtros y orden. «Emulación» va siempre al final. El filtro y la plataforma (empresa o consola)
 * se combinan: «Exclusivas» + NES son las exclusivas de Nintendo que están en NES, y «Más reconocidas» ordena por lo de esa consola.
 */
export default function Sagas() {
  const t = useTranslations("catalog.sagas");
  const describe = useDescribeError();
  const consoles = useConsoles();
  const [filter, setFilter] = useState<string>("all");
  const [where, setWhere] = useState(""); // "", "company:Nintendo" o "console:nes"
  const [sort, setSort] = useState<(typeof SORTS)[number]>("recognized");
  const params = new URLSearchParams({ sort });
  if (filter !== "all") params.set("filter", filter);
  const [kind, value] = where.split(":");
  if (kind === "company") params.set("company", value);
  if (kind === "console") params.set("platform", value);
  const sagas = useQuery({ queryKey: ["sagas", filter, where, sort], queryFn: () => request<SagaCard[]>(`/api/sagas?${params}`), placeholderData: (previous) => previous });

  const chip = (id: string, label: string) => (
    <button key={id} className="chip" type="button" aria-pressed={filter === id} onClick={() => setFilter(id)}>{label}</button>
  );
  const nothingLoaded = filter === "all" && where === "" && sagas.data?.length === 0;

  return (
    <div className="content">
      {!nothingLoaded && (
        <div className="toolbar">
          <div className="chips" role="group" aria-label={t("filters")}>
            {FILTERS.map((f) => chip(f, t(`filter.${f}`)))}
          </div>
          <select className="ot-btn" aria-label={t("platform")} value={where} onChange={(e) => setWhere(e.target.value)}>
            <option value="">{t("allPlatforms")}</option>
            {COMPANIES.map((c) => <option key={c} value={`company:${c}`}>{c}</option>)}
            {consoles.data?.filter((c) => Number(c.games) > 0).map((c) => <option key={c.id} value={`console:${c.id}`}>{c.name}</option>)}
          </select>
          <label className="small muted toolbar-label" htmlFor="sort">
            {t("sortBy")}
            <select id="sort" className="ot-btn" value={sort} onChange={(e) => setSort(e.target.value as typeof sort)}>
              {SORTS.map((s) => <option key={s} value={s}>{t(`sort.${s}`)}</option>)}
            </select>
          </label>
        </div>
      )}
      {sagas.isError && <p className="error small" role="alert">{describe(sagas.error)}</p>}
      {nothingLoaded && <Empty title={t("emptyTitle")} text={t("emptyText")} />}
      {!nothingLoaded && sagas.data?.length === 0 && <p className="muted">{t("noMatch")}</p>}
      <div className="grid-cards">
        {sagas.data?.map((s) => <SagaTile key={s.id} saga={s} />)}
      </div>
    </div>
  );
}
