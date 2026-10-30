import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { WidgetView } from "../src/widgets/WidgetView";
import type { Widget } from "../src/types";

function web(url?: string): Widget {
  return { id: "w", type: "web", x: 0, y: 0, w: 2, h: 2, props: url === undefined ? {} : { url }, actions: {} };
}

function shadow(): ShadowRoot {
  return (document.querySelector("[data-ms-widget-host]") as HTMLElement).shadowRoot!;
}

describe("web widget", () => {
  it("draws the page in an iframe with every lock on", () => {
    render(<WidgetView widget={web("https://example.org/chat")} />);

    const frame = shadow().querySelector("iframe")!;
    expect(frame.getAttribute("src")).toBe("https://example.org/chat");
    expect(frame.getAttribute("sandbox")).toBe("allow-scripts allow-same-origin allow-forms");
    expect(frame.getAttribute("allow")).toBe("");
    expect(frame.getAttribute("referrerpolicy")).toBe("no-referrer");
  });

  it("never grants popups, downloads, top navigation, dialogs or device features", () => {
    render(<WidgetView widget={web("https://example.org/")} />);

    const tokens = shadow().querySelector("iframe")!.getAttribute("sandbox")!.split(" ");
    for (const forbidden of [
      "allow-popups",
      "allow-popups-to-escape-sandbox",
      "allow-downloads",
      "allow-top-navigation",
      "allow-top-navigation-by-user-activation",
      "allow-modals",
      "allow-orientation-lock",
      "allow-pointer-lock",
      "allow-presentation",
    ])
      expect(tokens).not.toContain(forbidden);
  });

  it("draws a placeholder, not a frame, when there is no address or a refused one", () => {
    for (const url of [undefined, "", "javascript:alert(1)", "http://localhost:9820/"]) {
      const { unmount } = render(<WidgetView widget={web(url)} />);
      expect(shadow().querySelector("iframe")).toBeNull();
      expect(shadow().querySelector(".ms-placeholder")).not.toBeNull();
      unmount();
    }
  });

  it("shows only the host name when not live", () => {
    render(<WidgetView widget={web("https://example.org/chat?token=secret")} webLive={false} />);

    expect(shadow().querySelector("iframe")).toBeNull();
    expect(shadow().textContent).toContain("example.org");
    expect(shadow().textContent).not.toContain("secret");
  });

  it("shows the reason instead of the host name when web pages are turned off", () => {
    render(<WidgetView widget={web("https://example.org/chat")} webLive={false} webTexts={{ off: "Web pages are off" }} />);

    expect(shadow().querySelector("iframe")).toBeNull();
    expect(shadow().textContent).toContain("Web pages are off");
    expect(shadow().textContent).not.toContain("example.org");
  });

  it("switches off pointer events in the editor", () => {
    render(<WidgetView widget={web("https://example.org/")} webInteractive={false} />);

    expect((shadow().querySelector("iframe") as HTMLIFrameElement).style.pointerEvents).toBe("none");
  });

  it("shows a device's own address instead of the widget's, and goes back on an empty one", () => {
    const { rerender } = render(<WidgetView widget={web("https://example.org/a")} webUrl="https://example.org/b" />);
    expect(shadow().querySelector("iframe")!.getAttribute("src")).toBe("https://example.org/b");

    rerender(<WidgetView widget={web("https://example.org/a")} webUrl="" />);
    expect(shadow().querySelector("iframe")!.getAttribute("src")).toBe("https://example.org/a");
  });

  it("refuses a device address that fails the rule and keeps nothing loaded", () => {
    render(<WidgetView widget={web("https://example.org/a")} webUrl="javascript:alert(1)" />);

    expect(shadow().querySelector("iframe")).toBeNull();
  });

  it("makes a new iframe for a reload, so the sandbox is read again", () => {
    const { rerender } = render(<WidgetView widget={web("https://example.org/a")} webReload={0} />);
    const first = shadow().querySelector("iframe");

    rerender(<WidgetView widget={web("https://example.org/a")} webReload={1} />);

    expect(shadow().querySelector("iframe")).not.toBe(first);
  });

  it("draws a placeholder with a button instead of the page when it is blocked, and the button calls back", () => {
    let taps = 0;
    render(<WidgetView widget={web("https://example.org/chat")} webBlocked={{ text: "example.org", action: "Tap to load", onAction: () => void taps++ }} />);

    expect(shadow().querySelector("iframe")).toBeNull();
    expect(shadow().textContent).toContain("example.org");
    (shadow().querySelector("button") as HTMLButtonElement).click();
    expect(taps).toBe(1);
  });
});
