import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { en } from "../src/i18n/en";
import { buildPseudoPack } from "../src/i18n/pack/pseudo";
import { sanitizeText } from "../src/i18n/pack/sanitizeText";
import { checkPack, validateEntry } from "../src/i18n/pack/validate";
import { canonicalTag, isRightToLeft, localeOf } from "../src/i18n/language";

const server = vi.hoisted(() => ({
  language: "de",
  pack: null as unknown,
  prefs: () => ({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: server.language }),
}));

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve(server.prefs()),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => (server.pack ? Promise.resolve(server.pack) : Promise.reject(new Error("404"))),
    listLanguagePacks: () => Promise.resolve([]),
  },
}));

import { useT } from "../src/i18n/I18nContext";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

const wrapper = ({ children }: { children: ReactNode }) => <PreferencesProvider>{children}</PreferencesProvider>;
const pack = (strings: Record<string, string>, tag = "de") => ({ meta: { format: 1, tag, name: "Deutsch", version: 1 }, strings });

describe("sanitizeText", () => {
  const clean = (value: unknown, english?: string) => sanitizeText(value, english);

  it("keeps markup and formula-looking text as plain text", () => {
    expect(clean("<script>alert(1)</script>")).toEqual({ status: "ok", text: "<script>alert(1)</script>" });
    expect(clean("=SUM(A1)")).toEqual({ status: "ok", text: "=SUM(A1)" });
  });

  it("refuses what is not text, too long, or has a lone surrogate", () => {
    expect(clean(5)).toMatchObject({ status: "refused", reason: "notText" });
    expect(clean("x".repeat(2001))).toMatchObject({ status: "refused", reason: "tooLong" });
    expect(clean("x".repeat(2000))).toMatchObject({ status: "ok" });
    expect(clean("a\uD800b")).toMatchObject({ status: "refused", reason: "brokenSurrogate" });
    expect(clean("a\uDC00")).toMatchObject({ status: "refused", reason: "brokenSurrogate" });
  });

  it("removes direction overrides, hidden characters and controls", () => {
    expect(clean("a‮b‪c​d﻿e\u0007f\u0085g")).toEqual({ status: "ok", text: "abcdefg" });
  });

  it("keeps the joiners and direction marks that scripts need", () => {
    expect(clean("a‌b‍c‎d‏")).toEqual({ status: "ok", text: "a‌b‍c‎d‏" });
  });

  it("keeps balanced isolates and drops unbalanced ones", () => {
    expect(clean("⁨x⁩")).toEqual({ status: "ok", text: "⁨x⁩" });
    expect(clean("⁨x")).toEqual({ status: "ok", text: "x" });
    expect(clean("x⁩⁨")).toEqual({ status: "ok", text: "x" });
  });

  it("turns a line break into a space unless the English text has one", () => {
    expect(clean("a\r\nb\tc", "one line")).toEqual({ status: "ok", text: "a b c" });
    expect(clean("a\r\nb", "two\nlines")).toEqual({ status: "ok", text: "a\nb" });
  });

  it("calls an empty result not translated", () => {
    expect(clean("   ​ ")).toEqual({ status: "empty" });
  });
});

describe("validateEntry", () => {
  it("accepts a row with the same placeholders in any order", () => {
    expect(validateEntry("consent.http", "Senden an {target}")).toMatchObject({ status: "ok" });
    expect(validateEntry("diag.save.total", "{pages} / {count}")).toMatchObject({ status: "ok" });
  });

  it("refuses a missing, extra or mistyped placeholder", () => {
    expect(validateEntry("consent.http", "Senden")).toMatchObject({ status: "refused", reason: "placeholders" });
    expect(validateEntry("consent.http", "Senden an {target} {name}")).toMatchObject({ status: "refused", reason: "placeholders" });
    expect(validateEntry("consent.http", "Senden an {tagret}")).toMatchObject({ status: "refused", reason: "placeholders" });
  });

  it("refuses unknown keys, including names that exist on every object", () => {
    for (const key of ["__proto__", "constructor", "toString", "nope.nope", "hasOwnProperty"]) {
      expect(validateEntry(key, "x")).toMatchObject({ status: "refused", reason: "unknownKey" });
    }
  });

  it("accepts the plural categories a language may need, and refuses them for a key that has no plural", () => {
    expect(validateEntry("status.pluginUpdates.few", "{n} Updates")).toMatchObject({ status: "ok" });
    expect(validateEntry("status.pluginUpdates.few", "Updates")).toMatchObject({ status: "ok" });
    expect(validateEntry("status.pluginUpdates.few", "{x}")).toMatchObject({ status: "refused" });
    expect(validateEntry("app.title.few", "x")).toMatchObject({ status: "refused", reason: "unknownKey" });
  });
});

describe("checkPack", () => {
  it("leaves out bad rows and keeps the rest", () => {
    const checked = checkPack(pack({ "app.title": "Editor", "consent.http": "ohne", "__x": "y", "palette.noMatch": "x".repeat(3000) }), "de")!;

    expect(Object.keys(checked.strings)).toEqual(["app.title"]);
    expect(checked.rejected).toBe(3);
  });

  it("does not let a key called __proto__ reach the prototype", () => {
    const raw = JSON.parse('{"meta":{"tag":"de","name":"x","version":1},"strings":{"__proto__":"polluted","constructor":"x","app.title":"T"}}');
    const checked = checkPack(raw, "de")!;

    expect(({} as Record<string, unknown>).polluted).toBeUndefined();
    expect(Object.keys(checked.strings)).toEqual(["app.title"]);
    expect(Object.getPrototypeOf(checked.strings)).toBeNull();
  });

  it("refuses a pack of the wrong shape or for another tag", () => {
    expect(checkPack(null, "de")).toBeNull();
    expect(checkPack([], "de")).toBeNull();
    expect(checkPack({ meta: {}, strings: {} }, "de")).toBeNull();
    expect(checkPack(pack({}, "fr"), "de")).toBeNull();
    expect(checkPack({ meta: pack({}).meta, strings: [] }, "de")).toBeNull();
  });

  it("reads the pseudo pack without rejecting a single row", () => {
    const pseudo = buildPseudoPack();

    expect(pseudo.rejected).toBe(0);
    expect(Object.keys(pseudo.strings)).toHaveLength(Object.keys(en).length);
  });
});

describe("language tags", () => {
  it("canonicalises and refuses", () => {
    expect(canonicalTag("pt-br")).toBe("pt-BR");
    expect(canonicalTag(" de ")).toBe("de");
    for (const bad of ["tr", "en", "", "d", "../de", "de.json", "DE", "x".repeat(40)]) expect(canonicalTag(bad), bad).toBeNull();
  });

  it("gives a locale for the built-in languages and for a tag", () => {
    expect(localeOf("tr")).toBe("tr-TR");
    expect(localeOf("en")).toBe("en-US");
    expect(localeOf("de")).toBe("de");
    expect(isRightToLeft("ar-EG")).toBe(true);
    expect(isRightToLeft("de")).toBe(false);
  });
});

describe("t with a language pack", () => {
  beforeEach(() => {
    server.language = "de";
    server.pack = null;
  });

  it("shows English for a saved tag whose pack is missing", async () => {
    const { result } = renderHook(() => useT(), { wrapper });
    await waitFor(() => expect(result.current.lang).toBe("de"));
    expect(result.current.t("app.loading")).toBe(en["app.loading"]);
  });

  it("uses the pack's text and falls back to English for a key it lacks", async () => {
    server.pack = pack({ "app.loading": "Laden…", "diag.save.total": "{pages} / {count}" });
    const { result } = renderHook(() => useT(), { wrapper });

    await waitFor(() => expect(result.current.t("app.loading")).toBe("Laden…"));
    expect(result.current.t("app.title")).toBe(en["app.title"]);
    expect(result.current.t("diag.save.total", "a", "b")).toContain("⁨");
  });

  it("keeps a hostile text as text", async () => {
    server.pack = pack({ "app.loading": "<img src=x onerror=alert(1)>" });
    const { result } = renderHook(() => useT(), { wrapper });

    await waitFor(() => expect(result.current.t("app.loading")).toBe("<img src=x onerror=alert(1)>"));
  });

  it("picks plural rows by the language rules and falls back to the pack's other row", async () => {
    server.pack = pack({ "status.pluginUpdates.one": "{n} Update", "status.pluginUpdates.other": "{n} Updates" });
    const { result } = renderHook(() => useT(), { wrapper });

    await waitFor(() => expect(result.current.tn("status.pluginUpdates", 1)).toContain("Update"));
    expect(result.current.tn("status.pluginUpdates", 1)).toBe("⁨1⁩ Update");
    expect(result.current.tn("status.pluginUpdates", 3)).toBe("⁨3⁩ Updates");
  });

  it("keeps the English text next to a pack's text on a permission screen only", async () => {
    server.pack = pack({ "consent.http": "Senden an {target}", "app.loading": "Laden…" });
    const { result } = renderHook(() => useT(), { wrapper });

    await waitFor(() => expect(result.current.t("app.loading")).toBe("Laden…"));
    expect(result.current.t("consent.http", "example.com")).toBe("Senden an ⁨example.com⁩ (Send web requests to example.com)");
  });

  it("leaves the built-in languages exactly as they are", async () => {
    server.language = "en";
    const { result } = renderHook(() => useT(), { wrapper });

    await waitFor(() => expect(result.current.lang).toBe("en"));
    expect(result.current.t("consent.http", "example.com")).toBe("Send web requests to example.com");
    expect(result.current.tn("status.pluginUpdates", 1)).toBe("1 plugin update needed");
  });
});
