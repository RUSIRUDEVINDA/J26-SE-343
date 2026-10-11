import assert from "node:assert/strict";
import path from "node:path";
import fs from "node:fs/promises";
import { tmpdir } from "node:os";
import { createRequire } from "node:module";
const loadCommonJs = createRequire(import.meta.url);
const { chromium } = loadCommonJs(process.env.C4_PLAYWRIGHT_MODULE || "playwright");
const output = process.env.C4_SCREENSHOT_DIR || path.join(tmpdir(), "c4-frontend-verification");
(async () => {
  await fs.mkdir(output, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1728, height: 1117 } });
  const errors = [];
  const requests = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
  page.on("request", request => { if (/\/api\/|:8000|:8001/.test(request.url())) requests.push(request.url()); });
  const base = "http://localhost:3000";
  const routes = ["", "/cases", "/cases/compliance", "/cases/conflicts", "/complaints", "/anomalies", "/screening", "/risk", "/explain", "/consensus", "/conditions", "/timing", "/commissioner", "/commissioner/review", "/summary", "/audit", "/verification"];
  for (const route of routes) {
    const response = await page.goto(base + "/component-04" + route);
    assert.equal(response.status(), 200, route);
    await page.getByRole("heading", { level: 1 }).waitFor();
    await page.locator("img").first().waitFor();
    await page.waitForFunction(() => [...document.images].every(image => image.complete && image.naturalWidth > 0));
    assert.equal(await page.locator("img").evaluateAll(images => images.every(image => image.getBoundingClientRect().width > 0 && image.getBoundingClientRect().height > 0)), true, route + " visible assets");
    assert.equal(await page.locator("main").count(), 1, route + " has exactly one main");
    await page.getByText("Loading", { exact: false }).count(); // Loading is allowed while the read settles.
    if (route === "" || route === "/cases") await page.getByText("Live governance assessments are not available yet.", { exact: false }).waitFor();
    assert.equal(await page.getByText("DEMO DATA", { exact: true }).count(), 0, "live mode starts without demo fixtures");
    await page.screenshot({ path: path.join(output, (route.slice(1).replaceAll("/", "-") || "dashboard") + "-live-desktop.png"), fullPage: true });
    await page.getByRole("button", { name: "Explore demo data" }).click();
    await page.getByText("DEMO DATA", { exact: true }).waitFor();
    if (route === "" || route === "/cases") await page.getByText("DEMO-C4-005", { exact: true }).waitFor();
    await page.screenshot({ path: path.join(output, (route.slice(1).replaceAll("/", "-") || "dashboard") + "-demo-desktop.png"), fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true, route + " mobile page overflow");
    await page.screenshot({ path: path.join(output, (route.slice(1).replaceAll("/", "-") || "dashboard") + "-demo-mobile.png"), fullPage: true });
    await page.setViewportSize({ width: 1728, height: 1117 });
  }
  // Case selection, filter resets, empty search and working dossier navigation.
  await page.goto(base + "/component-04/cases");
  await page.getByRole("button", { name: "Explore demo data" }).click();
  await page.getByRole("button", { name: "Review", exact: true }).first().click();
  await page.getByRole("button", { name: "Close case detail" }).waitFor();
  await page.getByRole("searchbox").fill("no-matching-case");
  assert.equal(await page.getByRole("button", { name: "Close case detail" }).count(), 0);
  await page.getByText("No governance assessments found matching your filter criteria.").waitFor();
  await page.getByRole("searchbox").fill("");
  await page.getByRole("button", { name: "Clear / Compliant" }).click();
  assert.equal(await page.getByRole("button", { name: "Clear / Compliant" }).getAttribute("aria-pressed"), "true");
  await page.getByRole("button", { name: "Review", exact: true }).first().click();
  assert.equal(await page.getByText("All Core Requirements Satisfied", { exact: true }).count(), 0);
  await page.getByRole("link", { name: "Open Case Dossier" }).click();
  await page.getByRole("heading", { name: "Case Assessment Dossier" }).waitFor();
  await page.screenshot({ path: path.join(output, "dossier-demo-desktop.png"), fullPage: true });
  await page.getByText("NotAssessed", { exact: true }).waitFor();
  await page.getByRole("link", { name: "Compliance", exact: true }).click();
  await page.getByRole("heading", { name: "Regulatory Compliance Check" }).waitFor();
  await page.screenshot({ path: path.join(output, "compliance-dossier-demo-desktop.png"), fullPage: true });
  await page.getByRole("link", { name: "Conflicts", exact: true }).click();
  await page.getByRole("heading", { name: "Conflict Detection Check" }).waitFor();
  await page.screenshot({ path: path.join(output, "conflicts-dossier-demo-desktop.png"), fullPage: true });
  assert.equal(await page.getByText("DEMO DATA", { exact: true }).count(), 1, "demo stays explicit across navigation");
  // Selecting history changes the assessment, not the case key.
  await page.getByRole("link", { name: "Complaint intelligence", exact: true }).first().click();
  await page.getByRole("combobox", { name: "Assessment", exact: true }).selectOption("1");
  await page.getByText("DEMO-COMPLAINT-001", { exact: true }).waitFor();
  assert.equal(await page.getByRole("button", { name: "Classify complaint" }).isDisabled(), true);
  await page.getByRole("button", { name: "Return to live" }).click();
  assert.equal(await page.getByRole("combobox", { name: "Assessment", exact: true }).count(), 0);
  assert.equal(await page.getByText("DEMO-COMPLAINT-001", { exact: true }).count(), 0);
  // Component 1 navigation keeps its own labels and route matching.
  const c4Requests = [...requests];
  const c4Errors = [...errors];
  assert.deepEqual(c4Requests, []);
  assert.deepEqual(c4Errors, []);
  await page.goto(base + "/land-intelligence");
  await page.getByRole("complementary", { name: "Land Intelligence navigation" }).waitFor();
  assert.equal(await page.getByRole("complementary", { name: "Land Intelligence navigation" }).getByRole("link", { name: "Find Suitable Land", exact: false }).count(), 1);
  assert.equal(await page.getByRole("complementary", { name: "Governance Intelligence navigation" }).count(), 0);
  await page.screenshot({ path: path.join(output, "component-1-desktop.png"), fullPage: true });
  // Only inspect C4 requests: Component 1 is independently integrated and may call its own API.
  console.log(JSON.stringify({ routes: routes.length, desktop: "1728x1117", mobile: "390x844", screenshots: output, c4ApiRequests: c4Requests, c4Errors, component1Errors: errors.slice(c4Errors.length) }, null, 2));
  await browser.close();
})().catch(error => { console.error(error); process.exit(1); });
