"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { SagaTile } from "@/components/Catalog";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { SagaCard } from "@/lib/types";

const FILTERS = ["all", "favorite", "following", "iconic", "owned", "pc"] as const;
const COMPANIES = ["Nintendo", "Sony", "Microsoft"];
const SORTS = ["iconic", "name", "played", "activity"] as const;

/** Sagas (§3.1): cuadrícula con filtros y orden. «Emulación» va siempre al final. */
export default function Sagas() {
  const t = useTranslations("catalog.sagas");
  const describe = useDescribeError();
  const [filter, setFilter] = useState<string>("all");
  const [sort, setSort] = useState<(typeof SORTS)[number]>("iconic");
  const params = new URLSearchParams({ sort });
  if (COMPANIES.includes(filter)) params.set("company", filter);
  else if (filter !== "all") params.set("filter", filter);
  const sagas = useQuery({ queryKey: ["sagas", filter, sort], queryFn: () => request<SagaCard[]>(`/api/sagas?${params}`), placeholderData: (previous) => previous });

  const chip = (id: string, label: string) => (
    <button key={id} className="chip" type="button" aria-pressed={filter === id} onClick={() => setFilter(id)}>{label}</button>
  );
  const nothingLoaded = filter === "all" && sagas.data?.length === 0;

  return (
    <div className="content">
      {!nothingLoaded && (
        <div className="toolbar">
          <div className="chips" role="group" aria-label={t("filters")}>
            {FILTERS.slice(0, 5).map((f) => chip(f, t(`filter.${f}`)))}
            {COMPANIES.map((c) => chip(c, c))}
            {chip("pc", t("filter.pc"))}
          </div>
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
