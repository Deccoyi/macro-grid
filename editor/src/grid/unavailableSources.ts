import type { Widget } from "@macro/renderer";
import { evaluateWidgetDynamicText } from "./evaluateDynamic";

/**
 * The variables a widget's current text needs that have no value now and carry no placeholder: the editor preview's twin of the server's
 * DynamicText.UnavailableSources, so the preview fades the same widgets the phone does. A widget with `props.hideUnavailable` never fades.
 */
export function unavailableSources(widget: Widget, variables: Record<string, unknown>): string[] {
  if (widget.props?.hideUnavailable === true) return [];

  const names = new Set<string>();
  const text = evaluateWidgetDynamicText(widget, variables);
  if (text) {
    for (const match of text.matchAll(/\{\{|\}\}|\{([^{}|]+)(?:\|([^{}|]*))?(?:\|([^}]*))?\}/g)) {
      const name = match[1]?.trim();
      if (name && match[3] === undefined) names.add(name);
    }
  }
  const bound = widget.type === "slider" || widget.type === "knob" ? widget.props?.valueVariable : undefined;
  if (typeof bound === "string" && bound) names.add(bound);

  return [...names].filter((n) => !n.startsWith("self.") && (variables[n] === undefined || variables[n] === null));
}
