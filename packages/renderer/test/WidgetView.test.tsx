import { render, screen } from "@testing-library/react";
import { fireEvent } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WidgetView } from "../src/widgets/WidgetView";
import type { Widget } from "../src/types";

function button(overrides: Partial<Widget> = {}): Widget {
  return { id: "w1", type: "button", x: 0, y: 0, w: 1, h: 1, text: "Kopyala", actions: {}, ...overrides };
}

describe("WidgetView", () => {
  it("renders its text inside a shadow root", () => {
    render(<WidgetView widget={button()} />);

    const host = document.querySelector('[data-ms-widget-host]') as HTMLElement;
    expect(host.shadowRoot).not.toBeNull();
    expect(host.shadowRoot!.textContent).toContain("Kopyala");
  });

  it("prefers liveText over the widget's own text", () => {
    render(<WidgetView widget={button()} liveText="12:00:00" />);

    const host = document.querySelector('[data-ms-widget-host]') as HTMLElement;
    expect(host.shadowRoot!.textContent).toContain("12:00:00");
  });

  it("calls onPress/onRelease for an interactive widget", () => {
    const onPress = vi.fn();
    const onRelease = vi.fn();
    render(<WidgetView widget={button()} onPress={onPress} onRelease={onRelease} />);

    const host = document.querySelector('[data-ms-widget-host]') as HTMLElement;
    fireEvent.pointerDown(host, { pointerId: 1 });
    fireEvent.pointerUp(host, { pointerId: 1 });

    expect(onPress).toHaveBeenCalledTimes(1);
    expect(onRelease).toHaveBeenCalledTimes(1);
  });

  it("does not attach press handlers for a label widget", () => {
    const onPress = vi.fn();
    render(<WidgetView widget={button({ type: "label" })} onPress={onPress} />);

    const host = document.querySelector('[data-ms-widget-host]') as HTMLElement;
    fireEvent.pointerDown(host, { pointerId: 1 });

    expect(onPress).not.toHaveBeenCalled();
  });

  it("reflects liveActive as the data-active attribute for :host([data-active]) styling", () => {
    render(<WidgetView widget={button({ type: "toggle" })} liveActive />);

    const host = document.querySelector('[data-ms-widget-host]') as HTMLElement;
    expect(host.getAttribute("data-active")).toBe("");
  });

  it("injects sanitized customCss into the shadow root but strips forbidden properties", () => {
    render(<WidgetView widget={button({ customCss: ":host { width: 999px; background: gold; }" })} />);

    const host = document.querySelector('[data-ms-widget-host]') as HTMLElement;
    const styleTags = Array.from(host.shadowRoot!.querySelectorAll("style")).map((s) => s.textContent).join("\n");

    expect(styleTags).not.toContain("999px");
    expect(styleTags).toContain("background: gold");
  });
});
