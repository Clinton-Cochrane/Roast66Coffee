import { useEffect } from "react";
import { useLocation } from "react-router-dom";
import { en } from "../i18n/strings/en";
import { getPublicPage, organization, SITE_ORIGIN } from "../seo";

function setMeta(attribute: "name" | "property", key: string, content: string) {
  let element = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${key}"]`);
  if (!element) {
    element = document.createElement("meta");
    element.setAttribute(attribute, key);
    document.head.appendChild(element);
  }
  element.content = content;
}

export default function RouteMetadata() {
  const { pathname } = useLocation();

  useEffect(() => {
    const match = getPublicPage(pathname);
    const page = match?.[1];
    const title = page?.title ?? en.meta.pageTitle;
    const description = page?.description ?? en.meta.description;

    // SEO stays English; LanguageProvider owns the UI locale and document language.
    document.title = title;
    setMeta("name", "description", description);
    // Only the four public pages are indexable, including before an admin redirect.
    setMeta("name", "robots", page ? "index, follow" : "noindex, nofollow");
    setMeta("property", "og:title", title);
    setMeta("property", "og:description", description);
    setMeta("property", "og:type", "website");
    setMeta("property", "og:site_name", organization.name);

    const existingCanonical = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (match) {
      const canonical = existingCanonical ?? document.createElement("link");
      canonical.rel = "canonical";
      canonical.href = `${SITE_ORIGIN}${match[0]}`;
      if (!existingCanonical) document.head.appendChild(canonical);
      setMeta("property", "og:url", canonical.href);
    } else {
      existingCanonical?.remove();
      document.head.querySelector('meta[property="og:url"]')?.remove();
    }

    // Reuse the build-time script so the initial client render cannot duplicate it.
    const existingSchema = document.head.querySelector<HTMLScriptElement>("#organization-schema");
    if (match?.[0] === "/") {
      const script = existingSchema ?? document.createElement("script");
      script.id = "organization-schema";
      script.type = "application/ld+json";
      script.textContent = JSON.stringify(organization);
      if (!existingSchema) document.head.appendChild(script);
      return () => script.remove();
    }
    existingSchema?.remove();
  }, [pathname]);

  return null;
}
