import type { Profile, WidgetState } from "@macro/renderer";
import { version as CLIENT_VERSION } from "../../package.json";

/** Every frame is { type, data }, matching MacroGrid.Protocol.Envelope server-side — same wire format
 * as the Android client's connection.ts (deliberately not shared as a package, same reasoning as the
 * renderer not being shared between the server and client repositories). */
interface Envelope<T = unknown> {
  type: string;
  data?: T;
}

interface LayoutFullData {
  profile: Profile;
  pageId: string;
}

interface PageShowData {
  pageId: string;
}

interface WelcomeData {
  serverName: string;
  serverVersion: string;
  token?: string | null;
}

interface ErrorData {
  code: string;
  message: string;
}

export interface ProfileSummary {
  id: string;
  name: string;
}

interface ProfilesListData {
  profiles: ProfileSummary[];
}

export type ConnectionStatus = "connecting" | "connected" | "disconnected" | "pairing_required";

export interface ConnectionEvents {
  onStatusChange: (status: ConnectionStatus) => void;
  onLayout: (profile: Profile, pageId: string) => void;
  onPageChange: (pageId: string) => void;
  onWidgetState: (state: WidgetState) => void;
  onProfiles: (profiles: ProfileSummary[]) => void;
  onPaired: (token: string) => void;
  onActionError: (message: string) => void;
}

const MAX_BACKOFF_MS = 10_000;
const TOKEN_KEY = "macro-grid.webToken";

/**
 * Owns the one WebSocket to the server this page is served from — always same-origin (this app is only
 * ever reached BY navigating to the server's own address, so there's no "enter the server's IP" screen
 * the way the phone client needs one). Sends hello on connect, dispatches incoming envelopes, reconnects
 * with exponential backoff on drop.
 */
export class ServerConnection {
  private socket: WebSocket | null = null;
  private backoffMs = 500;
  private closedByUser = false;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private token: string | null = localStorage.getItem(TOKEN_KEY);
  /** See the Android client's ServerConnection for why this exists (React StrictMode double-mount). */
  private destroyed = false;

  constructor(
    private readonly deviceId: string,
    private readonly deviceName: string,
    private readonly events: ConnectionEvents,
  ) {}

  connect(): void {
    this.closedByUser = false;
    this.open();
  }

  disconnect(): void {
    this.closedByUser = true;
    this.destroyed = true;
    if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
    this.socket?.close();
  }

  send(type: string, data?: unknown): void {
    if (this.socket?.readyState !== WebSocket.OPEN) return;
    this.socket.send(JSON.stringify({ type, data } satisfies Envelope));
  }

  retryWithPin(pin: string): void {
    this.sendHello(pin);
  }

  private sendHello(pin?: string): void {
    this.send("hello", {
      deviceId: this.deviceId,
      deviceName: this.deviceName,
      token: this.token,
      clientVersion: CLIENT_VERSION,
      pin,
    });
  }

  private open(): void {
    if (this.destroyed) return;
    this.events.onStatusChange("connecting");
    const protocol = location.protocol === "https:" ? "wss:" : "ws:";
    const socket = new WebSocket(`${protocol}//${location.host}/ws`);
    this.socket = socket;

    socket.onopen = () => {
      if (this.destroyed) return;
      this.backoffMs = 500;
      this.sendHello();
      this.events.onStatusChange("connected");
    };

    socket.onmessage = (ev) => {
      if (this.destroyed) return;
      this.handleMessage(ev.data);
    };

    socket.onclose = () => {
      if (this.destroyed) return;
      this.events.onStatusChange("disconnected");
      if (this.closedByUser) return;
      this.reconnectTimer = setTimeout(() => this.open(), this.backoffMs);
      this.backoffMs = Math.min(this.backoffMs * 2, MAX_BACKOFF_MS);
    };

    socket.onerror = () => socket.close();
  }

  private handleMessage(raw: string): void {
    let envelope: Envelope;
    try {
      envelope = JSON.parse(raw);
    } catch {
      return;
    }

    switch (envelope.type) {
      case "welcome": {
        const data = envelope.data as WelcomeData;
        if (data.token) {
          this.token = data.token;
          localStorage.setItem(TOKEN_KEY, data.token);
          this.events.onPaired(data.token);
        }
        this.events.onStatusChange("connected");
        break;
      }
      case "layout.full": {
        const data = envelope.data as LayoutFullData;
        this.events.onLayout(data.profile, data.pageId);
        break;
      }
      case "page.show": {
        const data = envelope.data as PageShowData;
        this.events.onPageChange(data.pageId);
        break;
      }
      case "widget.state":
        this.events.onWidgetState(envelope.data as WidgetState);
        break;
      case "profiles.list":
        this.events.onProfiles((envelope.data as ProfilesListData).profiles);
        break;
      case "error": {
        const data = envelope.data as ErrorData;
        if (data.code === "pairing_required") this.events.onStatusChange("pairing_required");
        else if (data.code === "action_failed") this.events.onActionError(data.message);
        break;
      }
      default:
        break;
    }
  }

  changeProfile(profileId: string): void {
    this.send("profile.change", { profileId });
  }

  nextPage(): void {
    this.send("page.next");
  }

  prevPage(): void {
    this.send("page.prev");
  }
}
