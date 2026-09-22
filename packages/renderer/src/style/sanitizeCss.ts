import postcss, { type AtRule, type Declaration, type Rule } from "postcss";
import safeParse from "postcss-safe-parser";

/**
 * Strips anything from user-written widget CSS that could break the grid layout or fetch
 * an external resource, while leaving purely visual declarations (gradients, shadows,
 * animations, borders, ...) untouched. Runs on both the server editor (live preview) and
 * the client (received over the wire), so it must never throw on malformed input.
 */

const FORBIDDEN_PROPS = new Set([
  "width",
  "height",
  "position",
  "inset",
  "inset-block",
  "inset-inline",
  "inset-block-start",
  "inset-block-end",
  "inset-inline-start",
  "inset-inline-end",
  "top",
  "right",
  "bottom",
  "left",
  "margin",
  "margin-top",
  "margin-right",
  "margin-bottom",
  "margin-left",
  "transform",
  "transform-origin",
  "zoom",
  "display",
]);

const FORBIDDEN_AT_RULES = new Set(["import"]);

const URL_RE = /url\(\s*(['"]?)([^'")]*)\1\s*\)/gi;

export interface SanitizeResult {
  /** The cleaned stylesheet text, safe to inject into the widget's shadow root. */
  css: string;
  /** Human-readable (Turkish) notes on what was removed and why, for the editor to show as warnings. */
  removed: string[];
}

export function sanitizeWidgetCss(input: string | undefined | null): SanitizeResult {
  if (!input || !input.trim()) return { css: "", removed: [] };

  const removed: string[] = [];
  let root;
  try {
    root = postcss().process(input, { parser: safeParse }).root;
  } catch {
    return { css: "", removed: ["CSS ayrıştırılamadı."] };
  }

  root.walkAtRules((atRule: AtRule) => {
    if (FORBIDDEN_AT_RULES.has(atRule.name.toLowerCase())) {
      removed.push(`@${atRule.name} kaldırıldı (dış kaynak yüklenemez).`);
      atRule.remove();
    }
  });

  root.walkDecls((decl: Declaration) => {
    const prop = decl.prop.toLowerCase();
    if (isForbiddenProperty(prop)) {
      removed.push(`${decl.prop} kaldırıldı (boyut/konum widget tarafından belirlenir).`);
      decl.remove();
      return;
    }
    if (hasExternalUrl(decl.value)) {
      removed.push(`${decl.prop}: dış url() kaldırıldı (yalnızca data: URI'lere izin var).`);
      decl.remove();
    }
  });

  // Drop rules that ended up with no declarations left, so the output stays clean.
  root.walkRules((rule: Rule) => {
    if (rule.nodes.length === 0) rule.remove();
  });

  return { css: root.toString(), removed };
}

function isForbiddenProperty(prop: string): boolean {
  if (FORBIDDEN_PROPS.has(prop)) return true;
  return prop.startsWith("min-") || prop.startsWith("max-") || prop.startsWith("grid-");
}

function hasExternalUrl(value: string): boolean {
  URL_RE.lastIndex = 0;
  let match: RegExpExecArray | null;
  while ((match = URL_RE.exec(value))) {
    const target = (match[2] ?? "").trim().toLowerCase();
    if (!target.startsWith("data:")) return true;
  }
  return false;
}
