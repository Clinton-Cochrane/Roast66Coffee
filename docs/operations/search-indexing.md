# Search indexing and SEO verification

## Observed production baseline

On October 22, 2026, `https://roast66coffee.com/` returned a permanent redirect
to `https://www.roast66coffee.com/`. The destination returned HTTP 200 with an
empty React root, a generic description, and “You need to enable JavaScript to
run this app.” `/sitemap.xml` returned that same HTML document. `robots.txt`
allowed crawling but did not advertise a sitemap. These observations establish
delivery problems; they do not establish Google's indexing status. Gemini's
answer and a `site:` query are not substitutes for Search Console inspection.

## Build and hosting behavior

`npm run build` builds the browser app, renders the existing public React page
components in Node, and writes `index.html`, `about.html`, `menu.html`, and
`order.html`. The Node bundle stays outside `dist` and is removed afterward.
The browser replaces the static markup using the existing `createRoot` flow,
so language preferences, live menu data, and ordering retain their behavior.
The build makes no API requests and embeds no live prices or order data.

Each public response includes a distinct English title and description, an
index/follow directive, canonical URL, Open Graph tags, and crawlable links.
The homepage includes Organization JSON-LD with the canonical URL, Instagram
profile, and service cities. It does not claim a permanent street address or
opening hours. This is a mobile business; confirm any future business details
with the owner before adding LocalBusiness data.

Menu availability, individual menu rows, prices, and ordering still require
JavaScript and the API. The generated pages describe the business and provide
navigation and an Instagram contact link without JavaScript. Home's sample
fallback specials are excluded from generated HTML. This does not turn the
live API menu into a static snapshot or guarantee individual menu-item indexing.

`app.html` is a separate noindex/nofollow document with no public canonical.
Nginx serves it for staff, order tracking, confirmation, and unknown routes.
Missing scripts, styles, images, and crawl files return 404. Render static-site
Blueprints rewrite public routes to their respective documents and use
`app.html` as the final fallback. Apply those rules if the live deployment uses
a Render static site. A Docker frontend uses `public/nginx.conf` automatically.
Vite preview is useful for public pages but does not reproduce Nginx's private
route fallback; use Nginx for the browser SEO checks.

`robots.txt` allows crawling, including crawling the noindex directives, and
advertises `https://www.roast66coffee.com/sitemap.xml`. The generated sitemap
contains exactly the four public canonical URLs. It uses no fabricated
last-modified timestamps or search-engine submission pings.

## Verification

The frontend CI job runs `npm run test:seo` after building. Those focused checks
inspect the actual generated documents without executing JavaScript, validate
the sitemap, ensure referenced assets exist, and verify the noindex fallback.
`src/App.test.tsx` covers client navigation and adoption of generated metadata.
The release smoke script serves the built frontend through Nginx, then runs
the live-menu smoke and `e2e/seo.spec.ts`. The SEO browser checks cover visible
content without JavaScript, client navigation, private routes, and missing files.

Ready-to-run local OpenCode prompt, from the repository root:

```sh
opencode run 'Validate issue #334 on the current topic branch. Read /home/workstation/Storage/code/AGENTS.md first. Do not edit files, switch branches, commit, push, merge, or deploy. In roast66 run npm ci, npm test, npm run lint, npm run build, and npm run test:seo. From the repository root run scripts/ci/verify-hosting-config.sh. Then run scripts/ci/render-smoke.sh "$PWD" "$PWD" to verify the built menu and SEO browser checks through Nginx; this needs Docker and Playwright Chromium (install with cd roast66 && npx playwright install chromium if absent). Report command results and failures. Do not mark Google indexing complete from local tests.'
```

After deployment, inspect the actual responses before asking Google to recrawl:

```sh
curl -I https://roast66coffee.com/
curl -fsS https://www.roast66coffee.com/
curl -fsS https://www.roast66coffee.com/about
curl -fsS https://www.roast66coffee.com/menu
curl -fsS https://www.roast66coffee.com/order
curl -fsS https://www.roast66coffee.com/robots.txt
curl -fsS https://www.roast66coffee.com/sitemap.xml
curl -fsS https://www.roast66coffee.com/admin
curl -I https://www.roast66coffee.com/missing.xml
```

Verify that public responses contain the actual page text and matching
canonicals with no `noindex` tag or `X-Robots-Tag` header, `/admin` has noindex
in its initial response, and the sitemap has an XML content type. Check that
the canonical host redirects consistently and that navigation and assets load.
Inspect all four public pages in a browser with JavaScript disabled and enabled.
Do not treat a green build as evidence that production has this revision.

## Google Search Console completion gate

An owner of the business/domain must use their existing verified Search Console
property or verify the `roast66coffee.com` Domain property using Google's exact
DNS TXT token. Never invent a verification token. Domain properties cover both
the apex and `www` hostnames.

1. Submit `https://www.roast66coffee.com/sitemap.xml` in Sitemaps and verify
   successful processing and discovery of the public URLs.
2. Inspect `https://www.roast66coffee.com/` with URL Inspection, run Test Live URL,
   and inspect the tested HTML/rendered result. Confirm it is accessible and
   indexable and contains the business content.
3. Request indexing of the homepage and inspect `/about`, `/menu`, and `/order`.
   Requesting indexing does not prove that indexing has happened.
4. Recheck URL Inspection after Google crawls the new deployment. Record the
   last crawl, “URL is on Google” status, and Google's selected canonical.
5. Verify a Google search result links to the actual domain for the business.
   Review Page Indexing reasons for any excluded public pages and fix genuine
   exclusions. Use Search Console as the authoritative per-URL evidence.

Keep the overall indexing goal open until production delivery and actual Google
indexing have been verified. Indexing is Google's decision, and a sitemap or
an indexing request cannot guarantee inclusion, timing, or ranking.

References:

- [Google JavaScript SEO basics](https://developers.google.com/search/docs/crawling-indexing/javascript/javascript-seo-basics)
- [Build and submit a sitemap](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap)
- [Request recrawling](https://developers.google.com/search/docs/crawling-indexing/ask-google-to-recrawl)
- [Render redirects and rewrites](https://render.com/docs/redirects-rewrites)
