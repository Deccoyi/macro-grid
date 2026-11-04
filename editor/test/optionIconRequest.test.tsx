import { fireEvent, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ButtonContent, setIconUrlResolver } from "@macro/renderer";
import { RETRY_DELAYS_MS, iconSrc, isImageMissing, requestPluginImage, subscribeImages } from "../src/api/optionIcon";
import { OptionIcon } from "../src/panels/actionForms/SearchSelect";

const fetchMock = vi.fn((_url: string) => Promise.resolve(new Response(null, { status: 200 })));

beforeEach(() => { vi.stubGlobal("fetch", fetchMock); setIconUrlResolver(iconSrc, subscribeImages); });
afterEach(() => { fetchMock.mockReset(); fetchMock.mockImplementation(() => Promise.resolve(new Response(null, { status: 200 }))); vi.unstubAllGlobals(); });

describe("plugin picture requests", () => {
  it("browsing renders a plain image URL and never calls fetch", () => {
    const { container } = render(<OptionIcon icon="plugin-image:p1/browse" />);
    expect(container.querySelector("img")?.getAttribute("src")).toBe("/api/plugins/p1/images/browse");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("remembers a 404 and renders nothing for that picture again", () => {
    const icon = "plugin-image:p1/gone";
    const first = render(<OptionIcon icon={icon} />);
    fireEvent.error(first.container.querySelector("img")!);
    expect(first.container.querySelector("img")).toBeNull();
    expect(isImageMissing(icon)).toBe(true);
    first.unmount();
    const second = render(<OptionIcon icon={icon} />);
    expect(second.container.querySelector("img")).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("requestPluginImage sends ?request=1 exactly once per reference", () => {
    const icon = "plugin-image:p1/pick";
    requestPluginImage(icon);
    requestPluginImage(icon);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith("/api/plugins/p1/images/pick?request=1");
  });

  it("ignores non-picture icons and failed requests", async () => {
    requestPluginImage("mic");
    requestPluginImage(null);
    expect(fetchMock).not.toHaveBeenCalled();
    fetchMock.mockImplementationOnce(() => Promise.reject(new Error("offline")));
    requestPluginImage("plugin-image:p1/fail");
    await Promise.resolve();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});

describe("pictures that arrive after the request", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  const missingThenOk = (misses: number) => {
    let n = 0;
    fetchMock.mockImplementation(() => Promise.resolve(new Response(null, { status: n++ < misses ? 404 : 200 })));
  };

  it("retries after a 404 and shows the thumbnail and button icon without reopening", async () => {
    const icon = "plugin-image:p2/late";
    missingThenOk(2);
    const thumb = render(<OptionIcon icon={icon} />);
    fireEvent.error(thumb.container.querySelector("img")!); // the browse load missed
    expect(thumb.container.querySelector("img")).toBeNull();
    requestPluginImage(icon);
    const button = render(<ButtonContent text="x" icon={icon} />);
    await vi.advanceTimersByTimeAsync(0);
    await vi.advanceTimersByTimeAsync(500);
    await vi.advanceTimersByTimeAsync(1000);
    expect(fetchMock.mock.calls.map((c) => c[0])).toEqual([
      "/api/plugins/p2/images/late?request=1", "/api/plugins/p2/images/late", "/api/plugins/p2/images/late",
    ]);
    expect(thumb.container.querySelector("img")?.getAttribute("src")).toBe("/api/plugins/p2/images/late?v=1");
    expect(button.container.querySelector("img.ms-icon")?.getAttribute("src")).toBe("/api/plugins/p2/images/late?v=1");
  });

  it("stops after about ten seconds", async () => {
    missingThenOk(1000);
    requestPluginImage("plugin-image:p2/never");
    await vi.advanceTimersByTimeAsync(60000);
    expect(fetchMock).toHaveBeenCalledTimes(1 + RETRY_DELAYS_MS.length);
    expect(RETRY_DELAYS_MS.reduce((a, b) => a + b, 0)).toBeLessThanOrEqual(11000);
  });

  it("a stored picture that is not cached is watched with plain GETs, never with the request flag", async () => {
    missingThenOk(1);
    const { container } = render(<ButtonContent text="x" icon="plugin-image:p2/stored" />);
    await vi.advanceTimersByTimeAsync(600);
    expect(fetchMock.mock.calls.map((c) => c[0])).toEqual(["/api/plugins/p2/images/stored", "/api/plugins/p2/images/stored"]);
    expect(container.querySelector("img.ms-icon")?.getAttribute("src")).toBe("/api/plugins/p2/images/stored?v=1");
  });
});
