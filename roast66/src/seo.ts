import { matchPath } from "react-router-dom";

export const SITE_ORIGIN = "https://www.roast66coffee.com";

export const publicPages = {
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

export const organization = {
  "@context": "https://schema.org",
  "@type": "Organization",
  name: "Roast 66 Coffee",
  "@id": `${SITE_ORIGIN}/#organization`,
  url: `${SITE_ORIGIN}/`,
  description: publicPages["/"].description,
  sameAs: ["https://www.instagram.com/roast66coffee/"],
  areaServed: [
    { "@type": "City", name: "Brentwood, California" },
    { "@type": "City", name: "Oakley, California" },
    { "@type": "City", name: "Antioch, California" },
  ],
};

export function getPublicPage(pathname: string) {
  return Object.entries(publicPages).find(([path]) => matchPath(path, pathname));
}
