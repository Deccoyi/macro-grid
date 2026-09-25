# Retire the temporary test client

Today `wwwroot/index.html` is a hand-written WebSocket page served at `/`, opened by the tray item "Open test page". It was a stopgap until a real client existed; the browser deck (`webclient/`) is now served at `/deck/` and pairs the same way.

Proposal
- Point the tray item at `/deck/` and rename it "Open browser deck".
- Delete `wwwroot/index.html`, the `UseDefaultFiles()` dependency on it, and the two `HostText` strings only it needs.
- Behavior change: `http://<host>:9820/` would no longer show the test page, so decide whether `/` should redirect to `/deck/`.

Not done here because it changes visible behavior (a tray menu entry and a public URL).
