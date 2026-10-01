import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Translated text, including a language pack the person imported, must only ever be drawn as text. The repository has no linter, so this
 * test reads every source file of the editor, the browser deck and the renderer and fails on the APIs that turn a string into markup or code.
 */
const ROOTS = ["src", "../webclient/src", "../packages/renderer/src"];
const FORBIDDEN: [string, RegExp][] = [
  ["innerHTML", /\binnerHTML\b/],
  ["outerHTML", /\bouterHTML\b/],
  ["insertAdjacentHTML", /\binsertAdjacentHTML\b/],
  ["dangerouslySetInnerHTML", /\bdangerouslySetInnerHTML\b/],
  ["document.write", /\bdocument\.write(ln)?\s*\(/],
  ["eval(", /(^|[^.\w])eval\s*\(/],
  ["new Function", /\bnew\s+Function\b/],
];
// The one place a document is built from text: it takes no argument and uses no translated string.
const SRCDOC_ALLOWED = ["widgets/pluginWidget/launcher.ts"];

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return name === "node_modules" ? [] : sources(path);
    return /\.(ts|tsx)$/.test(name) ? [path] : [];
  });
}

describe("unsafe DOM APIs", () => {
  const files = ROOTS.flatMap((root) => sources(root));

  it("reads a believable number of source files", () => {
    expect(files.length).toBeGreaterThan(100);
  });

  it("are not used anywhere", () => {
    const found: string[] = [];
    for (const file of files) {
      const text = readFileSync(file, "utf8");
      const code = text.split("\n").filter((line) => !/^\s*(\/\/|\*|\/\*)/.test(line)).join("\n");
      for (const [name, pattern] of FORBIDDEN) if (pattern.test(code)) found.push(`${relative(".", file)}: ${name}`);
      if (/\bsrcdoc\b/.test(code) && !SRCDOC_ALLOWED.some((allowed) => file.replaceAll("\\", "/").endsWith(allowed))) found.push(`${relative(".", file)}: srcdoc`);
    }
    expect(found).toEqual([]);
  });
});
