import { useEffect, useState } from "react";
import { api } from "../api/client";

let cached: string | null = null;

/** The server's version for the Help menu and window; empty until the server has answered. */
export function useServerVersion(): string {
  const [version, setVersion] = useState(cached ?? "");
  useEffect(() => {
    if (cached) return;
    api.getVersion().then((v) => { cached = v.version; setVersion(v.version); }).catch(() => {});
  }, []);
  return version;
}
