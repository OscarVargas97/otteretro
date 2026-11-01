/** Bytes a "812 GB" (o "1,4 GB" por debajo de 10) en el idioma activo. La API manda los long como número o texto. */
export function gb(bytes: number | string, locale: string): string {
  const value = Number(bytes) / 1024 ** 3;
  return `${value.toLocaleString(locale, { maximumFractionDigits: value < 10 ? 1 : 0 })} GB`;
}
