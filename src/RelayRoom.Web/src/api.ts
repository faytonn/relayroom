import type { DeliveryStatus, RoomDetail, Session, Transfer, TransferKind } from "./types";

async function readError(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { error?: string };
    if (body.error) {
      return body.error;
    }
  } catch {
    // ignore
  }
  return `${response.status} ${response.statusText}`;
}

async function request<T>(path: string, init: RequestInit & { token?: string } = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.token) {
    headers.set("Authorization", `Bearer ${init.token}`);
  }
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  let response: Response;
  try {
    response = await fetch(path, { ...init, headers });
  } catch {
    throw new Error("Can't reach RelayRoom. Is the API running on port 5090?");
  }

  if (!response.ok) {
    throw new Error(await readError(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const api = {
  createRoom: (displayName?: string) =>
    request<Session>("/api/rooms", { method: "POST", body: JSON.stringify({ displayName }) }),

  joinRoom: (code: string, displayName?: string) =>
    request<Session>("/api/rooms/join", { method: "POST", body: JSON.stringify({ code, displayName }) }),

  getRoom: (roomId: string, token: string) =>
    request<RoomDetail>(`/api/rooms/${roomId}`, { token }),

  closeRoom: (roomId: string, token: string) =>
    request<void>(`/api/rooms/${roomId}/close`, { method: "POST", token }),

  rename: (displayName: string, token: string) =>
    request<{ displayName: string }>("/api/devices/me", {
      method: "PATCH",
      token,
      body: JSON.stringify({ displayName }),
    }),

  createTransfer: (
    roomId: string,
    token: string,
    body: {
      kind: TransferKind;
      textBody?: string;
      fileName?: string;
      mimeType?: string;
      sizeBytes?: number;
      targetDeviceId?: string | null;
    },
  ) => request<Transfer>(`/api/rooms/${roomId}/transfers`, { method: "POST", token, body: JSON.stringify(body) }),

  downloadUrl: (transferId: string, token: string) =>
    request<{ url: string; expiresAt: string; inline: boolean }>(`/api/transfers/${transferId}/download-url`, { token }),

  ack: (transferId: string, token: string, status: DeliveryStatus, progressBytes?: number) =>
    request(`/api/transfers/${transferId}/deliveries/ack`, {
      method: "POST",
      token,
      body: JSON.stringify({ status, progressBytes }),
    }),
};
