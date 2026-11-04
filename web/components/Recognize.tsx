"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { optimistic } from "@/lib/optimistic";
import type { RecognizeStatus } from "@/lib/types";

/**
 * Almacenamiento › Reconocer juegos (#251, decisiones 280 a 282): si lo que ningún script reconoce va a Claude, sin horario,
 * y lo que eso gastó este mes.
 */
export function RecognizeSettings() {
  const t = useTranslations("libraries.recognize");
  const describe = useDescribeError();
  const format = useFormatter();
  const cache = useQueryClient();
  const status = useQuery({ queryKey: ["claude"], queryFn: () => request<RecognizeStatus>("/api/claude") });
  const save = useMutation({
    mutationFn: (useClaude: boolean) => request<RecognizeStatus>("/api/claude", { method: "PUT", body: JSON.stringify({ useClaude }) }),
    ...optimistic(cache, ["claude"], (d: RecognizeStatus, useClaude: boolean) => ({ ...d, useClaude })),
    onSuccess: (data) => cache.setQueryData(["claude"], data),
  });
  const s = status.data;
  return (
    <section className="panel">
      <h2>{t("title")}</h2>
      <p className="small muted" style={{ margin: 0 }}>{t("text")}</p>
      {status.isError && <p className="error small" role="alert">{describe(status.error)}</p>}
      {s && (
        <>
          <label className="sentence">
            <input type="checkbox" role="switch" checked={s.useClaude} onChange={(e) => save.mutate(e.target.checked)} />
            {t("useClaude")}
          </label>
          <p className="small" role="status" style={{ margin: 0 }}>
            {!s.useClaude ? t("off")
              : s.pausedUntil ? t("paused", { at: format.dateTime(new Date(s.pausedUntil), { weekday: "long", hour: "2-digit", minute: "2-digit" }) })
                : t("month", { calls: Number(s.monthCalls), cost: format.number(Number(s.monthCost), { style: "currency", currency: "USD", maximumFractionDigits: 2 }) })}
          </p>
        </>
      )}
      {save.error && <p className="error small" role="alert">{describe(save.error)}</p>}
    </section>
  );
}
