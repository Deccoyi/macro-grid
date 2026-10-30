/** Whether a Toolbox entry matches what the person typed: every word of the query has to appear in one of the texts, ignoring case. */
export function matchesSearch(query: string, ...texts: (string | undefined | null)[]): boolean {
  const words = query.toLocaleLowerCase().split(/\s+/).filter(Boolean);
  if (words.length === 0) return true;
  const haystack = texts.filter(Boolean).join(" ").toLocaleLowerCase();
  return words.every((word) => haystack.includes(word));
}
