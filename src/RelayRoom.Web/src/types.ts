export type DeviceKind = "Desktop" | "Mobile" | "Other";
export type DeviceRole = "Host" | "Guest";
export type DeviceConnectionStatus = "Connected" | "Disconnected";
export type RoomStatus = "Active" | "Expired" | "Purged";
export type TransferKind = "Text" | "Link" | "File" | "Image" | "Voice";
export type TransferStatus = "Uploading" | "Available" | "Expired" | "Failed" | "Deleted";
export type DeliveryStatus = "Offered" | "Accepted" | "Downloading" | "Completed" | "Declined" | "Failed";
export type ConnectionState = "Live" | "Away" | "Reconnecting";

export type Device = {
  id: string;
  displayName: string;
  kind: DeviceKind;
  role: DeviceRole;
  status: DeviceConnectionStatus;
  avatarSeed: number;
  lastSeenAt: string;
};

export type TransferDelivery = {
  id: string;
  deviceId: string;
  status: DeliveryStatus;
  progressBytes: number;
  updatedAt: string;
};

export type Transfer = {
  id: string;
  senderDeviceId: string;
  targetDeviceId: string | null;
  kind: TransferKind;
  status: TransferStatus;
  fileName: string | null;
  mimeType: string | null;
  sizeBytes: number;
  textBody: string | null;
  sha256: string | null;
  createdAt: string;
  completedAt: string | null;
  failureReason: string | null;
  deliveries: TransferDelivery[];
  uploadPercent?: number;
};

export type Room = {
  id: string;
  publicCode: string;
  joinUrl: string;
  qrPayload: string;
  status: RoomStatus;
  createdAt: string;
  expiresAt: string;
  usedBytes: number;
  maxBytes: number;
  encryptionMode: string;
};

export type Session = {
  room: Room;
  device: Device;
  accessToken: string;
  accessTokenExpiresAt: string;
};

export type RoomDetail = {
  room: Room;
  devices: Device[];
  transfers: Transfer[];
};
