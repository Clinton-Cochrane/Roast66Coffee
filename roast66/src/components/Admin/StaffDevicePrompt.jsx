import React, { useEffect, useState } from "react";
import Button from "../common/Button";
import { API_BASE_URL } from "../../config";

function urlBase64ToUint8Array(base64String) {
  const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
  const rawData = window.atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

function getAuthHeaders() {
  const token = localStorage.getItem("token");
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function getVapidPublicKey() {
  const response = await fetch(`${API_BASE_URL}/admin/push/vapid-public-key`, {
    method: "GET",
    headers: {
      ...getAuthHeaders(),
    },
  });

  if (!response.ok) {
    return null;
  }

  const data = await response.json();
  return data?.publicKey ?? null;
}

async function syncSubscriptionToServer(subscription) {
  const json = subscription.toJSON();
  await fetch(`${API_BASE_URL}/admin/push/subscriptions`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...getAuthHeaders(),
    },
    body: JSON.stringify({
      endpoint: json.endpoint,
      keys: {
        p256dh: json.keys?.p256dh || "",
        auth: json.keys?.auth || "",
      },
    }),
  });
}

function StaffDevicePrompt() {
  const [notificationPermission, setNotificationPermission] = useState(
    typeof Notification !== "undefined" ? Notification.permission : "default"
  );
  const [installEvent, setInstallEvent] = useState(null);
  const [isInstallRecommended, setIsInstallRecommended] = useState(false);

  useEffect(() => {
    const onBeforeInstallPrompt = (event) => {
      event.preventDefault();
      setInstallEvent(event);
      setIsInstallRecommended(true);
    };

    window.addEventListener("beforeinstallprompt", onBeforeInstallPrompt);
    return () => window.removeEventListener("beforeinstallprompt", onBeforeInstallPrompt);
  }, []);

  useEffect(() => {
    const ensureSubscribed = async () => {
      if (
        typeof Notification === "undefined" ||
        Notification.permission !== "granted" ||
        !("serviceWorker" in navigator)
      ) {
        return;
      }

      try {
        const registration = await navigator.serviceWorker.ready;
        let subscription = await registration.pushManager.getSubscription();

        if (!subscription) {
          const publicKey = await getVapidPublicKey();
          if (!publicKey) return;

          subscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(publicKey),
          });
        }

        await syncSubscriptionToServer(subscription);
      } catch (err) {
        // Silently ignore non-critical subscription errors in UI.
      }
    };

    ensureSubscribed();
  }, [notificationPermission]);

  const requestPermission = async () => {
    if (typeof Notification === "undefined") return;
    const permission = await Notification.requestPermission();
    setNotificationPermission(permission);
    if (permission !== "granted" || !("serviceWorker" in navigator)) {
      return;
    }

    try {
      const registration = await navigator.serviceWorker.ready;
      let subscription = await registration.pushManager.getSubscription();
      if (!subscription) {
        const publicKey = await getVapidPublicKey();
        if (!publicKey) return;

        subscription = await registration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: urlBase64ToUint8Array(publicKey),
        });
      }

      await syncSubscriptionToServer(subscription);
    } catch (err) {
      // Keep UX simple; the prompt can be retried on next session.
    }
  };

  const handleInstall = async () => {
    if (!installEvent) return;
    await installEvent.prompt();
    setInstallEvent(null);
    setIsInstallRecommended(false);
  };

  const shouldShowPermissionPrompt =
    typeof Notification !== "undefined" && notificationPermission === "default";
  const shouldShowInstallPrompt = isInstallRecommended && Boolean(installEvent);

  if (!shouldShowPermissionPrompt && !shouldShowInstallPrompt) {
    return null;
  }

  return (
    <div className="bg-yellow-50 border border-yellow-200 rounded-md p-4 mb-4 space-y-3">
      {shouldShowPermissionPrompt && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-yellow-900">
            Enable notifications on this staff device so new order alerts can appear right away.
          </p>
          <Button color="blue" onClick={requestPermission}>
            Enable Notifications
          </Button>
        </div>
      )}
      {shouldShowInstallPrompt && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-yellow-900">
            Install this app on your device for faster access during service.
          </p>
          <Button color="green" onClick={handleInstall}>
            Install App
          </Button>
        </div>
      )}
    </div>
  );
}

export default StaffDevicePrompt;
