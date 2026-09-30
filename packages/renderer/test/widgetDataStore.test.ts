import { describe, expect, it } from "vitest";
import { WIDGET_STORAGE_LIMITS, WidgetDataStore } from "../src/widgets/pluginWidget/storage";

function memory() {
  const map = new Map<string, string>();
  return { map, backend: { getItem: (k: string) => map.get(k) ?? null, setItem: (k: string, v: string) => void map.set(k, v), removeItem: (k: string) => void map.delete(k) } };
}

describe("widget data store", () => {
  it("keeps values per widget and returns null for a missing key", () => {
    const { backend } = memory();
    const store = new WidgetDataStore(backend, "p.");
    store.set("a", "count", 3);
    store.set("b", "count", 9);

    expect(store.get("a", "count")).toBe(3);
    expect(store.get("b", "count")).toBe(9);
    expect(store.get("a", "nothing")).toBeNull();
  });

  it("removes one key and clears all of a widget", () => {
    const { backend, map } = memory();
    const store = new WidgetDataStore(backend, "p.");
    store.set("a", "x", 1);
    store.set("a", "y", 2);
    store.remove("a", "x");
    expect(store.get("a", "x")).toBeNull();
    expect(store.get("a", "y")).toBe(2);

    store.clear("a");
    expect(map.size).toBe(0);
  });

  it("refuses a bad key, too many keys and too much data", () => {
    const { backend } = memory();
    const store = new WidgetDataStore(backend, "p.");

    expect(() => store.set("a", "", 1)).toThrow(/invalid_key/);
    expect(() => store.set("a", "k".repeat(WIDGET_STORAGE_LIMITS.maxKeyChars + 1), 1)).toThrow(/invalid_key/);
    for (let i = 0; i < WIDGET_STORAGE_LIMITS.maxKeys; i++) store.set("a", `k${i}`, i);
    expect(() => store.set("a", "one-more", 1)).toThrow(/quota_exceeded/);
    store.set("a", "k0", 5); // an existing key can still change

    expect(() => store.set("b", "big", "x".repeat(WIDGET_STORAGE_LIMITS.maxTotalChars))).toThrow(/quota_exceeded/);
  });

  it("does not trust damaged data", () => {
    const { backend, map } = memory();
    map.set("p.a", "not json");
    const store = new WidgetDataStore(backend, "p.");

    expect(store.get("a", "x")).toBeNull();
    store.set("a", "x", 1);
    expect(store.get("a", "x")).toBe(1);
  });
});
