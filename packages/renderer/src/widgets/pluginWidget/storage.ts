/**
 * The small key-value store a widget gets when it declares the `storage` option. It is not a file system: values are JSON, one object per widget,
 * at most 256 KB together. Each device keeps its own (a widget's data never leaves the device), and the person can clear it.
 */
export const WIDGET_STORAGE_LIMITS = {
  maxKeyChars: 64,
  maxKeys: 64,
  maxTotalChars: 256 * 1024,
} as const;

/** The part of the browser's storage the store needs; the tests give it a map. */
export interface WidgetStorageBackend {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export class WidgetDataStore {
  constructor(
    private readonly backend: WidgetStorageBackend,
    private readonly prefix: string,
  ) {}

  private read(widgetId: string): Record<string, unknown> {
    try {
      const value = JSON.parse(this.backend.getItem(this.prefix + widgetId) ?? "{}");
      return value && typeof value === "object" && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
    } catch {
      return {};
    }
  }

  get(widgetId: string, key: string): unknown {
    const data = this.read(widgetId);
    return Object.prototype.hasOwnProperty.call(data, key) ? data[key] : null;
  }

  set(widgetId: string, key: string, value: unknown): void {
    if (typeof key !== "string" || key.length === 0 || key.length > WIDGET_STORAGE_LIMITS.maxKeyChars) throw new Error("invalid_key: a key is 1 to 64 characters");
    const data = this.read(widgetId);
    if (!Object.prototype.hasOwnProperty.call(data, key) && Object.keys(data).length >= WIDGET_STORAGE_LIMITS.maxKeys) throw new Error("quota_exceeded: at most 64 keys");
    const next = { ...data, [key]: value === undefined ? null : value };
    const text = JSON.stringify(next);
    if (text.length > WIDGET_STORAGE_LIMITS.maxTotalChars) throw new Error("quota_exceeded: at most 256 KB in total");
    this.backend.setItem(this.prefix + widgetId, text);
  }

  remove(widgetId: string, key: string): void {
    const data = this.read(widgetId);
    if (!Object.prototype.hasOwnProperty.call(data, key)) return;
    delete data[key];
    this.backend.setItem(this.prefix + widgetId, JSON.stringify(data));
  }

  /** "Clear widget data": everything this widget stored on this device. */
  clear(widgetId: string): void {
    this.backend.removeItem(this.prefix + widgetId);
  }
}

/** The browser's local storage, or a plain in-memory map where it is not available or refuses (private windows, blocked site data). */
export function browserStorageBackend(): WidgetStorageBackend {
  const memory = new Map<string, string>();
  return {
    getItem(key) {
      try {
        return localStorage.getItem(key);
      } catch {
        return memory.get(key) ?? null;
      }
    },
    setItem(key, value) {
      try {
        localStorage.setItem(key, value);
      } catch {
        memory.set(key, value);
      }
    },
    removeItem(key) {
      try {
        localStorage.removeItem(key);
      } catch {
        memory.delete(key);
      }
    },
  };
}
