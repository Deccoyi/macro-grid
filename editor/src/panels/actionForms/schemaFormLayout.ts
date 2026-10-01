import type { SettingField } from "../../api/types";

/** What the Properties panel draws for a run of schema fields (see docs/ui/ui-guidelines.md, "Layout per field kind"). */
export type SchemaLayoutItem =
  | { type: "field"; field: SettingField }
  | { type: "grid"; fields: SettingField[] }
  | { type: "nest"; fields: SettingField[] };

/** Most options a Segmented field shows as segments; with more it is drawn as a Select. */
export const MAX_SEGMENTS = 3;

/** True when a Segmented field is drawn as a Select instead: its options come from a source or there are more than three of them. */
export function segmentedAsSelect(field: SettingField): boolean {
  return field.kind === "Segmented" && (Boolean(field.optionsSource) || (field.options?.length ?? 0) > MAX_SEGMENTS);
}

/** Kinds that can sit two to a row: Number, Color, Select and a Segmented field with few options. */
export function canShareRow(field: SettingField): boolean {
  return field.kind === "Number" || field.kind === "Color" || field.kind === "Select" || field.kind === "Segmented";
}

/**
 * Orders visible fields for drawing. A field that depends on another (`visibleWhen`) joins the run of dependent fields right after it and is drawn
 * indented. Consecutive fields that can share a row fill two-column grids in order; a field that is left over in its run is drawn on its own.
 */
export function layoutFields(fields: SettingField[]): SchemaLayoutItem[] {
  const items: SchemaLayoutItem[] = [];
  let run: SettingField[] = [];

  const flushRun = () => {
    for (let i = 0; i < run.length; i += 2) {
      const pair = run.slice(i, i + 2);
      items.push(pair.length === 2 ? { type: "grid", fields: pair } : { type: "field", field: pair[0]! });
    }
    run = [];
  };

  for (const field of fields) {
    if (field.visibleWhen) {
      flushRun();
      const last = items[items.length - 1];
      if (last?.type === "nest") last.fields.push(field);
      else items.push({ type: "nest", fields: [field] });
    } else if (canShareRow(field)) {
      run.push(field);
    } else {
      flushRun();
      items.push({ type: "field", field });
    }
  }
  flushRun();
  return items;
}
