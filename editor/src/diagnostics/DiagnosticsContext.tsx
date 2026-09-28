import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import type { Diagnostic } from "./types";

interface DiagnosticsApi {
  diagnostics: Diagnostic[];
  /** Replaces everything `source` reported before — how a validator re-run works. */
  report: (source: string, list: Diagnostic[]) => void;
  clear: (source?: string) => void;
}

const DiagnosticsContext = createContext<DiagnosticsApi | null>(null);

/** Error List's data, in memory only. No producer exists yet (docking-workspace-plan.md, "Error List"):
 * nothing calls report() today, so the panel always starts empty. Do not invent validation to fill it. */
export function DiagnosticsProvider({ children }: { children: ReactNode }) {
  const [bySource, setBySource] = useState<Record<string, Diagnostic[]>>({});

  const report = useCallback((source: string, list: Diagnostic[]) => {
    setBySource((prev) => ({ ...prev, [source]: list }));
  }, []);

  const clear = useCallback((source?: string) => {
    setBySource((prev) => (source ? { ...prev, [source]: [] } : {}));
  }, []);

  const diagnostics = useMemo(() => Object.values(bySource).flat(), [bySource]);

  const value = useMemo(() => ({ diagnostics, report, clear }), [diagnostics, report, clear]);
  return <DiagnosticsContext.Provider value={value}>{children}</DiagnosticsContext.Provider>;
}

export function useDiagnostics(): DiagnosticsApi {
  const value = useContext(DiagnosticsContext);
  if (!value) throw new Error("useDiagnostics() used outside <DiagnosticsProvider>");
  return value;
}
