// Toma las capturas del README desde una instalación limpia: configuración y carpeta temporales, el catálogo que trae la app y una
// biblioteca de ejemplo con archivos vacíos. Nunca toca los datos reales. Uso: `node scripts/capturas.mjs` con la app compilada.
import { chromium } from "@playwright/test";
import { spawn } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { DatabaseSync } from "node:sqlite";

const output = resolve("../docs/capturas");
const work = mkdtempSync(join(tmpdir(), "otteretro-capturas-"));
const data = join(work, "datos");
const config = join(work, "config.json");
writeFileSync(config, JSON.stringify({ Language: "es", capturarPortapapeles: false }));
mkdirSync(output, { recursive: true });

const exe = resolve("../src/Otteretro/bin/Debug/net10.0", process.platform === "win32" ? "Otteretro.exe" : "Otteretro");
const server = spawn(exe, ["servidor"], { env: { ...process.env, OTTERETRO_CONFIG: config } });
const url = await new Promise((done, fail) => {
  setTimeout(() => fail(new Error("El servidor no arrancó en 30 s")), 30_000).unref();
  server.stdout.on("data", (chunk) => {
    const match = /Abre (\S+)/.exec(chunk.toString());
    if (match) done(match[1]);
  });
});
const base = url.split("/?")[0];

const browser = await chromium.launch({ channel: "chrome" });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
  await page.goto(url);
  await page.waitForFunction(() => sessionStorage.getItem("otteretro.key"));
  const api = (method, path, body) => page.evaluate(async ([method, path, body]) => {
    const r = await fetch(path, { method, body: body ? JSON.stringify(body) : undefined,
      headers: { "Content-Type": "application/json", "X-Otteretro-Key": sessionStorage.getItem("otteretro.key") ?? "" } });
    return { ok: r.ok, status: r.status, text: await r.text() };
  }, [method, path, body]);
  const must = async (call) => {
    const r = await call;
    if (!r.ok) throw new Error(`${r.status}: ${r.text.slice(0, 300)}`);
    return r.text ? JSON.parse(r.text) : null;
  };

  // Lo mismo que elegir la carpeta en el asistente: tiene que dejar el catálogo listo, sin claves.
  const start = Date.now();
  await must(api("POST", "/api/data-folder", { path: data }));
  console.log(`Carpeta lista con el catálogo en ${Date.now() - start} ms`);

  const db = new DatabaseSync(join(data, "catalogo", "catalogo.db"), { readOnly: true });
  const one = (sql, ...args) => db.prepare(sql).get(...args);
  const games = one("SELECT count(*) AS n FROM game").n;
  const sagas = one("SELECT count(*) AS n FROM saga").n;
  if (games === 0) throw new Error("El catálogo quedó vacío: la copia inicial no corrió.");
  console.log(`${sagas} sagas y ${games} juegos, sin cargar nada`);

  // Una biblioteca de ejemplo: archivos vacíos con nombres reales.
  const disk = join(work, "Mis juegos", "Otteretro");
  await must(api("POST", "/api/disks", { path: disk, name: "Mis juegos" }));
  const consoleName = (id) => one("SELECT name FROM console WHERE id = ?", id).name;
  for (const [id, file] of [["gba", "Metroid Fusion (USA).gba"], ["gba", "The Legend of Zelda - The Minish Cap (USA).gba"], ["snes", "Chrono Trigger (USA).sfc"], ["snes", "Super Metroid (USA).sfc"], ["n64", "The Legend of Zelda - Ocarina of Time (USA).z64"]]) {
    const folder = join(disk, "Juegos", consoleName(id));
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, file), "");
  }
  await must(api("POST", "/api/collection/scan"));
  await page.waitForTimeout(4000);
  await must(api("POST", "/api/downloads/links", { text: "https://1fichier.com/?ejemplo00001\nhttps://sitio-de-ejemplo.test/Juego.part1.rar\nhttps://sitio-de-ejemplo.test/Juego.part2.rar\nhttps://mega.nz/file/ejemplo#clave" }));

  const saga = one("SELECT saga_id AS id FROM route ORDER BY id LIMIT 1").id;
  const game = one("SELECT id FROM game WHERE saga_id = ? AND iconic = 1 ORDER BY year LIMIT 1", saga)?.id ?? one("SELECT id FROM game WHERE saga_id = ? ORDER BY popularity DESC LIMIT 1", saga).id;
  db.close();

  // Se apaga durante el arranque para que nada de lo que tengas copiado entre a las capturas; la casilla se ve como la vería cualquiera.
  await must(api("PUT", "/api/downloads/clipboard", { enabled: true }));
  // La cola con datos de ejemplo: una descarga real no cabe en una prueba.
  const item = (part, size, check) => ({ url: `https://sitio-de-ejemplo.test/${part}`, name: `Juego de ejemplo.${part}.rar`, size, check });
  await page.route("**/api/downloads", (route) => route.fulfill({ json: [
    { id: 90001, name: "Juego de ejemplo", disk: "Mis juegos", links: 3, done: 1, status: "paused", progress: 52, detail: null, createdAt: "2026-11-01T00:00:00Z",
      items: [item("part1", 4294967296, "crc32"), item("part2", 4294967296, null), item("part3", 1073741824, null)] },
  ] }));

  const shots = [
    ["inicio", "/"],
    ["sagas", "/sagas/"],
    ["saga", `/saga/?id=${saga}`],
    ["juego", `/game/?id=${game}`],
    ["coleccion", "/collection/"],
    ["capturador", "/downloads/?tab=links"],
    ["descargas", "/downloads/?tab=queue"],
  ];
  for (const [name, path] of shots) {
    await page.goto(base + path);
    await page.waitForLoadState("networkidle").catch(() => {});
    await page.waitForTimeout(1500);
    if (name === "descargas") {
      await page.locator("details summary").first().click();
      await page.locator("details summary").last().click();
      await page.waitForTimeout(300);
    }
    await page.screenshot({ path: join(output, `${name}.png`) });
    console.log(`${name}.png`);
  }
} finally {
  await browser.close();
  const exited = new Promise((done) => server.once("exit", done));
  server.kill();
  await exited;
  rmSync(work, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
}
