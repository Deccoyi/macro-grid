export const en = {
  "device.name": "Browser",
  "status.connected": "Connected",
  "status.connecting": "Connecting…",
  "status.offline": "Offline",
  "connect.lost": "Connection lost, retrying…",
  "connect.pairHint": 'This browser is not paired yet. Open "Pairing" in the Macro Grid editor on your computer and enter the 6-digit PIN shown there.',
  "connect.pair": "Pair",
  "connect.pairBlockedSeconds": "Too many wrong PINs. Try again in {seconds} seconds.",
  "connect.reason.wrong_pin": "Wrong PIN. Enter the PIN shown in the Pairing window of the Macro Grid editor on the computer.",
  "connect.reason.pairing_closed": "Pairing is closed. Open the Pairing window in the Macro Grid editor on the computer, then enter the PIN shown there.",
  "connect.reason.not_paired": "Pairing is required. Open the Pairing window in the Macro Grid editor on the computer and enter the PIN shown there.",
} as const;

export type MessageKey = keyof typeof en;
