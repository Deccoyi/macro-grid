/** Same cut as the server's template (shown while a variable is unavailable). */
const MAX_PLACEHOLDER = 64;

/**
 * Best-effort local substitution of {name|format} tokens using the one-shot variable snapshot, so a
 * template widget shows something close to what the server's Template.Render would produce, rather
 * than either the raw "{system.cpu}" text or (worse) an unformatted "2026-09-22T10:09:03.006Z". Only
 * approximates the server's number/date formatting — the server's own render always wins at runtime.
 */
export function renderPreviewText(text: string | undefined, variables: Record<string, unknown>): string | undefined {
  if (!text || !text.includes("{")) return text;
  return text.replace(/\{\{|\}\}|\{([^{}|]+)(?:\|([^{}|]*))?(?:\|([^}]*))?\}/g, (match, name: string | undefined, format: string | undefined, placeholder: string | undefined) => {
    if (match === "{{") return "{";
    if (match === "}}") return "}";
    if (!name) return match;
    const value = variables[name.trim()];
    if (value === undefined || value === null) return placeholder === undefined ? "" : placeholder.slice(0, MAX_PLACEHOLDER);
    return formatPreviewValue(value, format);
  });
}

function formatPreviewValue(value: unknown, format: string | undefined): string {
  if (typeof value === "number") return formatNumber(value, format);
  if (typeof value === "string" && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(value)) {
    const date = new Date(value);
    if (!Number.isNaN(date.getTime())) return formatDate(date, format);
  }
  return String(value);
}

function formatNumber(value: number, format: string | undefined): string {
  const decimalsMatch = /^0\.(#+)$/.exec(format ?? "0.##");
  const maxDecimals = decimalsMatch ? decimalsMatch[1]!.length : 0;
  const fixed = value.toFixed(maxDecimals);
  return maxDecimals > 0 ? fixed.replace(/\.?0+$/, "") : fixed;
}

function formatDate(date: Date, format: string | undefined): string {
  const pad = (n: number) => n.toString().padStart(2, "0");
  const tokens: Record<string, string> = {
    HH: pad(date.getHours()),
    mm: pad(date.getMinutes()),
    ss: pad(date.getSeconds()),
    dd: pad(date.getDate()),
    MM: pad(date.getMonth() + 1),
    yyyy: date.getFullYear().toString(),
  };
  const pattern = format ?? "HH:mm";
  return pattern.replace(/HH|mm|ss|dd|MM|yyyy/g, (token) => tokens[token] ?? token);
}
