import { describe, expect, it } from "vitest";
import type { SettingField } from "../src/api/types";
import { layoutFields, segmentedAsSelect } from "../src/panels/actionForms/schemaFormLayout";

const f = (key: string, kind: SettingField["kind"], extra: Partial<SettingField> = {}): SettingField => ({ key, label: key, kind, ...extra });
const shape = (fields: SettingField[]) =>
  layoutFields(fields).map((i) => (i.type === "field" ? i.field.key : `${i.type}:${i.fields.map((x) => x.key).join("+")}`));

describe("layoutFields", () => {
  it("puts consecutive shareable fields into two-column grids in order", () => {
    expect(shape([f("a", "Number"), f("b", "Color"), f("c", "Select"), f("d", "Number")])).toEqual(["grid:a+b", "grid:c+d"]);
  });

  it("leaves an odd field on its own at the end of its run", () => {
    expect(shape([f("a", "Number"), f("b", "Number"), f("c", "Number")])).toEqual(["grid:a+b", "c"]);
  });

  it("breaks a run at a field that cannot share a row", () => {
    expect(shape([f("a", "Number"), f("t", "Text"), f("b", "Number"), f("c", "Number")])).toEqual(["a", "t", "grid:b+c"]);
  });

  it("groups dependent fields into one indented block after their parent", () => {
    expect(shape([f("on", "Bool"), f("x", "Number", { visibleWhen: "on=true" }), f("y", "Text", { visibleWhen: "on=true" })])).toEqual(["on", "nest:x+y"]);
  });

  it("draws a Segmented field with more than three options as a Select", () => {
    const options = ["1", "2", "3", "4"].map((v) => ({ value: v, label: v }));
    expect(segmentedAsSelect(f("s", "Segmented", { options }))).toBe(true);
    expect(segmentedAsSelect(f("s", "Segmented", { options: options.slice(0, 3) }))).toBe(false);
    expect(segmentedAsSelect(f("s", "Segmented", { options: options.slice(0, 2), optionsSource: "devices" }))).toBe(true);
  });
});
