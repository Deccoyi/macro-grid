import { useEffect } from "react";
import { useT } from "./I18nContext";
import type { DictKey } from "./tr";

/** Sets the page title to a dictionary entry; the desktop host copies it into the native window title bar
 * (see WebViewEnvironment.cs), so window titles follow the language preference. */
export function useDocumentTitle(key: DictKey) {
  const { t } = useT();
  useEffect(() => {
    document.title = t(key);
  }, [t, key]);
}
