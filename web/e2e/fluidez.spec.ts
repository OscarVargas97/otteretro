// Fluidez primero (#173, #104): con un catálogo del tamaño del real, cada pantalla muestra su marco en menos de 100 ms
// desde el clic, sus datos en menos de 300 ms (marco más una consulta) y nada se mueve al llegar (desplazamiento < 0,01). Corre después de app.spec.ts (orden alfabético), sobre la misma carpeta de datos ya preparada.
import { expect, test, type Page } from "@playwright/test";
import { writeFileSync } from "node:fs";
import { join } from "node:path";

const FRAME_MS = 100;
const DATA_MS = 300;
const SHIFT = 0.01;

/** Las que se pasan del presupuesto. #105 dejó todas dentro; ninguna nueva puede sumarse. */
const OVER_BUDGET = new Set<string>([]);

test.describe.configure({ mode: "serial" });
let page: Page;

test.beforeAll(async ({ browser }) => {
  const { DatabaseSync } = await import("node:sqlite");
  const moved = join(process.env.OTTERETRO_E2E_DIR!, "moved");
  const db = new DatabaseSync(join(moved, "data", "otteretro.db"));
  db.exec(`ATTACH DATABASE '${join(moved, "catalogo", "catalogo.db").replaceAll("'", "''")}' AS catalog`);
  // Como FluidityTests: 5.800 sagas, 50.000 juegos (30.000 sin saga) y dos o tres versiones por juego, con ids que no chocan.
  db.exec(`
    WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 5800)
    INSERT INTO saga (id, name, iconic) SELECT 100000 + i, 'Saga grande ' || i, i % 50 = 0 FROM n;
    WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 50000)
    INSERT INTO game (id, saga_id, name_es, original_name, year, igdb_id, popularity)
    SELECT 1000000 + i, CASE WHEN i <= 400 THEN 100001 WHEN i <= 20000 THEN 100002 + i % 5799 END,
           'Juego grande ' || i, 'Big game ' || i, 1980 + i % 45, 1000000 + i, i % 997 FROM n;
    CREATE TEMP TABLE p AS SELECT id, row_number() OVER (ORDER BY rowid) - 1 AS k FROM console;
    INSERT INTO version (game_id, platform, kind, year, igdb_id)
    SELECT g.id, p.id, 'original', g.year, g.id FROM game g JOIN p ON p.k = coalesce(g.saga_id, g.id) % (SELECT count(*) FROM p) WHERE g.id > 1000000;
    INSERT INTO version (game_id, platform, kind, year, igdb_id)
    SELECT g.id, 'pc', 'original', g.year, g.id FROM game g
    WHERE g.id > 1000000 AND g.id % 3 = 0 AND NOT EXISTS (SELECT 1 FROM version v WHERE v.game_id = g.id AND v.platform = 'pc');
  `);
  db.close();
  page = await browser.newPage();
  // Desplazamientos del contenido (CLS) desde la última medición.
  await page.addInitScript(() => {
    const w = window as unknown as { __cls: number };
    w.__cls = 0;
    new PerformanceObserver((list) => {
      for (const e of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) if (!e.hadRecentInput) w.__cls += e.value;
    }).observe({ type: "layout-shift", buffered: true });
  });
});

type Result = { frame: number; data: number; cls: number };

/** Hace clic en `link` y mide, dentro de la página, cuándo aparece un `frame` nuevo y cuándo sus datos (`data`). */
function measure(link: string, frame: string, data: string): Promise<Result> {
  return page.evaluate(({ link, frame, data }) => new Promise<Result>((resolve) => {
    const w = window as unknown as { __cls: number };
    const old = document.querySelector(frame);
    const target = document.querySelector<HTMLElement>(link);
    if (!target) throw new Error(`No hay ${link}`);
    w.__cls = 0;
    const t0 = performance.now();
    let shown = -1;
    target.click();
    const tick = () => {
      const now = performance.now() - t0;
      const el = document.querySelector(frame);
      if (shown < 0 && el && el !== old) shown = now;
      if (shown >= 0 && document.querySelector(data)) return resolve({ frame: shown, data: now, cls: w.__cls });
      if (now > 10_000) return resolve({ frame: shown, data: -1, cls: w.__cls });
      requestAnimationFrame(tick);
    };
    tick();
  }), { link, frame, data });
}

test("con un catálogo grande, cada pantalla aparece al instante y no salta", async () => {
  test.setTimeout(120_000);
  // Al abrir la app: cuándo aparece el menú lateral (solo se informa; lo exige #105).
  const start = Date.now();
  await page.goto(process.env.OTTERETRO_E2E_URL!);
  await page.locator(".sidebar").waitFor();
  const opening = Date.now() - start;

  const content = ".content";
  const panel = "#settings-panel > :last-child";
  const steps: [string, string, string, string][] = [
    ["Sagas", '.sidebar a[href="/sagas/"]', content, "a.ot-saga"],
    ["Una saga", "a.ot-saga", content, 'table a[href^="/game/"]'],
    ["Un juego", 'table a[href^="/game/"]', content, ".version-row"],
    ["Colección", '.sidebar a[href="/collection/"]', content, ".collection-consoles button"],
    ["Revisar", '.sidebar a[href="/review/"]', content, '.content [role="tablist"]'],
    ["Inicio", '.sidebar a[href="/"]', content, ".content .sec"],
    ["Ajustes", '.sidebar a[href="/settings/"]', content, "#settings-panel .version-row"],
    ["Ajustes › Cuentas y claves", "#tab-credentials", panel, "#settings-panel .cred-line"],
    ["Ajustes › Consolas", "#tab-consoles", panel, '#settings-panel input[type="checkbox"]'],
    ["Ajustes › Claude", "#tab-claude", panel, '#settings-panel input[type="time"]'],
    ["Ajustes › General", "#tab-general", panel, "#settings-panel .version-row"],
  ];
  const lines = [`Abrir la app (menú lateral): ${opening} ms`];
  const slow: string[] = [];
  for (const [name, link, frame, data] of steps) {
    await page.locator(link).first().waitFor();
    const r = await measure(link, frame, data);
    lines.push(`${name}: marco ${Math.round(r.frame)} ms · datos ${Math.round(r.data)} ms · desplazamiento ${r.cls.toFixed(3)}`);
    expect(r.data, `${name}: los datos no llegaron`).toBeGreaterThanOrEqual(0);
    if (r.frame > FRAME_MS) slow.push(`marco: ${name}`);
    if (r.data > DATA_MS) slow.push(`datos: ${name}`);
    if (r.cls > SHIFT) slow.push(`desplazamiento: ${name}`);
  }
  writeFileSync("e2e-results/fluidez.txt", lines.join("\n") + "\n");
  console.log(lines.join("\n"));
  for (const done of [...OVER_BUDGET].filter((x) => !slow.includes(x))) console.log(`Ya cumple, sácala de OVER_BUDGET: ${done}`);
  expect(slow.filter((x) => !OVER_BUDGET.has(x)), `pasan del presupuesto (marco ${FRAME_MS} ms, datos ${DATA_MS} ms, desplazamiento ${SHIFT})`).toEqual([]);
});
