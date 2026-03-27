import React from 'react';

import { useEffect, useState } from "react";
import axios from "../../axiosConfig";
import "./NotificationSettings.css";

function NotificationSettings() {
  const [adminEmail, setAdminEmail] = useState("");
  const [baristaEmail, setBaristaEmail] = useState("");
  const [trailerEmail, setTrailerEmail] = useState("");
  const [credentialInfo, setCredentialInfo] = useState(null);
  const [credentialRequestMessage, setCredentialRequestMessage] = useState("");
  const [isSubmittingCredentialRequest, setIsSubmittingCredentialRequest] = useState(false);

  useEffect(() => {
    fetchNotificationSettings();
    fetchCredentialSettingsInfo();
  }, []);

  const fetchNotificationSettings = () => {
    axios
      .get("/admin/notificationSettings")
      .then((response) => {
        const data = response.data ?? {};
        setAdminEmail(data.adminEmail ?? "");
        setBaristaEmail(data.baristaEmail ?? "");
        setTrailerEmail(data.trailerEmail ?? "");
      })
      .catch((error) => console.error(error));
  };

  const fetchCredentialSettingsInfo = () => {
    axios
      .get("/admin/credential-settings")
      .then((response) => {
        setCredentialInfo(response.data ?? null);
      })
      .catch((error) => console.error(error));
  };

  const handleSaveSettings = (e) => {
    e.preventDefault();
    axios
      .put("/admin/notificationSettings", {
        adminEmail: adminEmail.trim(),
        baristaEmail: baristaEmail.trim(),
        trailerEmail: trailerEmail.trim(),
      })
      .then(() => alert("Notification settings saved successfully!"))
      .catch((error) => console.error(error));
  };

  const handleCredentialRequest = (e) => {
    e.preventDefault();
    setIsSubmittingCredentialRequest(true);
    axios
      .post("/admin/forgot-password", {
        message: credentialRequestMessage.trim(),
      })
      .then(() => {
        alert("Credential update request sent to support.");
        setCredentialRequestMessage("");
      })
      .catch((error) => console.error(error))
      .finally(() => setIsSubmittingCredentialRequest(false));
  };

  return (
    <div className="notification-settings">
      <h2>Notification Settings</h2>
      <form onSubmit={handleSaveSettings}>
        <p className="helper-text">
          Set who receives email notifications for order updates. SMS is currently disabled.
        </p>
        <input
          type="email"
          placeholder="Admin notification email"
          value={adminEmail}
          onChange={(e) => setAdminEmail(e.target.value)}
        />
        <input
          type="email"
          placeholder="Barista notification email"
          value={baristaEmail}
          onChange={(e) => setBaristaEmail(e.target.value)}
        />
        <input
          type="email"
          placeholder="Trailer notification email"
          value={trailerEmail}
          onChange={(e) => setTrailerEmail(e.target.value)}
        />
        <button type="submit"> Save Settings</button>
      </form>
      <section className="credentials-settings">
        <h2>Username / Password Settings</h2>
        <p className="helper-text">
          Credentials are managed through environment variables and are not edited directly in the app.
        </p>
        <p className="helper-text">
          Current username: <strong>{credentialInfo?.username ?? "Unavailable"}</strong>
        </p>
        <p className="helper-text">
          Update <code>{credentialInfo?.usernameEnvKey ?? "Admin__Username"}</code> and{" "}
          <code>{credentialInfo?.passwordEnvKey ?? "Admin__Password"}</code> in your deploy provider, then redeploy the API.
        </p>
        <form onSubmit={handleCredentialRequest}>
          <textarea
            placeholder="Optional message to support for credential update request"
            value={credentialRequestMessage}
            onChange={(e) => setCredentialRequestMessage(e.target.value)}
            rows={3}
          />
          <button type="submit" disabled={isSubmittingCredentialRequest}>
            {isSubmittingCredentialRequest ? "Sending..." : "Request Credential Update"}
          </button>
        </form>
      </section>
    </div>
  );
}

export default NotificationSettings;
