import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Grid, gridArea } from "../src/grid/Grid";
import type { Page } from "../src/types";

describe("gridArea", () => {
  it("converts 0-based x/y/w/h into a 1-based CSS grid-area", () => {
    expect(gridArea({ x: 0, y: 0, w: 1, h: 1 })).toBe("1 / 1 / span 1 / span 1");
    expect(gridArea({ x: 2, y: 1, w: 2, h: 2 })).toBe("2 / 3 / span 2 / span 2");
  });
});

describe("Grid", () => {
  const page: Pick<Page, "cols" | "rows" | "widgets"> = {
    cols: 4,
    rows: 2,
    widgets: [
      { id: "a", type: "button", x: 0, y: 0, w: 1, h: 1, text: "A", actions: {} },
      { id: "b", type: "button", x: 2, y: 1, w: 2, h: 1, text: "B", actions: {} },
    ],
  };

  it("renders one wrapper per widget, positioned by grid-area", () => {
    render(<Grid page={page} renderWidget={(w) => <span data-testid={`content-${w.id}`}>{w.text}</span>} />);

    const a = screen.getByTestId("content-a").parentElement!;
    const b = screen.getByTestId("content-b").parentElement!;

    expect(a.style.gridArea).toBe("1 / 1 / span 1 / span 1");
    expect(b.style.gridArea).toBe("2 / 3 / span 1 / span 2");
  });

  it("sets grid-template-columns/rows from page.cols/rows", () => {
    const { container } = render(<Grid page={page} renderWidget={() => null} />);

    const gridEl = container.firstElementChild as HTMLElement;
    expect(gridEl.style.gridTemplateColumns).toBe("repeat(4, 1fr)");
    expect(gridEl.style.gridTemplateRows).toBe("repeat(2, 1fr)");
  });
});
