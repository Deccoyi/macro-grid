export interface PlaceholderContentProps {
  label: string;
}

/**
 * Stand-in for widget types whose real content is rendered above the shared renderer
 * (a "web" widget's iframe/native WebView overlay, a "plugin-html" widget's sandboxed iframe).
 * Keeps the grid cell visible and correctly sized while that layer isn't wired up yet.
 */
export function PlaceholderContent({ label }: PlaceholderContentProps) {
  return (
    <div className="ms-content ms-placeholder">
      <span className="ms-text">{label}</span>
    </div>
  );
}
