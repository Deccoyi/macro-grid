/** The browser client speaks Turkish when the browser language is Turkish and English otherwise. */
const turkish = typeof navigator !== "undefined" && navigator.language.toLowerCase().startsWith("tr");

export const t = (english: string, turkishText: string) => (turkish ? turkishText : english);
