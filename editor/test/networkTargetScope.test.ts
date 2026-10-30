import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { networkTargetScope } from "../src/windows/httpTarget";

// Run from the editor folder (npm test). The same table is read by the server tests (JsNetworkGuardTests).
const cases = JSON.parse(readFileSync(resolve(process.cwd(), "../tests/shared/network-scope-cases.json"), "utf8")) as { host: string; scope: string }[];

describe("networkTargetScope", () => {
  it.each(cases)("$host is $scope", ({ host, scope }) => {
    expect(networkTargetScope(`${host}:80`)).toBe(scope);
  });
});
