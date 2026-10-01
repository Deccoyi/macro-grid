import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { api } from "../api/client";
import type { ServerProblem } from "../api/types";
import { onDiagnostic } from "./editorEvents";
import type { Diagnostic } from "./types";

interface DiagnosticsApi {
  diagnostics: Diagnostic[];
  /** Replaces everything `source` reported before — how a validator re-run works. */
  report: (source: string, list: Diagnostic[]) => void;
  /** Clears the editor's own lists and everything the server reported (all of it, or one source). */
  clear: (source?: string) => void;
}

const DiagnosticsContext = createContext<DiagnosticsApi | null>(null);

const POLL_MS = 2000;

function fromServer(p: ServerProblem): Diagnostic {
  return { id: p.id, source: p.source, sourceName: p.sourceName, severity: p.severity, code: p.code, message: p.message, count: p.count, origin: "server" };
}

/** Error List's data. The editor's own checks call report(); nothing does yet. What the server has seen go wrong (a plugin's refused key press,
 * a plugin that did not load) is fetched from /api/problems every couple of seconds and merged in, so a refusal shows up without anyone
 * opening a log file. In memory on both sides: a restart starts empty. */
export function DiagnosticsProvider({ children }: { children: ReactNode }) {
  const [bySource, setBySource] = useState<Record<string, Diagnostic[]>>({});
  const [serverList, setServerList] = useState<Diagnostic[]>([]);
  const serverVersion = useRef<number | null>(null);

  useEffect(() => {
    let stopped = false;
    const poll = () => {
      api.fetchProblems()
        .then((result) => {
          if (stopped || result.version === serverVersion.current) return;
          serverVersion.current = result.version;
          setServerList(result.problems.map(fromServer));
        })
        .catch(() => {});
    };
    poll();
    const timer = window.setInterval(poll, POLL_MS);
    return () => { stopped = true; window.clearInterval(timer); };
  }, []);

  // What the editor's own hooks notice (a widget that did not fit): the same message again is counted, not listed twice.
  useEffect(
    () =>
      onDiagnostic((d) =>
        setBySource((prev) => {
          const list = prev[d.source] ?? [];
          const existing = list.find((x) => x.id === d.id);
          const next = existing ? list.map((x) => (x.id === d.id ? { ...x, count: (x.count ?? 1) + 1 } : x)) : [...list, { ...d, count: 1 }];
          return { ...prev, [d.source]: next };
        }),
      ),
    [],
  );

  const report = useCallback((source: string, list: Diagnostic[]) => {
    setBySource((prev) => ({ ...prev, [source]: list }));
  }, []);

  const clear = useCallback((source?: string) => {
    setBySource((prev) => (source ? { ...prev, [source]: [] } : {}));
    setServerList((prev) => (source ? prev.filter((d) => d.source !== source) : []));
    api.clearProblems(source).catch(() => {});
  }, []);

  const diagnostics = useMemo(() => [...serverList, ...Object.values(bySource).flat()], [serverList, bySource]);

  const value = useMemo(() => ({ diagnostics, report, clear }), [diagnostics, report, clear]);
  return <DiagnosticsContext.Provider value={value}>{children}</DiagnosticsContext.Provider>;
}

export function useDiagnostics(): DiagnosticsApi {
  const value = useContext(DiagnosticsContext);
  if (!value) throw new Error("useDiagnostics() used outside <DiagnosticsProvider>");
  return value;
}
