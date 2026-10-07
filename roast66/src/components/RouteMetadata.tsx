import { useEffect } from "react";
import { matchPath, useLocation } from "react-router-dom";
import { en } from "../i18n/strings/en";

const publicPages = {
  "/": {
    title: "Roast 66 Coffee | Mobile Coffee Trailer",
    description:
      "Roast 66 Coffee is a mobile coffee trailer serving handcrafted drinks in the Brentwood, Oakley, and Antioch area of California. Follow us for our next stop.",
  },
  "/about": {
    title: "Our Story | Roast 66 Coffee",
    description:
      "Meet Roast 66 Coffee, an owner-run mobile coffee trailer built from a love of cars, coffee, and community. Learn our story and connect with us for your next event.",
  },
  "/menu": {
    title: "Coffee & Drink Menu | Roast 66 Coffee",
    description:
      "Explore the current Roast 66 Coffee menu, including handcrafted coffee, specialty drinks, and flavors for your next visit to our mobile coffee trailer.",
  },
  "/order": {
    title: "Order Online | Roast 66 Coffee",
    description:
      "Order online from Roast 66 Coffee. Choose your drinks, customize your coffee, and place your order with our mobile coffee trailer.",
  },
};

const organization = {
  "@context": "https://schema.org",
  "@type": "Organization",
  name: "Roast 66 Coffee",
  description: publicPages["/"].description,
  sameAs: ["https://www.instagram.com/roast66coffee/"],
  areaServed: [
    { "@type": "City", name: "Brentwood, California" },
    { "@type": "City", name: "Oakley, California" },
    { "@type": "City", name: "Antioch, California" },
  ],
};

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
    const page = Object.entries(publicPages).find(([path]) => matchPath(path, pathname))?.[1];
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

    if (matchPath("/", pathname)) {
      const script = document.createElement("script");
      script.type = "application/ld+json";
      script.textContent = JSON.stringify(organization);
      document.head.appendChild(script);
      return () => script.remove();
    }
  }, [pathname]);

  return null;
}
