let nextTempId = 1;

/** Client-generated ids only ever need to be unique within this editing session; the server assigns real ones on first save of a brand new widget/page... */
export function tempId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${nextTempId++}`;
}
