export interface ConfirmRequest {
  kind: "confirm";
  message: string;
  title?: string;
  danger?: boolean;
  resolve: (value: boolean) => void;
}

export interface PromptRequest {
  kind: "prompt";
  message: string;
  title?: string;
  defaultValue: string;
  resolve: (value: string | null) => void;
}

export interface AlertRequest {
  kind: "alert";
  message: string;
  title?: string;
  resolve: () => void;
}

export type DialogRequest = ConfirmRequest | PromptRequest | AlertRequest;

let listener: ((request: DialogRequest | null) => void) | null = null;

/** Module-level pub-sub so plain (non-component) code like useEditorState.ts can await a dialog
 * without needing to be a component itself — only DialogHost (mounted once near the app root)
 * subscribes and actually renders the modal. */
export function subscribe(fn: (request: DialogRequest | null) => void): () => void {
  listener = fn;
  return () => {
    if (listener === fn) listener = null;
  };
}

/** Replaces window.confirm with an app-styled modal (see docs/ui-guidelines.md: no native browser chrome). */
export function confirmAsync(message: string, opts?: { title?: string; danger?: boolean }): Promise<boolean> {
  return new Promise((resolve) => {
    listener?.({ kind: "confirm", message, title: opts?.title, danger: opts?.danger, resolve });
  });
}

/** Replaces window.prompt with an app-styled modal. Resolves null on cancel, matching window.prompt. */
export function promptAsync(message: string, defaultValue = "", opts?: { title?: string }): Promise<string | null> {
  return new Promise((resolve) => {
    listener?.({ kind: "prompt", message, title: opts?.title, defaultValue, resolve });
  });
}

/** Replaces window.alert with an app-styled modal — a single-button notice (e.g. "renamed on import"). */
export function alertAsync(message: string, opts?: { title?: string }): Promise<void> {
  return new Promise((resolve) => {
    listener?.({ kind: "alert", message, title: opts?.title, resolve });
  });
}
