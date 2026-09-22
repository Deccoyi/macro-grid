import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

// globals:false means Testing Library's own auto-cleanup (which hooks the *global* afterEach) never
// registers, so without this, DOM from one test would still be present — and matched by — the next.
afterEach(() => cleanup());

// jsdom has no vibration API; tests assert on calls to it, so give every test a stub to spy on.
if (!("vibrate" in navigator)) {
  Object.defineProperty(navigator, "vibrate", { value: () => true, writable: true, configurable: true });
}
