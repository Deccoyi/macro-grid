import { fireEvent, render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { WidgetView } from "../src/widgets/WidgetView";
import type { Widget } from "../src/types";

function button(overrides: Partial<Widget> = {}): Widget {
  return { id: "w1", type: "button", x: 0, y: 0, w: 1, h: 1, text: "Kopyala", actions: {}, ...overrides };
}

describe("WidgetView", () => {
  const tintable = "data:image/svg+xml;utf8," + encodeURIComponent('<svg data-mg-tint="1" viewBox="0 0 24 24"><path d="M0 0h24v24H0z"/></svg>');
  const baked = "data:image/svg+xml;utf8," + encodeURIComponent('<svg viewBox="0 0 24 24" color="#ff0000"><path d="M0 0h24v24H0z"/></svg>');

  it("paints a recolourable icon with the text colour as a mask", () => {
    render(<WidgetView widget={button({ style: { icon: tintable } })} />);

    const host = document.querySelector("[data-ms-widget-host]") as HTMLElement;
    const tinted = host.shadowRoot!.querySelector(".ms-icon-tint") as HTMLElement;
    expect(tinted.style.backgroundColor).toBe("currentcolor");
    expect(tinted.style.maskImage || tinted.style.webkitMaskImage).toContain("data:image/svg+xml");
    expect(host.shadowRoot!.querySelector("img.ms-icon")).toBeNull();
  });

  it("follows a dynamic icon rule result and keeps an old baked icon as an image", () => {
    const { rerender } = render(<WidgetView widget={button({ style: { icon: baked } })} />);
    const host = () => document.querySelector("[data-ms-widget-host]") as HTMLElement;
    expect(host().shadowRoot!.querySelector("img.ms-icon")).not.toBeNull();

    rerender(<WidgetView widget={button({ style: { icon: baked } })} liveStyle={{ icon: tintable }} />);
    expect(host().shadowRoot!.querySelector(".ms-icon-tint")).not.toBeNull();
  });

  it("fades an unavailable widget and shows a tappable mark that names the sources", () => {
    const onTap = vi.fn();
    const { getByRole } = render(<WidgetView widget={button()} unavailable={["obs.streaming"]} onUnavailableTap={onTap} unavailableLabel="Unavailable" />);

    const host = document.querySelector("[data-ms-widget-host]") as HTMLElement;
    expect(Number(host.style.opacity)).toBeLessThan(1);
    fireEvent.click(getByRole("button", { name: "Unavailable" }));
    expect(onTap).toHaveBeenCalledWith(["obs.streaming"]);
  });

  it("does not run a press on an unavailable widget, greys it, and names the sources instead", () => {
    const onPress = vi.fn();
    const onRelease = vi.fn();
    const onTap = vi.fn();
    render(<WidgetView widget={button()} unavailable={["obs.streaming"]} onUnavailableTap={onTap} onPress={onPress} onRelease={onRelease} />);

    const host = document.querySelector("[data-ms-widget-host]") as HTMLElement;
    expect(host.style.filter).toContain("grayscale");
    fireEvent.pointerDown(host);
    fireEvent.pointerUp(host);
    expect(onPress).not.toHaveBeenCalled();
    expect(onRelease).not.toHaveBeenCalled();
    expect(onTap).toHaveBeenCalledWith(["obs.streaming"]);
  });

  it("still runs a press when the app cannot show the notice (no tap handler)", () => {
    const onPress = vi.fn();
    render(<WidgetView widget={button()} unavailable={["obs.streaming"]} onPress={onPress} />);

    fireEvent.pointerDown(document.querySelector("[data-ms-widget-host]") as HTMLElement);
    expect(onPress).toHaveBeenCalled();
  });

  it("draws no mark and no fade when nothing is unavailable", () => {
    const { queryByRole } = render(<WidgetView widget={button()} unavailable={[]} onUnavailableTap={vi.fn()} />);

    const host = document.querySelector("[data-ms-widget-host]") as HTMLElement;
    expect(host.style.opacity).toBe("");
    expect(queryByRole("button")).toBeNull();
  });

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
