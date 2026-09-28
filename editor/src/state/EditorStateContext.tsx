import { createContext, useContext, type ReactNode } from "react";
import { useEditorState } from "./useEditorState";

export type EditorState = ReturnType<typeof useEditorState>;

const EditorStateContext = createContext<EditorState | null>(null);

/** Wraps `useEditorState()`'s result so tool windows can read it without App.tsx prop-drilling
 * them in — adding a tool window then never needs an App.tsx edit (docking-workspace.md). */
export function EditorStateProvider({ value, children }: { value: EditorState; children: ReactNode }) {
  return <EditorStateContext.Provider value={value}>{children}</EditorStateContext.Provider>;
}

export function useEditorStateContext(): EditorState {
  const value = useContext(EditorStateContext);
  if (!value) throw new Error("useEditorStateContext() used outside <EditorStateProvider>");
  return value;
}
