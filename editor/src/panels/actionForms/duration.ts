export type DurationUnit = "ms" | "s" | "min";

const FACTOR: Record<DurationUnit, number> = { ms: 1, s: 1000, min: 60000 };

/** The unit a stored number of milliseconds reads best in: minutes for whole minutes, seconds from a second up to a tenth, else milliseconds. */
export function splitDuration(ms: number): { amount: number; unit: DurationUnit } {
  if (ms !== 0 && ms % 60000 === 0) return { amount: ms / 60000, unit: "min" };
  if (ms >= 1000 && ms % 100 === 0) return { amount: ms / 1000, unit: "s" };
  return { amount: ms, unit: "ms" };
}

/** The whole milliseconds an amount in a unit stands for. */
export function joinDuration(amount: number, unit: DurationUnit): number {
  return Math.round(amount * FACTOR[unit]);
}

export const clampDuration = (ms: number, min?: number | null, max?: number | null): number => {
  let result = ms;
  if (max != null && result > max) result = max;
  if (min != null && result < min) result = min;
  return result;
};
