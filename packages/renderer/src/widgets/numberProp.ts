export function numberProp(props: Record<string, unknown> | undefined, key: string, fallback: number): number {
  const value = props?.[key];
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
