import { useEffect, useState } from "react";
import { offPlugins, subscribe } from "../grid/widgetCrashGuard";

/** The plugins whose widget previews are off after a crash of the editor's web view; follows the person turning one back on. */
export function useOffPlugins(): ReadonlySet<string> {
  const [off, setOff] = useState(offPlugins);
  useEffect(() => subscribe(() => setOff(offPlugins())), []);
  return off;
}
