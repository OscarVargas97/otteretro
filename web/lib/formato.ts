/** Bytes a "812 GB" (o "1,4 GB" por debajo de 10). La API manda los long como número o texto. */
export function gb(bytes: number | string): string {
  const valor = Number(bytes) / 1024 ** 3;
  return `${valor.toLocaleString("es", { maximumFractionDigits: valor < 10 ? 1 : 0 })} GB`;
}
