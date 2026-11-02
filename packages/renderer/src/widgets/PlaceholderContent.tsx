export interface PlaceholderContentProps {
  label: string;
}

/**
 * Stand-in for widget types whose real content is rendered above the shared renderer
 * (a "plugin-html" widget's sandboxed iframe) and for a removed "web" widget.
 * Keeps the grid cell visible and correctly sized.
 */
export function PlaceholderContent({ label }: PlaceholderContentProps) {
  return (
    <div className="ms-content ms-placeholder">
      <span className="ms-text">{label}</span>
    </div>
  );
}
