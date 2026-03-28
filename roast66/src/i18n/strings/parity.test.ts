import { describe, it, expect } from "vitest";
import { en } from "./en";
import { esMx } from "./es-MX";

/**
 * Ensures EN and es-MX bundles expose the same nested key paths so `t()` never
 * silently falls back for one locale due to a missing key.
 */
function collectLeafPaths(value: unknown, prefix = ""): string[] {
  if (value === null || typeof value !== "object") {
    return prefix ? [prefix] : [];
  }
  if (Array.isArray(value)) {
    return value.flatMap((item, i) => collectLeafPaths(item, `${prefix}[${i}]`));
  }
  const keys = Object.keys(value as Record<string, unknown>).sort();
  const paths: string[] = [];
  for (const key of keys) {
    const next = prefix ? `${prefix}.${key}` : key;
    const v = (value as Record<string, unknown>)[key];
    if (typeof v === "string") {
      paths.push(next);
    } else {
      paths.push(...collectLeafPaths(v, next));
    }
  }
  return paths;
}

describe("i18n string bundles", () => {
  it("has matching leaf key paths in en and es-MX", () => {
    const enPaths = new Set(collectLeafPaths(en));
    const esPaths = new Set(collectLeafPaths(esMx));
    const onlyEn = [...enPaths].filter((p) => !esPaths.has(p));
    const onlyEs = [...esPaths].filter((p) => !enPaths.has(p));
    expect(onlyEn).toHaveLength(0);
    expect(onlyEs).toHaveLength(0);
  });
});
