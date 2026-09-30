# Crash report dialog

> Status: **planned, not built.** Needs the owner's decision on the sending mechanism (open question 1). **Repositories:** `macro-grid` only.

When the server crashes, show the person a native window (WinForms, not the editor and not a browser tab) that says what would be sent and lets
them choose to send it or not. The app itself sends the report; it never opens the person's own mail program.

## What is sent

- The newest log files (the log folder is already capped by `LogRetention`) and a small info block: Macro Grid version, Windows version, .NET runtime
  version, the ids and versions of the installed plugins, and the exception type, message and stack trace.
- Never: profiles, pairing PIN, device tokens, plugin settings or secrets, file paths outside the log folder. Log lines already avoid secrets
  (`SecurityEvents.ForLog`); the report builder still runs a redaction pass (user name in paths replaced).
- The window shows the exact text before sending, and a "Save to a file instead" button.

## How a crash is caught

A crashed process cannot reliably show a window, so use the **next-start check**: an unhandled-exception handler (`AppDomain.UnhandledException`,
`TaskScheduler.UnobservedTaskException`, the WinForms thread exception) writes `crash-<time>.json` (exception and info block, no logs) into the data folder,
as fast and as simply as possible. The next start finds it and shows the dialog, then deletes the marker (send or not). A watchdog process is not needed.

## Sending

Needs a decision (open question 1). Options: (a) SMTP with a project-owned mailbox: the credentials would have to ship in the app, so anyone can
extract them; (b) a small relay that we run and that holds the credentials: safest, but it is a server we must keep running; (c) no automatic send:
the dialog saves a report file and shows where the person can send it (an issue on the public tracker, or an address). Recommendation: (c) first,
because it needs nothing from the owner; (b) later if reports are wanted at volume.

## Open questions

1. Which sending mechanism (a, b or c above)? Shipping mailbox credentials inside the app is not acceptable.
2. Is a report file the person can read and attach themselves enough for the first version?
3. Should the dialog be off for a silent or unattended start (autostart in tray mode shows it only when the person opens the tray menu)?
