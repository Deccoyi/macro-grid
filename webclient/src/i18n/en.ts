export const en = {
  "device.name": "Browser",
  "status.connected": "Connected",
  "status.connecting": "Connecting…",
  "status.offline": "Offline",
  "connect.lost": "Connection lost, retrying…",
  "connect.pairHint": 'This browser is not paired yet. Open "Pairing" in the Macro Grid editor on your computer and enter the 6-digit PIN shown there.',
  "connect.pair": "Pair",
} as const;

export type MessageKey = keyof typeof en;
