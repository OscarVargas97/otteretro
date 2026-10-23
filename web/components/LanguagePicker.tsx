"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { request } from "@/lib/client";
import type { AppState, LanguageRequest } from "@/lib/types";
import { CATALOGS, LOCALES, type Locale } from "@/messages";

/** Elegir el idioma: se guarda en config.json y la interfaz cambia sin reiniciar (#140). */
export function LanguagePicker() {
  const t = useTranslations("settings");
  const locale = useLocale();
  const cache = useQueryClient();
  const change = useMutation({
    mutationFn: (body: LanguageRequest) => request<AppState>("/api/language", { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: (state) => cache.setQueryData(["state"], state),
  });
  return (
    <label className="language-picker small muted">
      {t("language")}
      {/* Cada idioma se muestra con su propio nombre (languageName de su catálogo). */}
      <select value={locale} disabled={change.isPending} onChange={(e) => change.mutate({ language: e.target.value as Locale })}>
        {LOCALES.map((l) => (
          <option key={l} value={l}>{CATALOGS[l].languageName}</option>
        ))}
      </select>
    </label>
  );
}
