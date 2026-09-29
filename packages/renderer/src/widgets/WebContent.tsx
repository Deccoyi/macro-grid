import { isSafeWebUrl, webUrlHost } from "./webUrl";

/**
 * The iframe of a `web` widget. Every rule lives here, in the same words in every place that draws the widget
 * (editor, browser deck, phone app):
 *
 * - `sandbox` has no `allow-popups`, `allow-downloads`, `allow-top-navigation*`, `allow-modals`,
 *   `allow-orientation-lock`, `allow-pointer-lock` or `allow-presentation`, so the page cannot open windows, start
 *   downloads, navigate the app away, show dialogs or take over the screen. `allow-scripts` and `allow-same-origin`
 *   stay (most pages do not work without them; the page keeps its own origin, not ours) and `allow-forms` so its input works.
 * - `allow=""` is an empty permissions policy: no camera, microphone, location, clipboard, payment, USB.
 * - `referrerpolicy="no-referrer"`: the site is not told which server the person has.
 *
 * The attributes are set once, when the iframe is created, and never change: a changed `sandbox` would only apply on
 * the next load. A new address or a reload remounts the iframe (see `key`).
 */
export const WEB_SANDBOX = "allow-scripts allow-same-origin allow-forms";

export interface WebContentProps {
  /** The address to show; refused (and a placeholder drawn) unless it passes `isSafeWebUrl`. */
  url: string | undefined;
  /** A higher number than before loads the page again, even for the same address. */
  reload?: number;
  /** False in the editor, so selecting and dragging the widget still works over the page. */
  interactive?: boolean;
  /** False draws the placeholder with the host name instead of the page (another page of the grid, the app in the background, "Show web pages" off). */
  live?: boolean;
  /** Host names to refuse besides the built-in rule (the server's own address on the phone). */
  blockedHosts?: readonly string[];
  /** Replaces the default English words of the placeholder. */
  texts?: Partial<WebTexts>;
}

export interface WebTexts {
  /** No address set yet. */
  empty: string;
  /** The address was refused by the rule. */
  refused: string;
  /** Shown instead of the host name while the widget is not live and this says why (web pages turned off on this device). */
  off?: string;
}

const DEFAULT_TEXTS: WebTexts = { empty: "Web page", refused: "This web address is not allowed" };

export function WebContent({ url, reload = 0, interactive = true, live = true, blockedHosts, texts }: WebContentProps) {
  const words = { ...DEFAULT_TEXTS, ...texts };

  if (url === undefined || url === "")
    return (
      <div className="ms-content ms-placeholder">
        <span className="ms-text">{words.empty}</span>
      </div>
    );

  if (!isSafeWebUrl(url, blockedHosts))
    return (
      <div className="ms-content ms-placeholder">
        <span className="ms-text">{words.refused}</span>
      </div>
    );

  if (!live)
    return (
      <div className="ms-content ms-placeholder">
        <span className="ms-text">{words.off ?? webUrlHost(url)}</span>
      </div>
    );

  return (
    <iframe
      // A new address or reload counter is a new iframe: the safety attributes below are only read when it is created.
      key={`${reload}:${url}`}
      className="ms-web"
      src={url}
      sandbox={WEB_SANDBOX}
      allow=""
      referrerPolicy="no-referrer"
      loading="lazy"
      title={webUrlHost(url)}
      style={{ pointerEvents: interactive ? "auto" : "none" }}
    />
  );
}
