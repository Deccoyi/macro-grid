import { createContext, useContext, type ReactNode } from "react";
import { useProfileTree, type ProfileTreeApi } from "./useProfileTree";

/** One shared `useProfileTree()` instance for both the Hierarchy tree (which renders it) and App.tsx's
 * global Ctrl+C/Ctrl+V handler (which needs to read/mutate the same tree for a profile or profile-folder
 * copy/paste) — a tool window's Content component takes no props, so this is the only way to hand it the
 * same instance rather than two independently-fetched copies going out of sync. */
const ProfileTreeContext = createContext<ProfileTreeApi | null>(null);

export function ProfileTreeProvider({ children }: { children: ReactNode }) {
  const value = useProfileTree();
  return <ProfileTreeContext.Provider value={value}>{children}</ProfileTreeContext.Provider>;
}

export function useProfileTreeContext(): ProfileTreeApi {
  const value = useContext(ProfileTreeContext);
  if (!value) throw new Error("useProfileTreeContext() used outside <ProfileTreeProvider>");
  return value;
}
