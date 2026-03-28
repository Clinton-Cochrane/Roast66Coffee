import React, { useEffect, useState } from "react";
import Button from "../common/Button";
import { API_BASE_URL } from "../../config";
import { useI18n } from "../../i18n/LanguageContext";

function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
  const rawData = window.atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; i += 1) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

function getAuthHeaders(): Record<string, string> {
  const token = localStorage.getItem("token");
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function getVapidPublicKey(): Promise<string | null> {
  const response = await fetch(`${API_BASE_URL}/admin/push/vapid-public-key`, {
    method: "GET",
    headers: {
      ...getAuthHeaders(),
    },
  });

  if (!response.ok) {
    return null;
  }

  const data = (await response.json()) as { publicKey?: string };
  return data?.publicKey ?? null;
}

async function syncSubscriptionToServer(subscription: PushSubscription): Promise<void> {
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
  const [installEvent, setInstallEvent] = useState<BeforeInstallPromptEvent | null>(null);
  const [isInstallRecommended, setIsInstallRecommended] = useState(false);

  useEffect(() => {
    const onBeforeInstallPrompt = (event: Event) => {
      event.preventDefault();
      setInstallEvent(event as BeforeInstallPromptEvent);
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
      } catch {
        // Silently ignore non-critical subscription errors in UI.
      }
    };

    void ensureSubscribed();
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
    } catch {
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
      {shouldShowPermissionPrompt ? (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-yellow-900">{t("staffDevice.promptNotifications")}</p>
          <Button color="blue" onClick={() => void requestPermission()}>
            {t("staffDevice.enableNotifications")}
          </Button>
        </div>
      ) : null}
      {shouldShowInstallPrompt ? (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-yellow-900">{t("staffDevice.promptInstall")}</p>
          <Button color="green" onClick={() => void handleInstall()}>
            {t("staffDevice.installApp")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

export default StaffDevicePrompt;
