import { useEffect, useState } from "react";
import { LandingPage } from "./pages/LandingPage";
import { RoomPage } from "./pages/RoomPage";

function routeCode(): string | null {
  const match = window.location.pathname.match(/^\/r\/([A-Za-z0-9]+)/i);
  return match?.[1] ?? null;
}

export default function App() {
  const [code, setCode] = useState(routeCode);

  useEffect(() => {
    const onNav = () => setCode(routeCode());
    window.addEventListener("popstate", onNav);
    return () => window.removeEventListener("popstate", onNav);
  }, []);

  return code ? <RoomPage code={code} /> : <LandingPage />;
}
