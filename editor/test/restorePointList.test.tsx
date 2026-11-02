import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { RestorePointInfo } from "../src/api/types";
import { RestorePointList, formatPointSize } from "../src/dialogs/RestorePointList";

vi.mock("../src/i18n/I18nContext", () => ({ useT: () => ({ t: (key: string) => key }) }));

const points: RestorePointInfo[] = [
  { fileName: "b.mgbackup", createdAt: "2026-10-30T10:00:00Z", reason: "delete", size: 2048 },
  { fileName: "a.mgbackup", createdAt: "2026-10-01T10:00:00Z", reason: "mystery", size: 500 },
];

describe("RestorePointList", () => {
  it("lists the points in the order given with reason and size, and keeps the picked name", () => {
    const chosen = { name: "b.mgbackup" as string | null };
    render(<RestorePointList points={points} chosen={chosen} />);

    const radios = screen.getAllByRole("radio");
    expect(radios).toHaveLength(2);
    expect(screen.getByText("2 KB")).toBeTruthy();
    expect(screen.getByText("mystery")).toBeTruthy();
    expect(screen.getByText("restore.point.reason.delete")).toBeTruthy();

    fireEvent.click(radios[1]!);
    expect(chosen.name).toBe("a.mgbackup");
  });

  it("formats sizes", () => {
    expect(formatPointSize(10)).toBe("10 B");
    expect(formatPointSize(3 * 1024 * 1024)).toBe("3.0 MB");
  });
});
