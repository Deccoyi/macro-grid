/**
 * A small RFC 4180 reader and writer for the translation table. The table is opened in a spreadsheet, so a cell that starts with a
 * formula character is written with a leading apostrophe and the apostrophe is removed again on import.
 */
const FORMULA_START = /^[=+\-@\t\r]/;

export const CSV_COLUMNS = ["key", "english", "translation", "note", "max length"] as const;

export function protectCell(value: string): string {
  return FORMULA_START.test(value) ? `'${value}` : value;
}

export function unprotectCell(value: string): string {
  return value.length > 1 && value[0] === "'" && FORMULA_START.test(value.slice(1)) ? value.slice(1) : value;
}

function quote(cell: string): string {
  return /[",\r\n]/.test(cell) ? `"${cell.replace(/"/g, '""')}"` : cell;
}

/** Writes rows as CSV with CRLF line ends (the leading byte order mark is added by the server's export). */
export function writeCsv(rows: readonly (readonly string[])[]): string {
  return rows.map((row) => row.map((cell) => quote(cell)).join(",")).join("\r\n") + "\r\n";
}

/** The separator a table uses: a semicolon when the first line has more semicolons than commas outside quotes (a spreadsheet in some regions saves that way). */
function detectSeparator(text: string): "," | ";" {
  let commas = 0;
  let semicolons = 0;
  let inQuotes = false;
  for (const ch of text) {
    if (ch === '"') inQuotes = !inQuotes;
    else if (!inQuotes && (ch === "\n" || ch === "\r")) break;
    else if (!inQuotes && ch === ",") commas++;
    else if (!inQuotes && ch === ";") semicolons++;
  }
  return semicolons > commas ? ";" : ",";
}

/** Reads CSV text into rows of cells. Returns null when a quoted cell is never closed. A blank line is skipped. */
export function readCsv(input: string): string[][] | null {
  const text = input.charCodeAt(0) === 0xfeff ? input.slice(1) : input;
  const separator = detectSeparator(text);
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = "";
  let inQuotes = false;
  let started = false;

  const endRow = () => {
    row.push(cell);
    if (row.length > 1 || row[0] !== "") rows.push(row);
    row = [];
    cell = "";
    started = false;
  };

  for (let i = 0; i < text.length; i++) {
    const ch = text[i]!;
    if (inQuotes) {
      if (ch === '"') {
        if (text[i + 1] === '"') { cell += '"'; i++; }
        else inQuotes = false;
      } else cell += ch;
    } else if (ch === '"' && !started) {
      inQuotes = true;
      started = true;
    } else if (ch === separator) {
      row.push(cell);
      cell = "";
      started = false;
    } else if (ch === "\r" || ch === "\n") {
      if (ch === "\r" && text[i + 1] === "\n") i++;
      endRow();
    } else {
      cell += ch;
      started = true;
    }
  }
  if (inQuotes) return null;
  if (started || cell !== "" || row.length > 0) endRow();
  return rows;
}
