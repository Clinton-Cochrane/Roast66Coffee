import { useEffect } from "react";
import axios from "../axiosConfig";

const HEARTBEAT_INTERVAL_MS = 4 * 60 * 1000;

export default function useKeepAliveHeartbeat(enabled = true): void {
  useEffect(() => {
    if (!enabled) {
      return undefined;
    }

    let cancelled = false;

    const sendHeartbeat = async () => {
      if (cancelled || document.visibilityState !== "visible") {
        return;
      }

      try {
        await axios.post("/ops/keepalive/heartbeat", {
          source: "admin-dashboard",
        });
      } catch {
        // Keep-alive should never break admin workflows.
      }
    };

    void sendHeartbeat();
    const intervalId = window.setInterval(() => {
      void sendHeartbeat();
    }, HEARTBEAT_INTERVAL_MS);

    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        void sendHeartbeat();
      }
    };

    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => {
      cancelled = true;
      document.removeEventListener("visibilitychange", onVisibilityChange);
      window.clearInterval(intervalId);
    };
  }, [enabled]);
}
