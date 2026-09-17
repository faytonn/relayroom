import { useEffect, useMemo, useState, type FormEvent } from "react";
import { api } from "../api";
import { saveSession } from "../session";

export function LandingPage() {
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState<"create" | "join" | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canJoin = useMemo(() => code.replace(/[\s-]/g, "").length >= 6, [code]);

  useEffect(() => {
    document.title = "RelayRoom";
  }, []);

  async function createRoom() {
    setBusy("create");
    setError(null);
    try {
      const session = await api.createRoom();
      saveSession(session);
      window.history.pushState({}, "", `/r/${session.room.publicCode}`);
      window.dispatchEvent(new PopStateEvent("popstate"));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create a room.");
    } finally {
      setBusy(null);
    }
  }

  async function joinRoom(event: FormEvent) {
    event.preventDefault();
    setBusy("join");
    setError(null);
    try {
      const session = await api.joinRoom(code.trim());
      saveSession(session);
      window.history.pushState({}, "", `/r/${session.room.publicCode}`);
      window.dispatchEvent(new PopStateEvent("popstate"));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not join.");
    } finally {
      setBusy(null);
    }
  }

  return (
    <main className="page">
      <div className="stack" style={{ marginTop: 48 }}>
        <div className="brand">RelayRoom</div>
        <div className="landing-form">
          <button className="btn" type="button" onClick={() => void createRoom()} disabled={busy !== null}>
            {busy === "create" ? "Creating…" : "Create a room"}
          </button>
          <div className="label" style={{ marginTop: 16 }}>Join with a code</div>
          <form className="inline" onSubmit={(event) => void joinRoom(event)}>
            <input
              className="field"
              value={code}
              onChange={(event) => setCode(event.target.value.toUpperCase())}
              spellCheck={false}
              autoCapitalize="characters"
              autoComplete="off"
              placeholder="K7M2QX"
              aria-label="Room code"
            />
            <button className="btn" type="submit" disabled={!canJoin || busy !== null}>
              {busy === "join" ? "Joining…" : "Join"}
            </button>
          </form>
          {error ? <p className="error">{error}</p> : null}
        </div>
      </div>
    </main>
  );
}
