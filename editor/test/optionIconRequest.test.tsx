import { fireEvent, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { isImageMissing, requestPluginImage } from "../src/api/optionIcon";
import { OptionIcon } from "../src/panels/actionForms/SearchSelect";

const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 200 })));

beforeEach(() => vi.stubGlobal("fetch", fetchMock));
afterEach(() => { fetchMock.mockClear(); vi.unstubAllGlobals(); });

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
