import { createContext, useContext, useEffect, useRef, type ReactNode } from "react";
import type { Command } from "./types";

interface CommandsApi {
  register: (id: string, cmd: Command) => void;
  unregister: (id: string) => void;
  get: (id: string) => Command | undefined;
  all: () => Command[];
}

const CommandsContext = createContext<CommandsApi | null>(null);

/** Holds the command registry (docs/design/editor-edit-commands.md, "Command registry") — a plain Map in
 * a ref, not React state: registering a command must never itself trigger a re-render, and every reader
 * (the Edit menu, ShortcutListener, a context menu) reads it fresh when it needs it. */
export function CommandsProvider({ children }: { children: ReactNode }) {
  const registryRef = useRef<Map<string, Command>>(new Map());
  const apiRef = useRef<CommandsApi>({
    register: (id, cmd) => { registryRef.current.set(id, cmd); },
    unregister: (id) => { registryRef.current.delete(id); },
    get: (id) => registryRef.current.get(id),
    all: () => [...registryRef.current.values()],
  });
  return <CommandsContext.Provider value={apiRef.current}>{children}</CommandsContext.Provider>;
}

export function useCommands(): CommandsApi {
  const ctx = useContext(CommandsContext);
  if (!ctx) throw new Error("useCommands() used outside <CommandsProvider>");
  return ctx;
}

/** Registers `commands` for as long as the calling component stays mounted. Runs on every render (a
 * cheap Map.set) so each command's `enabled`/`run` closures always see the latest state — features
 * register their commands with this rather than touching the registry directly. */
export function useRegisterCommands(commands: Command[]): void {
  const { register, unregister } = useCommands();
  useEffect(() => {
    for (const c of commands) register(c.id, c);
    return () => { for (const c of commands) unregister(c.id); };
  });
}
