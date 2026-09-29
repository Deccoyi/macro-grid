import { describe, expect, it } from "vitest";
import { isSafeWebUrl, webUrlHost } from "../src/widgets/webUrl";

describe("isSafeWebUrl", () => {
  it.each([
    "https://example.org/chat?channel=abc&token=1",
    "http://192.0.2.50:8080/panel",
    "https://example.org:8443/",
  ])("allows %s", (url) => expect(isSafeWebUrl(url)).toBe(true));

  it.each([
    undefined,
    null,
    42,
    "",
    "   ",
    "example.org/chat",
    "javascript:alert(1)",
    "data:text/html,<b>x</b>",
    "blob:https://example.org/1",
    "file:///C:/secret.txt",
    "ftp://example.org/file",
    "https://user:pass@example.org/",
    "https://user@example.org/",
    " https://example.org/",
    "https:\\example.org",
  ])("refuses %s", (url) => expect(isSafeWebUrl(url)).toBe(false));

  it.each([
    "http://localhost/",
    "https://LOCALHOST:9821/",
    "https://localhost./",
    "https://app.localhost/",
    "http://127.0.0.1:9820/",
    "http://127.5.5.5/",
    "http://0x7f.1/",
    "http://2130706433/",
    "http://0177.0.0.1/",
    "http://0.0.0.0/",
    "http://[::1]/",
    "http://[::]/",
    "http://[0:0:0:0:0:0:0:1]/",
    "http://[::ffff:127.0.0.1]/",
    "http://[::ffff:7f00:1]/",
    "http://[::127.0.0.1]/",
  ])("refuses the loopback form %s", (url) => expect(isSafeWebUrl(url)).toBe(false));

  it("allows a public IPv6 address", () => expect(isSafeWebUrl("http://[2001:db8::1]/")).toBe(true));

  it("refuses an address over the length cap", () => expect(isSafeWebUrl("https://example.org/" + "a".repeat(2048))).toBe(false));

  it("refuses the app's own origin", () => {
    expect(isSafeWebUrl(`${location.origin}/editor/`)).toBe(false);
    expect(isSafeWebUrl(`http://${location.hostname}:1234/`)).toBe(false);
  });

  it("refuses the extra blocked hosts, for example the server's address on the phone", () => {
    expect(isSafeWebUrl("http://192.168.1.20:9820/", ["192.168.1.20"])).toBe(false);
    expect(isSafeWebUrl("http://192.168.1.21:9820/", ["192.168.1.20"])).toBe(true);
  });
});

describe("webUrlHost", () => {
  it("never carries the path or query", () => {
    expect(webUrlHost("https://example.org/chat?token=secret")).toBe("example.org");
    expect(webUrlHost("not a url")).toBe("");
  });
});
