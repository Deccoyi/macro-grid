import { describe, expect, it } from "vitest";
import { protectCell, readCsv, unprotectCell, writeCsv } from "../src/i18n/pack/csv";
import { buildTemplate, coverage, ENGLISH_KEYS, importTable, sourceHash } from "../src/i18n/pack/template";
import { en } from "../src/i18n/en";

describe("csv", () => {
  it("round-trips quotes, commas and line breaks", () => {
    const rows = [["a", 'say "hi", ok', "two\nlines"], ["", "x", "y"]];
    expect(readCsv(writeCsv(rows))).toEqual(rows);
  });

  it("reads a byte order mark, a semicolon table and CRLF", () => {
    expect(readCsv("﻿key;translation\r\na;b\r\n")).toEqual([["key", "translation"], ["a", "b"]]);
  });

  it("refuses a quote that never closes", () => {
    expect(readCsv('a,"b')).toBeNull();
  });

  it("protects formula-looking cells and restores them", () => {
    for (const cell of ["=1+1", "+x", "-x", "@x"]) {
      expect(protectCell(cell)).toBe(`'${cell}`);
      expect(unprotectCell(protectCell(cell))).toBe(cell);
    }
    expect(protectCell("plain")).toBe("plain");
    expect(unprotectCell("'quoted")).toBe("'quoted");
  });
});

describe("language table", () => {
  it("has one row per English text plus the header", () => {
    const rows = readCsv(buildTemplate())!;
    expect(rows).toHaveLength(ENGLISH_KEYS.length + 1);
    expect(rows[0]).toEqual(["key", "english", "translation", "note", "max length"]);
  });

  it("imports accepted, empty and refused rows into a report", () => {
    const text = writeCsv([
      ["key", "english", "translation"],
      ["app.loading", en["app.loading"], "Laden"],
      ["app.title", en["app.title"], ""],
      ["consent.http", en["consent.http"], "ohne Platzhalter"],
      ["nope.nope", "", "x"],
    ]);
    const outcome = importTable(text, "de", "Deutsch", 1);

    expect(outcome.ok).toBe(true);
    if (!outcome.ok) return;
    expect(outcome.report.accepted).toBe(1);
    expect(outcome.report.empty).toBe(1);
    expect(outcome.report.refused.map((r) => [r.line, r.reason])).toEqual([[4, "placeholders"], [5, "unknownKey"]]);
    expect(outcome.pack.strings).toEqual({ "app.loading": "Laden" });
    expect(outcome.pack.sources?.["app.loading"]).toBe(sourceHash(en["app.loading"]));
  });

  it("refuses a table without the key and translation columns", () => {
    expect(importTable("a,b\r\n1,2\r\n", "de", "x", 1)).toEqual({ ok: false, reason: "noHeader" });
    expect(importTable("", "de", "x", 1)).toEqual({ ok: false, reason: "notATable" });
  });

  it("marks a row for review when the English text changed after it was translated", () => {
    const stale = { "app.loading": "Laden", "app.title": "Titel" };
    const sources = { "app.loading": "00000000", "app.title": sourceHash(en["app.title"]) };

    expect(coverage(stale, sources)).toMatchObject({ done: 2, needsReview: 1 });
  });
});
