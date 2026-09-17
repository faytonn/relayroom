import * as tus from "tus-js-client";
import { api } from "./api";
import type { Transfer, TransferKind } from "./types";

export function isHttpUrl(value: string): boolean {
  try {
    const uri = new URL(value.trim());
    return uri.protocol === "http:" || uri.protocol === "https:";
  } catch {
    return false;
  }
}

export function kindForFile(file: File): TransferKind {
  if (file.type.startsWith("image/")) {
    return "Image";
  }
  if (file.type.startsWith("audio/")) {
    return "Voice";
  }
  return "File";
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function formatClock(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return date.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
}

export function formatRemaining(expiresAt: string): { label: string; expiring: boolean } {
  const ms = new Date(expiresAt).getTime() - Date.now();
  if (ms <= 0) {
    return { label: "00:00", expiring: true };
  }
  const total = Math.floor(ms / 1000);
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  return {
    label: `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`,
    expiring: total <= 5 * 60,
  };
}

export async function uploadFile(options: {
  roomId: string;
  token: string;
  file: File;
  kind?: TransferKind;
  targetDeviceId?: string | null;
  onProgress?: (percent: number) => void;
}): Promise<Transfer> {
  const kind = options.kind ?? kindForFile(options.file);
  const intent = await api.createTransfer(options.roomId, options.token, {
    kind,
    fileName: options.file.name,
    mimeType: options.file.type || "application/octet-stream",
    sizeBytes: options.file.size,
    targetDeviceId: options.targetDeviceId,
  });

  await new Promise<void>((resolve, reject) => {
    const upload = new tus.Upload(options.file, {
      endpoint: "/api/uploads",
      chunkSize: 1024 * 1024,
      retryDelays: [0, 1000, 3000],
      headers: { Authorization: `Bearer ${options.token}` },
      metadata: {
        transferId: intent.id,
        filename: options.file.name,
        filetype: options.file.type,
      },
      onError: (error) => reject(error),
      onProgress: (sent, total) => options.onProgress?.(Math.round((sent / total) * 100)),
      onSuccess: () => resolve(),
    });
    upload.start();
  });

  return intent;
}

export async function saveTransfer(transfer: Transfer, token: string): Promise<void> {
  if (transfer.kind === "Link" && transfer.textBody) {
    window.open(transfer.textBody, "_blank", "noopener,noreferrer");
    return;
  }

  if (transfer.kind === "Text" && transfer.textBody) {
    await navigator.clipboard.writeText(transfer.textBody);
    return;
  }

  // Always stream through the API. Azure Blob SAS URLs are a different origin, so
  // browser fetch() is blocked unless the storage account has CORS rules.
  const response = await fetch(`/api/transfers/${transfer.id}/content`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    throw new Error("Could not download the file.");
  }
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = transfer.fileName ?? "download";
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
