import React from 'react';

import { useEffect, useState } from "react";
import axios from "../../axiosConfig";
import "./NotificationSettings.css";

function NotificationSettings() {
  const [adminPhoneNumber, setAdminPhoneNumber] = useState("");
  const [baristaPhoneNumber, setBaristaPhoneNumber] = useState("");
  const [trailerPhoneNumber, setTrailerPhoneNumber] = useState("");
  const [twilioFromPhoneNumber, setTwilioFromPhoneNumber] = useState("");
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
        setAdminPhoneNumber(data.adminPhoneNumber ?? "");
        setBaristaPhoneNumber(data.baristaPhoneNumber ?? "");
        setTrailerPhoneNumber(data.trailerPhoneNumber ?? "");
        setTwilioFromPhoneNumber(data.twilioFromPhoneNumber ?? "");
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
        adminPhoneNumber: adminPhoneNumber.trim(),
        baristaPhoneNumber: baristaPhoneNumber.trim(),
        trailerPhoneNumber: trailerPhoneNumber.trim(),
        twilioFromPhoneNumber: twilioFromPhoneNumber.trim(),
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
          Set who receives new-order alerts and which Twilio sender number is used for outbound SMS.
        </p>
        <input
          type="text"
          placeholder="New Order Recipient (Admin) Phone Number"
          value={adminPhoneNumber}
          onChange={(e) => setAdminPhoneNumber(e.target.value)}
        />
        <input
          type="text"
          placeholder="Barista Phone Number"
          value={baristaPhoneNumber}
          onChange={(e) => setBaristaPhoneNumber(e.target.value)}
        />
        <input
          type="text"
          placeholder="Trailer Phone Number"
          value={trailerPhoneNumber}
          onChange={(e) => setTrailerPhoneNumber(e.target.value)}
        />
        <input
          type="text"
          placeholder="Twilio Sender Number (From)"
          value={twilioFromPhoneNumber}
          onChange={(e) => setTwilioFromPhoneNumber(e.target.value)}
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
