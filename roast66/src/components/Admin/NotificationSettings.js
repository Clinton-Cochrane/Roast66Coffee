import React from 'react';

import { useEffect, useState } from "react";
import axios from "../../axiosConfig";
import "./NotificationSettings.css";

function NotificationSettings() {
  const [adminPhoneNumber, setAdminPhoneNumber] = useState("");
  const [baristaPhoneNumber, setBaristaPhoneNumber] = useState("");
  const [trailerPhoneNumber, setTrailerPhoneNumber] = useState("");

  useEffect(() => {
    fetchNotificationSettings();
  }, []);

  const fetchNotificationSettings = () => {
    axios
      .get("/admin/notificationSettings")
      .then((response) => {
        const data = response.data ?? {};
        setAdminPhoneNumber(data.adminPhoneNumber ?? "");
        setBaristaPhoneNumber(data.baristaPhoneNumber ?? "");
        setTrailerPhoneNumber(data.trailerPhoneNumber ?? "");
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
      })
      .then(() => alert("Notification settings saved successfully!"))
      .catch((error) => console.error(error));
  };

  return (
    <div className="notification-settings">
      <h2>Notification Settings</h2>
      <form onSubmit={handleSaveSettings}>
        <input
          type="text"
          placeholder="Admin Phone Number"
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
        <button type="submit"> Save Settings</button>
      </form>
    </div>
  );
}

export default NotificationSettings;
