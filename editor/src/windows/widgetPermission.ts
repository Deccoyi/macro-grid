const OPTIONS = ["keepLoaded", "storage", "notifications"];

/** For a permission such as `widget:gauge:storage` (a widget option the person approves), the option; otherwise null. */
export function widgetOptionOf(permission: string): "keepLoaded" | "storage" | "notifications" | null {
  const parts = permission.split(":");
  const option = parts[0] === "widget" && parts.length === 3 ? parts[2] : undefined;
  return option && OPTIONS.includes(option) ? (option as "keepLoaded" | "storage" | "notifications") : null;
}
