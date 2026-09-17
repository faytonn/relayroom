import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import QRCode from "qrcode";
import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent, type KeyboardEvent, type PointerEvent } from "react";
import { api } from "../api";
import { formatBytes, formatClock, formatRemaining, isHttpUrl, kindForFile, saveTransfer, uploadFile } from "../files";
import { loadSession, saveSession } from "../session";
import type { ConnectionState, Device, RoomDetail, Session, Transfer } from "../types";

type Toast = { id: number; message: string };

type Props = {
  code: string;
};

export function RoomPage({ code }: Props) {
  const [session, setSession] = useState<Session | null>(loadSession);
  const [detail, setDetail] = useState<RoomDetail | null>(null);
  const [connection, setConnection] = useState<ConnectionState>("Away");
  const [error, setError] = useState<string | null>(null);
  const [dragDeviceId, setDragDeviceId] = useState<string | null>(null);
  const [composeOver, setComposeOver] = useState(false);
  const [text, setText] = useState("");
  const [recording, setRecording] = useState<number | null>(null);
  const [incoming, setIncoming] = useState<Transfer | null>(null);
  const [qr, setQr] = useState<string | null>(null);
  const [joinOpen, setJoinOpen] = useState(true);
  const [now, setNow] = useState(() => Date.now());
  const [toasts, setToasts] = useState<Toast[]>([]);
  const filesRef = useRef<Map<string, File>>(new Map());
  const recorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const startedRef = useRef(0);
  const textRef = useRef<HTMLTextAreaElement | null>(null);
  const fileRef = useRef<HTMLInputElement | null>(null);
  const recordLockRef = useRef(false);
  const pendingStopRef = useRef(false);
  const composeDragRef = useRef(0);
  const toastSeqRef = useRef(0);
  const lastToastRef = useRef({ message: "", at: 0 });
  const toastTimersRef = useRef<number[]>([]);

  const expired = detail?.room.status !== "Active" && detail !== null;

  const remaining = detail ? formatRemaining(detail.room.expiresAt) : { label: "--:--", expiring: false };

  const host = session?.device.role === "Host";
  const deviceCount = detail?.devices.length ?? 0;

  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, []);

  useEffect(() => {
    if (host && deviceCount < 2) {
      setJoinOpen(true);
    } else if (deviceCount >= 2) {
      setJoinOpen(false);
    }
  }, [host, deviceCount]);

  useEffect(() => {
    const url = detail?.room.joinUrl ?? session?.room.joinUrl;
    if (!url) {
      return;
    }
    void QRCode.toDataURL(url, { margin: 0, width: 256, color: { dark: "#1F1B16", light: "#FAF7F2" } }).then(setQr);
  }, [detail?.room.joinUrl, session?.room.joinUrl]);

  const showToast = useCallback((message: string) => {
    const at = Date.now();
    if (lastToastRef.current.message === message && at - lastToastRef.current.at < 2000) {
      return;
    }
    lastToastRef.current = { message, at };
    const id = ++toastSeqRef.current;
    setToasts((current) => [...current.slice(-2), { id, message }]);
    const timer = window.setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id));
    }, 2600);
    toastTimersRef.current.push(timer);
  }, []);

  useEffect(() => {
    return () => {
      toastTimersRef.current.forEach((timer) => window.clearTimeout(timer));
    };
  }, []);

  const upsertTransfer = useCallback((transfer: Transfer) => {
    setDetail((current) => {
      if (!current) {
        return current;
      }
      const exists = current.transfers.some((item) => item.id === transfer.id);
      return {
        ...current,
        transfers: exists
          ? current.transfers.map((item) => (item.id === transfer.id ? { ...item, ...transfer } : item))
          : [transfer, ...current.transfers],
      };
    });
  }, []);

  const enter = useCallback(async () => {
    setError(null);
    try {
      let next = loadSession();
      if (!next || next.room.publicCode.toUpperCase() !== code.toUpperCase()) {
        next = await api.joinRoom(code);
        saveSession(next);
      }
      setSession(next);
      const room = await api.getRoom(next.room.id, next.accessToken);
      setDetail(room);
      const offered = room.transfers.find(
        (transfer) =>
          transfer.targetDeviceId === next.device.id &&
          transfer.status === "Available" &&
          transfer.deliveries.some((delivery) => delivery.deviceId === next.device.id && delivery.status === "Offered"),
      );
      if (offered) {
        setIncoming(offered);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Could not enter the room.";
      setError(message);
      if (/expired|410/i.test(message)) {
        setDetail({
          room: {
            id: "",
            publicCode: code.toUpperCase(),
            joinUrl: "",
            qrPayload: "",
            status: "Expired",
            createdAt: new Date().toISOString(),
            expiresAt: new Date().toISOString(),
            usedBytes: 0,
            maxBytes: 0,
            encryptionMode: "None",
          },
          devices: [],
          transfers: [],
        });
      }
    }
  }, [code]);

  useEffect(() => {
    void enter();
  }, [enter]);

  useEffect(() => {
    if (!session || expired) {
      return;
    }

    const hub: HubConnection = new HubConnectionBuilder()
      .withUrl(`/hubs/room?access_token=${session.accessToken}`)
      .withAutomaticReconnect()
      .build();

    hub.on("DeviceJoined", (device: Device) => {
      let added = false;
      setDetail((current) => {
        if (!current || current.devices.some((item) => item.id === device.id)) {
          return current;
        }
        added = true;
        return { ...current, devices: [...current.devices, device] };
      });
      if (added && device.id !== session.device.id) {
        showToast("Device joined");
      }
    });
    hub.on("DeviceLeft", (device: Device) => {
      setDetail((current) =>
        current
          ? {
              ...current,
              devices: current.devices.map((item) =>
                item.id === device.id ? { ...item, status: "Disconnected" } : item,
              ),
            }
          : current,
      );
    });
    hub.on("DeviceUpdated", (device: Device) => {
      setDetail((current) =>
        current
          ? { ...current, devices: current.devices.map((item) => (item.id === device.id ? device : item)) }
          : current,
      );
    });
    hub.on("DeviceConnectionChanged", (device: Device) => {
      setDetail((current) =>
        current
          ? { ...current, devices: current.devices.map((item) => (item.id === device.id ? device : item)) }
          : current,
      );
    });
    hub.on("TransferCreated", upsertTransfer);
    hub.on("TransferAvailable", (transfer: Transfer) => {
      upsertTransfer(transfer);
      if (transfer.targetDeviceId === session.device.id) {
        setIncoming(transfer);
      }
    });
    hub.on("TransferOffered", (transfer: Transfer) => {
      upsertTransfer(transfer);
      setIncoming(transfer);
    });
    hub.on("TransferFailed", ({ transferId, reason }: { transferId: string; reason: string }) => {
      setDetail((current) =>
        current
          ? {
              ...current,
              transfers: current.transfers.map((item) =>
                item.id === transferId ? { ...item, status: "Failed", failureReason: reason } : item,
              ),
            }
          : current,
      );
    });
    hub.on("TransferDeleted", ({ transferId }: { transferId: string }) => {
      setDetail((current) =>
        current ? { ...current, transfers: current.transfers.filter((item) => item.id !== transferId) } : current,
      );
    });
    hub.on("TransferProgress", ({ transferId, uploadedBytes, totalBytes }: { transferId: string; uploadedBytes: number; totalBytes: number }) => {
      setDetail((current) =>
        current
          ? {
              ...current,
              transfers: current.transfers.map((item) =>
                item.id === transferId
                  ? { ...item, uploadPercent: totalBytes ? Math.round((uploadedBytes / totalBytes) * 100) : 0 }
                  : item,
              ),
            }
          : current,
      );
    });
    hub.on("RoomExpired", () => {
      setDetail((current) => (current ? { ...current, room: { ...current.room, status: "Expired" } } : current));
    });
    hub.onreconnecting(() => setConnection("Reconnecting"));
    hub.onreconnected(() => setConnection("Live"));
    hub.onclose(() => setConnection("Away"));

    void hub
      .start()
      .then(() => setConnection("Live"))
      .catch(() => setConnection("Away"));

    return () => {
      void hub.stop();
    };
  }, [session, expired, upsertTransfer, showToast]);

  useEffect(() => {
    function onPaste(event: ClipboardEvent) {
      if (!session || expired) {
        return;
      }
      const clipboard = event.clipboardData;
      if (!clipboard) {
        return;
      }
      const file = clipboard.files[0];
      if (file) {
        event.preventDefault();
        void sendFile(file, null);
        return;
      }
      const pasted = clipboard.getData("text");
      if (pasted && document.activeElement !== textRef.current) {
        event.preventDefault();
        void sendText(pasted, null);
      }
    }

    window.addEventListener("paste", onPaste);
    return () => window.removeEventListener("paste", onPaste);
  }, [session, expired]);

  function resizeTextArea() {
    const el = textRef.current;
    if (!el) {
      return;
    }
    el.style.height = "22px";
    el.style.height = `${Math.min(el.scrollHeight, 110)}px`;
  }

  async function sendText(value: string, targetDeviceId: string | null) {
    if (!session || !value.trim()) {
      return;
    }
    const kind = isHttpUrl(value) ? "Link" : "Text";
    const transfer = await api.createTransfer(session.room.id, session.accessToken, {
      kind,
      textBody: value.trim(),
      targetDeviceId,
    });
    upsertTransfer(transfer);
    setText("");
    requestAnimationFrame(resizeTextArea);
  }

  async function sendFile(file: File, targetDeviceId: string | null) {
    if (!session) {
      return;
    }
    try {
      const intent = await uploadFile({
        roomId: session.room.id,
        token: session.accessToken,
        file,
        kind: kindForFile(file),
        targetDeviceId,
        onProgress: (percent) => {
          setDetail((current) =>
            current
              ? {
                  ...current,
                  transfers: current.transfers.map((item) =>
                    item.fileName === file.name && item.status === "Uploading" ? { ...item, uploadPercent: percent } : item,
                  ),
                }
              : current,
          );
        },
      });
      filesRef.current.set(intent.id, file);
      upsertTransfer({ ...intent, uploadPercent: 100 });
      showToast("Upload complete");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed.");
    }
  }

  function onDrop(event: DragEvent, targetDeviceId: string | null) {
    event.preventDefault();
    composeDragRef.current = 0;
    setDragDeviceId(null);
    setComposeOver(false);
    const file = event.dataTransfer.files[0];
    const droppedText = event.dataTransfer.getData("text");
    if (file) {
      void sendFile(file, targetDeviceId);
    } else if (droppedText) {
      void sendText(droppedText, targetDeviceId);
    }
  }

  async function copyRoomCode() {
    const value = (detail?.room.publicCode ?? code).toUpperCase();
    try {
      await navigator.clipboard.writeText(value);
      showToast("Room code copied");
    } catch {
      setError("Could not copy the room code.");
    }
  }

  async function downloadTransfer(transfer: Transfer) {
    if (!session) {
      return;
    }
    if (transfer.kind !== "Text" && transfer.kind !== "Link") {
      showToast("Download started");
    }
    try {
      await saveTransfer(transfer, session.accessToken);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not download the file.");
    }
  }

  function releaseRecordLock() {
    window.setTimeout(() => {
      recordLockRef.current = false;
    }, 400);
  }

  async function beginRecording(button: HTMLButtonElement, pointerId?: number) {
    if (expired || recordLockRef.current || recorderRef.current) {
      return;
    }
    recordLockRef.current = true;
    pendingStopRef.current = false;
    try {
      if (pointerId !== undefined) {
        try {
          button.setPointerCapture(pointerId);
        } catch {
          /* capture is best-effort on some browsers */
        }
      }
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      if (pendingStopRef.current) {
        stream.getTracks().forEach((track) => track.stop());
        setRecording(null);
        releaseRecordLock();
        return;
      }
      const recorder = new MediaRecorder(stream);
      chunksRef.current = [];
      recorder.ondataavailable = (dataEvent) => {
        if (dataEvent.data.size > 0) {
          chunksRef.current.push(dataEvent.data);
        }
      };
      recorder.start();
      recorderRef.current = recorder;
      startedRef.current = Date.now();
      setRecording(0);
      const tick = window.setInterval(() => setRecording(Math.floor((Date.now() - startedRef.current) / 1000)), 250);
      recorder.addEventListener("stop", () => window.clearInterval(tick), { once: true });
    } catch (err) {
      recorderRef.current = null;
      setRecording(null);
      setError(err instanceof Error ? err.message : "Could not start recording.");
      releaseRecordLock();
    }
  }

  function onRecordPointerDown(event: PointerEvent<HTMLButtonElement>) {
    event.preventDefault();
    void beginRecording(event.currentTarget, event.pointerId);
  }

  function onRecordKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    if (event.repeat) {
      return;
    }
    if (event.key === " " || event.key === "Enter") {
      event.preventDefault();
      void beginRecording(event.currentTarget);
    }
  }

  function onRecordKeyUp(event: KeyboardEvent<HTMLButtonElement>) {
    if (event.key === " " || event.key === "Enter") {
      event.preventDefault();
      void stopRecording();
    }
  }

  async function stopRecording() {
    pendingStopRef.current = true;
    const recorder = recorderRef.current;
    if (!recorder) {
      return;
    }
    recorderRef.current = null;
    try {
      await new Promise<void>((resolve) => {
        recorder.addEventListener("stop", () => resolve(), { once: true });
        if (recorder.state !== "inactive") {
          recorder.stop();
        } else {
          resolve();
        }
      });
      recorder.stream.getTracks().forEach((track) => track.stop());
      setRecording(null);
      const blob = new Blob(chunksRef.current, { type: recorder.mimeType || "audio/webm" });
      chunksRef.current = [];
      if (blob.size === 0) {
        return;
      }
      const file = new File([blob], `voice-${Date.now()}.webm`, { type: blob.type });
      await sendFile(file, null);
    } finally {
      releaseRecordLock();
    }
  }

  async function retry(transfer: Transfer) {
    const file = filesRef.current.get(transfer.id);
    if (file) {
      await sendFile(file, transfer.targetDeviceId);
    }
  }

  async function acceptIncoming(action: "save" | "decline") {
    if (!session || !incoming) {
      return;
    }
    try {
      if (action === "save") {
        if (incoming.kind !== "Text" && incoming.kind !== "Link") {
          showToast("Download started");
        }
        await api.ack(incoming.id, session.accessToken, "Accepted");
        await saveTransfer(incoming, session.accessToken);
        await api.ack(incoming.id, session.accessToken, "Completed");
      } else {
        await api.ack(incoming.id, session.accessToken, "Declined");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not download the file.");
    }
    setIncoming(null);
  }

  const title = session ? `RelayRoom ${session.room.publicCode}` : "RelayRoom";
  useEffect(() => {
    document.title = title;
  }, [title]);

  const sortedTransfers = useMemo(
    () => [...(detail?.transfers ?? [])].sort((a, b) => +new Date(b.createdAt) - +new Date(a.createdAt)),
    [detail?.transfers],
  );

  void now;

  return (
    <main className="page room-page">
      <div className="stack">
        <header className="header">
          <div>
            <div className="brand">RelayRoom</div>
            <h1 className="room-code">
              <button type="button" className="room-code-btn" onClick={() => void copyRoomCode()} title="Copy room code">
                {(detail?.room.publicCode ?? code).toUpperCase()}
              </button>
            </h1>
          </div>
          <div style={{ textAlign: "right" }}>
            <div className={`timer ${remaining.expiring ? "expiring" : ""}`}>{remaining.label}</div>
            <div className="status">
              <span className={`dot ${connection === "Live" ? "live" : connection === "Reconnecting" ? "reconnecting" : ""}`} />
              {expired ? "Expired" : connection}
            </div>
          </div>
        </header>

        {error ? <p className="error">{error}</p> : null}
        {expired ? <p className="muted">This room has expired. Uploaded files were deleted.</p> : null}

        <details className="panel" open={joinOpen} onToggle={(event) => setJoinOpen(event.currentTarget.open)}>
          <summary>Join this room</summary>
          <div className="join-body">
            {qr ? <img className="qr" src={qr} alt="Join QR code" /> : <div className="qr" />}
            <div>
              <div className="label">Code</div>
              <button type="button" className="room-code-btn" onClick={() => void copyRoomCode()} title="Copy room code">
                <span className="room-code" style={{ fontSize: 22, lineHeight: "28px" }}>
                  {(detail?.room.publicCode ?? code).toUpperCase()}
                </span>
              </button>
              <p className="muted" style={{ margin: "8px 0 0" }}>
                {detail?.room.joinUrl ?? session?.room.joinUrl ?? `Open /r/${code}`}
              </p>
            </div>
          </div>
        </details>

        <section>
          <h2 className="section-title">Devices</h2>
          <div className="list">
            {(detail?.devices ?? []).map((device) => {
              const inbound = sortedTransfers.find(
                (transfer) => transfer.targetDeviceId === device.id && transfer.status === "Uploading",
              );
              return (
                <div
                  key={device.id}
                  className={`row device-row ${dragDeviceId === device.id ? "dragover" : ""}`}
                  onDragOver={(event) => {
                    event.preventDefault();
                    setDragDeviceId(device.id);
                  }}
                  onDragLeave={() => setDragDeviceId((current) => (current === device.id ? null : current))}
                  onDrop={(event) => onDrop(event, device.id)}
                >
                  <span className={`dot ${device.status === "Connected" ? "live" : ""}`} />
                  <div className="grow">
                    <div className="row-title">
                      {device.displayName}
                      {device.id === session?.device.id ? " · you" : ""}
                    </div>
                    <div className="row-meta">
                      {device.kind} · {device.role} · {device.status === "Connected" ? "Live" : "Away"}
                    </div>
                  </div>
                  {inbound ? (
                    <div className="progress">
                      <span style={{ width: `${inbound.uploadPercent ?? 0}%` }} />
                    </div>
                  ) : null}
                </div>
              );
            })}
          </div>
        </section>

        <section>
          <h2 className="section-title">Transfers</h2>
          <div className="list">
            {sortedTransfers.length === 0 ? (
              <div className="empty-state">
                <p className="empty-title">Nothing on the table yet</p>
                <p className="muted">
                  Drop a file, paste a link, or hold to record a voice note. Everything here stays until the room expires.
                </p>
              </div>
            ) : (
              sortedTransfers.map((transfer) => (
                <TransferRow
                  key={transfer.id}
                  transfer={transfer}
                  selfId={session?.device.id}
                  onDownload={() => void downloadTransfer(transfer)}
                  onRetry={() => void retry(transfer)}
                />
              ))
            )}
          </div>
        </section>
      </div>

      <div className="compose-sticky">
        <div
          className={`compose ${composeOver ? "dragover" : ""} ${expired ? "disabled" : ""}`}
          onDragEnter={(event) => {
            event.preventDefault();
            composeDragRef.current += 1;
            setComposeOver(true);
          }}
          onDragOver={(event) => {
            event.preventDefault();
            setComposeOver(true);
          }}
          onDragLeave={() => {
            composeDragRef.current = Math.max(0, composeDragRef.current - 1);
            if (composeDragRef.current === 0) {
              setComposeOver(false);
            }
          }}
          onDrop={(event) => onDrop(event, null)}
        >
          {composeOver ? (
            <div className="compose-drop-hint" aria-live="polite">
              Drop file to send.
            </div>
          ) : null}
          <textarea
            ref={textRef}
            rows={1}
            placeholder="Write, paste, or drop a file"
            value={text}
            disabled={expired}
            onChange={(event) => {
              setText(event.target.value);
              requestAnimationFrame(resizeTextArea);
            }}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                void sendText(text, null);
              }
            }}
          />
          <div className="compose-actions">
            <input
              ref={fileRef}
              className="hidden"
              type="file"
              tabIndex={-1}
              disabled={expired}
              onChange={(event) => {
                const file = event.target.files?.[0];
                if (file) {
                  void sendFile(file, null);
                }
                event.target.value = "";
              }}
            />
            <button className="file-btn" type="button" disabled={expired} onClick={() => fileRef.current?.click()}>
              File
            </button>
            <button
              className={`record-chip ${recording !== null ? "recording" : ""}`}
              type="button"
              disabled={expired}
              aria-pressed={recording !== null}
              aria-label={recording === null ? "Hold to record" : "Release to send"}
              onPointerDown={onRecordPointerDown}
              onPointerUp={() => void stopRecording()}
              onPointerCancel={() => void stopRecording()}
              onKeyDown={onRecordKeyDown}
              onKeyUp={onRecordKeyUp}
              onContextMenu={(event) => event.preventDefault()}
            >
              <span className="record-dot" />
              <span className="record-label">
                {recording === null
                  ? "Hold to record"
                  : `Release ${String(Math.floor(recording / 60)).padStart(2, "0")}:${String(recording % 60).padStart(2, "0")}`}
              </span>
            </button>
            <span className="spacer" />
            <button className="btn send-btn" type="button" disabled={expired || !text.trim()} onClick={() => void sendText(text, null)}>
              Send
            </button>
          </div>
        </div>
      </div>

      {incoming && !expired ? (
        <aside className="sheet">
          <div className="label">Incoming from {detail?.devices.find((device) => device.id === incoming.senderDeviceId)?.displayName ?? "a device"}</div>
          <div className="sheet-filename">{incoming.fileName ?? incoming.textBody ?? incoming.kind}</div>
          <div className="row-meta">
            {incoming.kind}
            {incoming.sizeBytes ? ` · ${formatBytes(incoming.sizeBytes)}` : ""}
          </div>
          <button className="btn" type="button" onClick={() => void acceptIncoming("save")}>
            {actionLabel(incoming.kind)}
          </button>
          <button className="btn-text" type="button" onClick={() => void acceptIncoming("decline")}>
            Decline
          </button>
        </aside>
      ) : null}

      <div className="toast-stack" aria-live="polite" aria-relevant="additions">
        {toasts.map((toast) => (
          <div key={toast.id} className="toast">
            {toast.message}
          </div>
        ))}
      </div>
    </main>
  );
}

function actionLabel(kind: Transfer["kind"]): string {
  if (kind === "Text") {
    return "Copy";
  }
  if (kind === "Link") {
    return "Open";
  }
  return "Download";
}

function fileIconKind(transfer: Transfer): "pdf" | "audio" | "image" | "zip" | "file" | "text" | "link" {
  if (transfer.kind === "Image") {
    return "image";
  }
  if (transfer.kind === "Voice") {
    return "audio";
  }
  if (transfer.kind === "Text") {
    return "text";
  }
  if (transfer.kind === "Link") {
    return "link";
  }
  const name = (transfer.fileName ?? "").toLowerCase();
  const mime = (transfer.mimeType ?? "").toLowerCase();
  if (mime === "application/pdf" || name.endsWith(".pdf")) {
    return "pdf";
  }
  if (
    mime.includes("zip") ||
    mime === "application/x-7z-compressed" ||
    mime === "application/vnd.rar" ||
    name.endsWith(".zip") ||
    name.endsWith(".7z") ||
    name.endsWith(".rar")
  ) {
    return "zip";
  }
  if (mime.startsWith("image/")) {
    return "image";
  }
  if (mime.startsWith("audio/")) {
    return "audio";
  }
  return "file";
}

function FileTypeIcon({ transfer }: { transfer: Transfer }) {
  const kind = fileIconKind(transfer);
  return (
    <span className={`file-icon file-icon-${kind}`} aria-hidden="true">
      <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.25" strokeLinecap="round" strokeLinejoin="round">
        {kind === "pdf" ? (
          <>
            <path d="M4 2.5h5.5L12 5v8.5H4z" />
            <path d="M9.5 2.5V5H12" />
            <path d="M6 11.2V7.5h1.15a1.35 1.35 0 0 1 0 2.7H6" />
          </>
        ) : null}
        {kind === "image" ? (
          <>
            <rect x="2.5" y="3.5" width="11" height="9" rx="1.2" />
            <path d="m3 11.2 3.1-3.1 2.2 2.2 2-2 2.7 2.7" />
            <circle cx="6.1" cy="6.4" r="0.8" fill="currentColor" stroke="none" />
          </>
        ) : null}
        {kind === "audio" ? (
          <>
            <path d="M6 10.2V5.8L10.2 4v7.4" />
            <path d="M6 10.2a1.6 1.6 0 1 1-3.2 0 1.6 1.6 0 0 1 3.2 0ZM10.2 11.4a1.6 1.6 0 1 1-3.2 0 1.6 1.6 0 0 1 3.2 0Z" />
          </>
        ) : null}
        {kind === "zip" ? (
          <>
            <path d="M3.5 4.5h9v9h-9z" />
            <path d="M3.5 7.2h9" />
            <path d="M8 4.5v3.2M7.2 4.5h1.6M7.2 6.1h1.6" />
          </>
        ) : null}
        {kind === "text" ? (
          <>
            <path d="M4 2.5h5.5L12 5v8.5H4z" />
            <path d="M9.5 2.5V5H12" />
            <path d="M6 8h4M6 10.2h4M6 12.2h2.4" />
          </>
        ) : null}
        {kind === "link" ? (
          <>
            <path d="M6.6 9.4 5.2 10.8a2 2 0 1 1-2.8-2.8l1.8-1.8a2 2 0 0 1 2.8 0" />
            <path d="M9.4 6.6 10.8 5.2a2 2 0 1 1 2.8 2.8l-1.8 1.8a2 2 0 0 1-2.8 0" />
          </>
        ) : null}
        {kind === "file" ? (
          <>
            <path d="M4 2.5h5.5L12 5v8.5H4z" />
            <path d="M9.5 2.5V5H12" />
          </>
        ) : null}
      </svg>
    </span>
  );
}

function TransferRow({
  transfer,
  selfId,
  onDownload,
  onRetry,
}: {
  transfer: Transfer;
  selfId?: string;
  onDownload: () => void;
  onRetry: () => void;
}) {
  const title =
    transfer.fileName ??
    (transfer.kind === "Link" ? transfer.textBody : transfer.textBody?.slice(0, 80)) ??
    transfer.kind;
  const percent = transfer.uploadPercent ?? 0;
  const clock = formatClock(transfer.createdAt);
  const state =
    transfer.status === "Uploading"
      ? `Uploading ${percent}%`
      : transfer.status === "Failed"
        ? "Failed"
        : transfer.status;

  return (
    <div className="row">
      <FileTypeIcon transfer={transfer} />
      <div className="grow">
        <div className="row-title">{title}</div>
        <div className="row-meta">
          {state}
          {clock ? ` · ${clock}` : ""}
          {transfer.targetDeviceId ? " · targeted" : " · room"}
          {transfer.senderDeviceId === selfId ? " · from you" : ""}
          {transfer.status === "Failed" ? (
            <>
              {" · "}
              <button className="btn-text" type="button" onClick={onRetry}>
                Retry
              </button>
            </>
          ) : null}
        </div>
      </div>
      {transfer.status === "Available" ? (
        <div className="row-actions">
          <button className="btn-text" type="button" onClick={onDownload}>
            {actionLabel(transfer.kind)}
          </button>
        </div>
      ) : null}
      {transfer.status === "Uploading" ? (
        <div className="progress">
          <span style={{ width: `${percent}%` }} />
        </div>
      ) : null}
    </div>
  );
}
