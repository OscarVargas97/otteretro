// Recorre la app como lo haría la persona, en español y en inglés (#140 a #142).
// Las pruebas van en orden y comparten la misma pestaña: cada una sigue donde quedó la anterior.
import { expect, test, type Page } from "@playwright/test";
import { join } from "node:path";
import en from "../messages/en.json";
import es from "../messages/es.json";

const url = () => process.env.OTTERETRO_E2E_URL!;
const base = () => url().split("/?")[0];
const dataDir = (name: string) => join(process.env.OTTERETRO_E2E_DIR!, name);

test.describe.configure({ mode: "serial" });

let page: Page;
const problems: string[] = [];

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage();
  // Un texto que falta en el catálogo o un error de React aparece en la consola: cualquiera hace fallar la prueba.
  page.on("console", (m) => m.type() === "error" && !m.text().startsWith("Failed to load resource") && problems.push(m.text()));
  page.on("pageerror", (e) => problems.push(e.message));
  // Las respuestas con error se anotan con su dirección (la consola solo dice "Failed to load resource").
  // Las de /api/ con 4xx son errores esperados de las pruebas (por ejemplo, una ruta incompleta).
  page.on("response", (r) => {
    const path = new URL(r.url()).pathname;
    if (r.status() >= 400 && !(path.startsWith("/api/") && r.status() < 500)) problems.push(`${r.status()} ${path}`);
  });
});

test.afterEach(() => {
  expect(problems, "errores en la consola").toEqual([]);
});

/** Ninguna clave sin traducir a la vista (next-intl muestra la ruta de la clave, por ejemplo "nav.home"). */
async function expectNoRawKeys() {
  const text = await page.locator("body").innerText();
  expect(text).not.toMatch(/\b(nav|topBar|comingSoon|welcome|settings|dataFolder|update|errors|move|credentials|catalog|notices|review|home)\.[a-zA-Z]+/);
}

/** El buscador mide 320 px y su texto de ejemplo cabe entero (#20): se acorta, no se corta. */
async function expectSearchFits() {
  const search = page.locator(".search");
  expect((await search.boundingBox())?.width).toBe(320);
  const text = search.locator(".search-text");
  expect(await text.evaluate((e) => e.scrollWidth <= e.clientWidth), "el texto del buscador no cabe").toBe(true);
}

/** Ningún texto se sale de su botón, enlace o título (#20). Los cortes con «…» son a propósito y no cuentan. */
async function expectTextFits() {
  const overflowing = await page.locator("button, a, h1, h2, h3, .ot-badge").evaluateAll((els) =>
    els
      .filter((e) => {
        const s = getComputedStyle(e);
        return s.display !== "inline" && s.textOverflow !== "ellipsis" && (e.scrollWidth > e.clientWidth + 1 || e.scrollHeight > e.clientHeight + 1);
      })
      .map((e) => e.textContent?.trim()),
  );
  expect(overflowing, "textos que no caben").toEqual([]);
}

test("sin carpeta de datos, abre el asistente en español", async () => {
  await page.goto(url());
  await expect(page).toHaveURL(/\/welcome\//);
  await expect(page.getByRole("button", { name: es.welcome.start })).toBeVisible();
  await expect(page.getByText(es.welcome.steps.dataFolder)).toBeVisible();
  await expectNoRawKeys();
});

test("cambiar a inglés en el asistente cambia la interfaz al momento", async () => {
  await page.getByLabel(es.settings.language).selectOption("en");
  await expect(page.getByRole("button", { name: en.welcome.start })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await expectNoRawKeys();
  await page.screenshot({ path: "e2e-results/1-welcome-en.png" });
});

test("una ruta incompleta muestra el error del backend traducido", async () => {
  await page.getByRole("button", { name: en.welcome.start }).click();
  await expect(page.getByRole("heading", { name: en.welcome.chooseTitle })).toBeVisible();
  await page.getByLabel(en.welcome.otherPath).fill("relative\\folder");
  await page.getByRole("button", { name: /^Use / }).click();
  await expect(page.getByText(en.errors.path.notAbsolute)).toBeVisible();
  await page.screenshot({ path: "e2e-results/2-error-en.png" });
});

test("elegir la carpeta de datos lleva al inicio", async () => {
  await page.getByLabel(en.welcome.otherPath).fill(dataDir("data"));
  await page.getByRole("button", { name: /^Use / }).click();
  await expect(page).toHaveURL(`${base()}/`);
  await expect(page.getByRole("heading", { level: 1, name: en.nav.home })).toBeVisible();
  await expect(page.getByRole("heading", { level: 2, name: en.home.next })).toBeVisible();
  await expect(page.getByText(en.notices.missingKey.replace("{service}", en.credentials.service.igdb))).toBeVisible();
  await expectNoRawKeys();
  await expectSearchFits();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/3-home-en.png" });
});

test("todas las pantallas se ven en inglés", async () => {
  // Las del catálogo, sin nada cargado, dicen cómo cargarlo; las demás, en qué fase llegan.
  const screens = [
    ["sagas", en.catalog.sagas.emptyTitle],
    ["collection", en.catalog.collection.emptyTitle],
    ["game", en.catalog.game.noneTitle],
    ["saga", en.catalog.saga.noneTitle],
    ["review", en.review.pendingEmptyTitle],
    ...(["discover", "downloads", "tools", "steam"] as const).map((s) => [s, en.comingSoon[s].title] as const),
  ] as const;
  for (const [screen, title] of screens) {
    await page.goto(`${base()}/${screen}/`);
    await expect(page.getByRole("heading", { level: 1, name: en.nav[screen] })).toBeVisible();
    await expect(page.getByText(title)).toBeVisible();
    await expectNoRawKeys();
    await expectTextFits();
  }
});

test("el panel de tareas y la búsqueda están traducidos", async () => {
  // El botón no cambia de ancho con 0, 1 o varias tareas (#20): se prueba cambiando el número del contador.
  const tasks = page.getByRole("button", { name: en.topBar.tasks });
  const widths = await tasks.evaluate((b) => {
    const count = b.querySelector(".ot-count")!;
    const original = count.textContent;
    const w = ["0", "1", "8", "88"].map((n) => ((count.textContent = n), b.getBoundingClientRect().width));
    count.textContent = original;
    return w;
  });
  expect(new Set(widths).size, `anchos: ${widths.join(", ")}`).toBe(1);
  await tasks.click();
  await expect(page.getByText(en.topBar.noTasksTitle)).toBeVisible();
  await page.keyboard.press("Escape");
  await page.keyboard.press("Control+K");
  await expect(page.getByPlaceholder(en.topBar.searchPlaceholder)).toBeVisible();
  await expect(page.getByText(en.catalog.search.typeMore)).toBeVisible();
  await page.keyboard.type("zelda");
  await expect(page.getByText(en.catalog.search.noResults.replace("{query}", "zelda"))).toBeVisible();
  await page.keyboard.press("Escape");
});

test("Ajustes en inglés: carpeta, idioma y actualización", async () => {
  await page.goto(`${base()}/settings/`);
  await expect(page.getByRole("heading", { level: 1, name: en.nav.settings })).toBeVisible();
  await expect(page.getByText(dataDir("data"))).toBeVisible();
  // El botón no cambia de ancho mientras busca (#20).
  const check = page.getByRole("button", { name: en.update.check });
  const idle = (await check.boundingBox())!.width;
  // La región de estado ya existe antes de buscar: así el lector de pantalla anuncia lo que aparece adentro.
  await expect(page.locator('.version-row ~ [role="status"]')).toBeAttached();
  await check.click();
  await expect(check).toHaveAttribute("aria-busy", "true");
  await expect(page.getByRole("status").getByText(en.update.checkingDetail)).toBeVisible();
  expect((await check.boundingBox())!.width).toBe(idle);
  // Sin origen de actualizaciones: el motivo llega como código y se traduce.
  await expect(page.getByText(en.update.reason.noSource)).toBeVisible();
  expect((await check.boundingBox())!.width).toBe(idle);
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/4-settings-en.png", fullPage: true });
});

test("mover la carpeta de datos muestra el progreso y el resultado traducidos", async () => {
  await page.getByRole("button", { name: en.dataFolder.moveButton }).click();
  await page.getByLabel(en.dataFolder.folderLabel).fill(dataDir("moved"));
  await page.getByRole("button", { name: en.dataFolder.move, exact: true }).click();
  await expect(page.getByText(en.dataFolder.moved, { exact: true })).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText(dataDir("moved")).first()).toBeVisible();
  await expectNoRawKeys();
});

test("volver a español en Ajustes cambia todo y se mantiene al recargar", async () => {
  await page.getByLabel(en.settings.language).selectOption("es");
  await expect(page.getByRole("heading", { level: 1, name: es.nav.settings })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("heading", { level: 1, name: es.nav.settings })).toBeVisible();
  await expect(page.getByRole("link", { name: es.nav.collection })).toBeVisible();
  await expectNoRawKeys();
  await expectSearchFits();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/5-settings-es.png", fullPage: true });
});

test("Cuentas y claves: pegar una clave la deja guardada sin mostrarla, y borrarla la deja en falta", async () => {
  // Arriba, las tarjetas de lo que usa F1; lo de otras fases, plegado (#57).
  const steam = `${es.credentials.configure}: ${es.credentials.service.steam}`;
  await expect(page.getByRole("button", { name: steam })).toBeVisible();
  await expect(page.getByRole("button", { name: `${es.credentials.configure}: ${es.credentials.service.github}` })).toBeHidden();
  await page.getByRole("button", { name: steam }).click();
  const dialog = page.getByRole("dialog", { name: es.credentials.service.steam });
  const row = dialog.locator(".cred-row").filter({ hasText: es.credentials.field.apiKey });
  await expect(row.getByText(es.credentials.missing, { exact: true })).toBeVisible();
  await row.getByRole("button", { name: `${es.credentials.paste}: ${es.credentials.field.apiKey}` }).click();
  await row.getByLabel(es.credentials.field.apiKey).fill("clave-e2e-123");
  await row.getByRole("button", { name: es.credentials.save }).click();
  await expect(row.getByText(es.credentials.saved, { exact: true })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await page.reload();
  const card = page.locator(".cred-card").filter({ hasText: es.credentials.service.steam });
  await expect(card.getByText(es.credentials.ready, { exact: true })).toBeVisible();
  expect(await page.locator("body").innerText()).not.toContain("clave-e2e-123");
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/6-credentials-es.png", fullPage: true });
  await page.getByRole("button", { name: steam }).click();
  await row.getByRole("button", { name: `${es.credentials.delete}: ${es.credentials.field.apiKey}` }).click();
  await expect(row.getByText(es.credentials.missing, { exact: true })).toBeVisible();
  await page.screenshot({ path: "e2e-results/6b-credentials-dialog-es.png" });
  await page.getByRole("button", { name: es.credentials.close }).click();
});

test("con mando se recorre la app: foco, barra de botones, buscar con el teclado en pantalla y menú rápido", async ({ browser }) => {
  // Un mando de Xbox simulado: la app lo lee con navigator.getGamepads() en cada cuadro.
  const pad = await browser.newPage();
  await pad.addInitScript(() => {
    const state = {
      id: "Xbox Wireless Controller (STANDARD GAMEPAD Vendor: 045e)", index: 0, connected: true, mapping: "standard", timestamp: 0,
      axes: [0, 0, 0, 0], buttons: Array.from({ length: 17 }, () => ({ pressed: false, touched: false, value: 0 })),
    };
    (window as unknown as { pad: typeof state }).pad = state;
    navigator.getGamepads = () => [state as unknown as Gamepad];
  });
  const press = async (button: number) => {
    await pad.evaluate((b) => { (window as unknown as { pad: { buttons: { pressed: boolean }[] } }).pad.buttons[b].pressed = true; }, button);
    await pad.waitForTimeout(100);
    await pad.evaluate((b) => { (window as unknown as { pad: { buttons: { pressed: boolean }[] } }).pad.buttons[b].pressed = false; }, button);
    await pad.waitForTimeout(100);
  };
  const [A, B, X, , , , , , , MENU, , , , DOWN, , RIGHT, GUIDE] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

  await pad.goto(url());
  await expect(pad.getByRole("heading", { level: 1, name: es.nav.home })).toBeVisible();
  await press(DOWN);
  await expect(pad.locator("html")).toHaveAttribute("data-input", "pad");
  await expect(pad.locator(".ot-padbar")).toContainText(es.pad.choose);
  // Con la forma del mando conectado: A de Xbox para elegir.
  await expect(pad.locator(".ot-padbar .ot-glyph").first()).toHaveText("A");
  await expect(pad.locator(".is-focus")).toHaveCount(1);

  // X abre la búsqueda; A en el campo abre el teclado; se escribe «qw» y Menú lo deja listo.
  await press(X);
  const field = pad.locator("#search");
  await expect(field).toBeFocused();
  await press(A);
  await expect(pad.getByRole("dialog", { name: es.pad.keyboard })).toBeVisible();
  await press(A);
  await press(RIGHT);
  await press(A);
  await expect(pad.locator(".ot-osk-field")).toHaveText("qw");
  await press(MENU);
  await expect(pad.getByRole("dialog", { name: es.pad.keyboard })).toBeHidden();
  await expect(field).toHaveValue("qw");
  await press(B);
  await expect(field).toBeHidden();

  // Guía abre el menú rápido; B lo cierra.
  await press(GUIDE);
  await expect(pad.getByRole("dialog", { name: es.pad.quickMenu })).toBeVisible();
  await expect(pad.getByRole("button", { name: es.pad.goToSteam })).toBeFocused();
  await pad.screenshot({ path: "e2e-results/7-mando-menu-rapido.png" });
  await press(B);
  await expect(pad.getByRole("dialog", { name: es.pad.quickMenu })).toBeHidden();

  // Con el ratón, la interfaz vuelve a su forma normal.
  await pad.mouse.move(400, 400);
  await expect(pad.locator("html")).toHaveAttribute("data-input", "mouse");
  await expect(pad.locator(".ot-padbar")).toHaveCount(0);
  await pad.close();
});

test("catálogo con datos: sagas, favorita, ficha con seguimiento y corrección a mano, colección de PC y búsqueda", async () => {
  // Un catálogo pequeño, como el que dejaría una carga de IGDB y de Steam.
  const { DatabaseSync } = await import("node:sqlite");
  const db = new DatabaseSync(join(dataDir("moved"), "data", "otteretro.db"));
  db.exec(`
    INSERT INTO saga (id, name, iconic) VALUES (1, 'The Legend of Zelda', 1), (2, 'Half-Life', 0);
    INSERT INTO game (id, saga_id, name_es, original_name, year) VALUES
      (10, 1, 'Ocarina of Time', 'Ocarina of Time', 1998), (11, 1, 'Breath of the Wild', 'Breath of the Wild', 2017), (20, 2, 'Half-Life', 'Half-Life', 1998);
    INSERT INTO version (id, game_id, platform, kind, year, steam_app_id) VALUES
      (100, 10, 'n64', 'original', 1998, NULL), (101, 10, '3ds', 'remake', 2011, NULL), (110, 11, 'switch', 'original', 2017, NULL), (200, 20, 'pc', 'original', 1998, 70);
    INSERT INTO field_value (entity, entity_id, field, value, source, manual) VALUES ('game', 10, 'name_es', 'Ocarina of Time', 'igdb', 0);
    INSERT INTO source (kind, store, store_id, name, playtime, last_played, version_id) VALUES
      ('store', 'steam', '70', 'Half-Life', 600, '2026-10-01T00:00:00Z', 200), ('store', 'steam', '999', 'Algo que IGDB no tiene', 5, NULL, NULL);
  `);
  db.close();

  await page.goto(`${base()}/sagas/`);
  await page.getByRole("link", { name: /The Legend of Zelda/ }).click();
  await expect(page.getByRole("heading", { level: 2, name: "The Legend of Zelda" })).toBeVisible();
  const favorite = page.getByRole("button", { name: es.catalog.saga.favorite });
  await favorite.click();
  await expect(favorite).toHaveAttribute("aria-pressed", "true");
  await page.reload();
  await expect(page.getByRole("button", { name: es.catalog.saga.favorite })).toHaveAttribute("aria-pressed", "true");
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/8-saga-es.png", fullPage: true });

  await page.getByRole("link", { name: "Ocarina of Time" }).click();
  await expect(page.getByRole("heading", { level: 2, name: "Ocarina of Time" })).toBeVisible();
  await page.getByRole("radio", { name: es.catalog.status.finished }).click();
  await expect(page.getByLabel(es.catalog.game.finished)).toBeVisible();
  await page.getByLabel(es.catalog.game.rating).selectOption("9");
  await expect(page.getByText(es.catalog.game.saved)).toBeVisible();
  await page.reload();
  await expect(page.getByRole("radio", { name: es.catalog.status.finished })).toHaveAttribute("aria-checked", "true");
  await expect(page.getByLabel(es.catalog.game.rating)).toHaveValue("9");

  // Corregir el nombre a mano gana sobre IGDB; volver al de las fuentes lo deshace.
  await page.getByText(es.catalog.game.sources).click();
  await page.getByRole("button", { name: `${es.catalog.game.fix}: ${es.catalog.game.field.name_es}` }).click();
  await page.getByLabel(es.catalog.game.field.name_es).fill("La ocarina del tiempo");
  await page.getByRole("button", { name: es.catalog.game.save }).click();
  await expect(page.getByRole("heading", { level: 2, name: "La ocarina del tiempo" })).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/9-ficha-es.png", fullPage: true });
  await page.getByRole("button", { name: es.catalog.game.undoFix }).click();
  await expect(page.getByRole("heading", { level: 2, name: "Ocarina of Time" })).toBeVisible();

  await page.goto(`${base()}/collection/?console=pc`);
  await expect(page.getByRole("link", { name: /Half-Life/ })).toBeVisible();
  await expect(page.getByText("Algo que IGDB no tiene")).toBeVisible();
  await page.getByRole("button", { name: es.catalog.collection.list }).click();
  await expect(page.getByRole("cell", { name: "10 h" })).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/10-coleccion-pc-es.png", fullPage: true });

  await page.keyboard.press("Control+K");
  await page.keyboard.type("zelda");
  await expect(page.getByRole("link", { name: /The Legend of Zelda/ })).toBeVisible();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/saga\/\?id=1/);
});

test("Claude: aprobar en Revisar, la ruta en la saga, la comparativa en la ficha y el siguiente juego en Inicio", async () => {
  // Lo que dejaría `claude cargar` (F2.5): las rutas de Zelda y la mejor versión de Ocarina, por aprobar.
  const { DatabaseSync } = await import("node:sqlite");
  const db = new DatabaseSync(join(dataDir("moved"), "data", "otteretro.db"));
  const criterion = { score: 4, text: "Bien" };
  const items = [
    ["saga", 1, { prompt: "ruta-saga", result: [
      { kind: "release", rationale: "Por fecha de salida.", steps: [{ gameId: 10, versionId: null, reason: "El primero en 3D" }, { gameId: 11, versionId: null, reason: "El último" }], sources: [] },
      { kind: "community", rationale: "Así se recomienda empezar.", steps: [{ gameId: 10, versionId: 101, reason: "El de 3DS se ve mejor" }, { gameId: 11, versionId: null, reason: "Después, el mundo abierto" }], sources: [{ title: "r/zelda", url: "https://reddit.com/r/zelda" }] },
    ] }],
    ["game", 10, { prompt: "mejor-version", result: { gameId: 10, bestVersionId: 101, reason: "Mejor hoy", versions: [
      { versionId: 100, content: criterion, gameplay: { score: 3, text: "Controles de su época" }, technical: criterion, original: { score: 5, text: "Es el original" } },
      { versionId: 101, content: criterion, gameplay: { score: 5, text: "Pantalla táctil para el inventario" }, technical: criterion, original: criterion },
    ] } }],
  ] as const;
  for (const [entity, id, data] of items)
    db.prepare("INSERT INTO review_item (kind, entity, entity_id, data, created_at) VALUES ('pending_approval', ?, ?, ?, '2026-10-31')").run(entity, id, JSON.stringify(data));
  db.close();

  await page.goto(`${base()}/review/`);
  await expect(page.getByText("Así se recomienda empezar.")).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/11-revisar-es.png", fullPage: true });
  for (let i = 0; i < 2; i++) await page.getByRole("button", { name: es.review.approve }).first().click();
  await expect(page.getByText(es.review.pendingEmptyTitle)).toBeVisible();

  await page.goto(`${base()}/saga/?id=1`);
  await expect(page.getByText(es.catalog.saga.why)).toBeVisible();
  await expect(page.getByText("El de 3DS se ve mejor")).toBeVisible();
  await page.getByRole("button", { name: es.catalog.route.community }).click(); // elegirla es seguirla
  await expect(page.getByRole("button", { name: es.catalog.route.community })).toHaveAttribute("aria-pressed", "true");
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/12-saga-ruta-es.png", fullPage: true });

  await page.goto(`${base()}/game/?id=10`);
  await expect(page.getByRole("heading", { level: 2, name: es.catalog.game.compare })).toBeVisible();
  await expect(page.getByText("Pantalla táctil para el inventario")).toBeVisible();
  await page.screenshot({ path: "e2e-results/13-comparativa-es.png", fullPage: true });

  // Ocarina ya está terminado (prueba anterior): lo siguiente es Breath of the Wild.
  await page.goto(`${base()}/`);
  await expect(page.getByRole("link", { name: /Breath of the Wild/ })).toBeVisible();
  await page.screenshot({ path: "e2e-results/14-inicio-es.png", fullPage: true });

  await page.goto(`${base()}/settings/`);
  await expect(page.getByRole("button", { name: es.settings.claude.runNow })).toBeVisible();
  await expectNoRawKeys();
});
