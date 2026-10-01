import type { ActionBinding } from "@macro/renderer";

/** The logic steps of an action list. The list stays flat; an If, its Otherwise and its End are three rows that the editor draws as one block. */
export const FLOW_IF = "core.if";
export const FLOW_ELSE = "core.else";
export const FLOW_END = "core.endIf";
export const FLOW_STOP = "core.stop";

/** How many Ifs may sit inside each other. */
export const MAX_DEPTH = 3;

export type RowKind = "if" | "else" | "end" | "step";

export interface FlowRow {
  index: number;
  kind: RowKind;
  /** How far the row is indented (an If, its Otherwise and its End share a depth; the steps between them are one deeper). */
  depth: number;
  /** For an If: the index of its Otherwise and of its End, when it has them. */
  elseIndex?: number;
  endIndex?: number;
  /** An Otherwise or End with no If to belong to (a list edited by hand). */
  stray?: boolean;
}

const kindOf = (type: string): RowKind =>
  type === FLOW_IF ? "if" : type === FLOW_ELSE ? "else" : type === FLOW_END ? "end" : "step";

/** Works out the depth of every row and which Otherwise and End belong to which If. Mirrors how the server reads the list. */
export function layout(bindings: readonly ActionBinding[]): FlowRow[] {
  const rows: FlowRow[] = [];
  const open: FlowRow[] = [];
  bindings.forEach((b, index) => {
    const kind = kindOf(b.type);
    const row: FlowRow = { index, kind, depth: open.length };
    if (kind === "if") {
      open.push(row);
    } else if (kind === "else") {
      const owner = open[open.length - 1];
      if (owner && owner.elseIndex === undefined) { owner.elseIndex = index; row.depth = open.length - 1; } else row.stray = true;
    } else if (kind === "end") {
      const owner = open.pop();
      if (owner) { owner.endIndex = index; row.depth = open.length; } else row.stray = true;
    }
    rows.push(row);
  });
  return rows;
}

/** True when every Otherwise and End has its If, every If has its End, and no block is nested deeper than <see cref="MAX_DEPTH"/>. */
export function isWellFormed(bindings: readonly ActionBinding[]): boolean {
  let open = 0;
  const hasElse: boolean[] = [];
  for (const b of bindings) {
    if (b.type === FLOW_IF) {
      if (open >= MAX_DEPTH) return false;
      open++;
      hasElse.push(false);
    } else if (b.type === FLOW_ELSE) {
      if (open === 0 || hasElse[open - 1]) return false;
      hasElse[open - 1] = true;
    } else if (b.type === FLOW_END) {
      if (open === 0) return false;
      open--;
      hasElse.pop();
    }
  }
  return open === 0;
}

/** Adds an If and its End together at the end of the list. Refused (the list comes back unchanged) when it would nest too deep. */
export function addIf(bindings: readonly ActionBinding[]): ActionBinding[] {
  const next = [...bindings, { type: FLOW_IF, settings: { when: "condition" } }, { type: FLOW_END, settings: {} }];
  return isWellFormed(bindings) && !isWellFormed(next) ? [...bindings] : next;
}

/** Adds the Otherwise of the If at <paramref name="ifIndex"/>, just before its End. Does nothing when that If already has one. */
export function addElse(bindings: readonly ActionBinding[], ifIndex: number): ActionBinding[] {
  const row = layout(bindings)[ifIndex];
  if (!row || row.kind !== "if" || row.elseIndex !== undefined) return [...bindings];
  const at = row.endIndex ?? bindings.length;
  return [...bindings.slice(0, at), { type: FLOW_ELSE, settings: {} }, ...bindings.slice(at)];
}

/** Removes a row. An If takes its Otherwise and End with it and leaves the steps inside; an End never goes alone (remove its If). */
export function remove(bindings: readonly ActionBinding[], index: number): ActionBinding[] {
  const row = layout(bindings)[index];
  if (!row || (row.kind === "end" && !row.stray)) return [...bindings];
  const gone = new Set([index]);
  if (row.kind === "if") {
    if (row.elseIndex !== undefined) gone.add(row.elseIndex);
    if (row.endIndex !== undefined) gone.add(row.endIndex);
  }
  return bindings.filter((_, i) => !gone.has(i));
}

/**
 * Moves a step, or a whole If block, one place up or down. An Otherwise or End never moves on its own. A move that would leave the
 * list not well formed (or nested too deep) is refused: the same list comes back.
 */
export function move(bindings: readonly ActionBinding[], index: number, dir: -1 | 1): ActionBinding[] {
  const rows = layout(bindings);
  const row = rows[index];
  if (!row || row.kind === "else" || row.kind === "end") return bindings as ActionBinding[];
  const from = index;
  const to = row.kind === "if" ? row.endIndex : index;
  if (to === undefined) return bindings as ActionBinding[];

  // The unit next to it: one row, or a whole block when the row beside is the End of one.
  let otherFrom: number;
  let otherTo: number;
  if (dir === -1) {
    if (from === 0) return bindings as ActionBinding[];
    otherTo = from - 1;
    otherFrom = row.kind === "if" && rows[otherTo]!.kind === "end" ? blockStart(rows, otherTo) : otherTo;
  } else {
    if (to === bindings.length - 1) return bindings as ActionBinding[];
    otherFrom = to + 1;
    otherTo = row.kind === "if" && rows[otherFrom]!.kind === "if" ? (rows[otherFrom]!.endIndex ?? otherFrom) : otherFrom;
  }

  const [firstFrom, firstTo, secondFrom, secondTo] = dir === -1 ? [otherFrom, otherTo, from, to] : [from, to, otherFrom, otherTo];
  const next = [...bindings.slice(0, firstFrom), ...bindings.slice(secondFrom, secondTo + 1), ...bindings.slice(firstFrom, firstTo + 1), ...bindings.slice(secondTo + 1)];
  return isWellFormed(next) || !isWellFormed(bindings) ? next : (bindings as ActionBinding[]);
}

function blockStart(rows: FlowRow[], endIndex: number): number {
  const owner = rows.find((r) => r.kind === "if" && r.endIndex === endIndex);
  return owner ? owner.index : endIndex;
}
