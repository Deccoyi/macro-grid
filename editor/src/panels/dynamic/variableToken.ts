import type { VariableInfo } from "../../api/types";

/** The text a picker inserts for a variable. A plugin's `example` is only a sample: it may be a ready token
 * ("{myplugin.bitrate|0} kbps") or just a sample value ("true"). Only a real token of this variable is used; anything else
 * becomes the plain "{name}", so a text always keeps following the variable instead of freezing a copied value. */
export function variableToken(v: Pick<VariableInfo, "name" | "example">): string {
  const ex = v.example ?? "";
  return ex.includes(`{${v.name}}`) || ex.includes(`{${v.name}|`) ? ex : `{${v.name}}`;
}
