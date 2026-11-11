"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

/** Revisar ya no es una pantalla (#415): sus archivos se revisan en Colección, con la lista abierta. */
export default function ReviewRedirect() {
  const router = useRouter();
  useEffect(() => router.replace("/collection/?review=1"), [router]);
  return null;
}
