const TOKEN = /\{([A-Za-z][A-Za-z0-9]*)\}/g;

/**
 * Fills the `{name}` placeholders of a dictionary string. Only the declared names are replaced, so any other text in braces stays as
 * written; a replaced value is never scanned again (one pass), so a value that itself contains `{name}` is shown as it is. A missing
 * argument gives an empty string.
 */
export function format(template: string, names: readonly string[], args: readonly string[], wrap?: (value: string) => string): string {
  return template.replace(TOKEN, (token, name: string) => {
    const index = names.indexOf(name);
    if (index < 0) return token;
    const value = args[index] ?? "";
    return wrap ? wrap(value) : value;
  });
}
