import { readFile, access } from "node:fs/promises";
import { resolve } from "node:path";
import { test } from "node:test";
import assert from "node:assert/strict";
import { JSDOM } from "jsdom";

const origin = "https://www.roast66coffee.com";
const paths = ["/", "/about", "/menu", "/order"];
const output = (path) => path === "/" ? "index.html" : `${path.slice(1)}.html`;
const titles = new Set();
const descriptions = new Set();

for (const path of paths) {
  test(`${path} is useful and indexable before JavaScript runs`, async () => {
    const html = await readFile(resolve("dist", output(path)), "utf8");
    // JSDOM does not execute scripts. Inspect the actual shipped document.
    const { document } = new JSDOM(html).window;
    assert.equal(document.querySelectorAll("h1").length, 1);
    assert.ok(document.querySelector("main").textContent.trim().length > 100);
    assert.equal(document.querySelectorAll("title").length, 1);
    assert.ok(!titles.has(document.title), "each page needs its own title");
    titles.add(document.title);
    const description = document.querySelector('meta[name="description"]').content;
    assert.ok(!descriptions.has(description), "each page needs its own description");
    descriptions.add(description);
    assert.equal(document.querySelector('meta[name="robots"]').content, "index, follow");
    assert.equal(document.querySelectorAll('link[rel="canonical"]').length, 1);
    assert.equal(document.querySelector('link[rel="canonical"]').href, `${origin}${path}`);
    assert.equal(document.querySelector('meta[property="og:url"]').content, `${origin}${path}`);
    assert.equal(document.querySelector('meta[property="og:title"]').content, document.title);
    assert.equal(document.querySelector('meta[property="og:description"]').content, description);
    assert.ok(!html.includes("You need to enable JavaScript to run this app"));
    for (const route of paths) {
      assert.ok(document.querySelector(`a[href="${route}"]`), `missing crawlable link to ${route}`);
    }
    for (const asset of document.querySelectorAll('[src^="/assets/"], link[href^="/assets/"]')) {
      await access(resolve("dist", (asset.getAttribute("src") ?? asset.getAttribute("href")).slice(1)));
    }
    if (path === "/order") {
      assert.ok(document.querySelector('link[rel="stylesheet"][href*="OrderPage-"]'), "ordering CSS must load without JavaScript");
    }
    if (path === "/") {
      const schema = JSON.parse(document.querySelector('script[type="application/ld+json"]').textContent);
      assert.equal(schema.url, `${origin}/`);
      assert.equal(schema["@type"], "Organization");
      assert.ok(!schema.address && !schema.openingHours);
      for (const city of ["Oakley", "Brentwood", "Antioch"]) {
        assert.ok(document.querySelector("main").textContent.includes(city));
      }
      assert.ok(!document.querySelector(".r66-special-price"), "do not bake sample prices into searchable HTML");
    }
  });
}

test("the SPA fallback is noindex and does not advertise the homepage canonical", async () => {
  const { document } = new JSDOM(await readFile("dist/app.html", "utf8")).window;
  assert.equal(document.querySelector('meta[name="robots"]').content, "noindex, nofollow");
  assert.equal(document.querySelector('link[rel="canonical"]'), null);
  assert.equal(document.querySelector('script[type="application/ld+json"]'), null);
  assert.ok(document.querySelector('script[type="module"][src^="/assets/"]'));
});

test("robots discovers an XML sitemap containing exactly the public canonical URLs", async () => {
  const robots = await readFile("dist/robots.txt", "utf8");
  assert.ok(robots.includes(`Sitemap: ${origin}/sitemap.xml`));
  assert.ok(!/Disallow:\s*\//.test(robots), "allow crawlers to read noindex directives");
  const sitemap = new JSDOM(await readFile("dist/sitemap.xml", "utf8"), { contentType: "text/xml" });
  const locations = [...sitemap.window.document.querySelectorAll("loc")].map((node) => node.textContent);
  assert.deepEqual(locations, paths.map((path) => `${origin}${path}`));
});
