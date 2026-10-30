"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { ComingSoon } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDynamicT } from "@/lib/i18n";
import type { ApiError } from "@/lib/types";

/** Inicio: por ahora, los avisos (las claves que faltan, §2) y lo que llega después. */
export default function Home() {
  const t = useTranslations("notices");
  const dt = useDynamicT();
  const notices = useQuery({ queryKey: ["notices"], queryFn: () => request<ApiError[]>("/api/notices") });

  return (
    <>
      {notices.data?.map((n) => (
        <div key={n.code + (n.params?.service ?? "")} className="notice" role="note" aria-label={t("title")}>
          <span>{dt(n.code, { service: dt(`credentials.service.${n.params?.service}`) })}</span>
          <Link className="ot-btn" href="/settings/">{t("openSettings")}</Link>
        </div>
      ))}
      <ComingSoon screen="home" phase={2} />
    </>
  );
}
