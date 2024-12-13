import { useEffect, useState } from "react";
import axios from "../../axiosConfig";
import "./NotificationSettings.css";

function NotificationSettings() {
  const [adminPhoneNumber, setAdminPhoneNumber] = useState("");

  useEffect(() => {
    fetchNotificationSettings();
  }, []);

  const fetchNotificationSettings = () => {
    axios
      .get("/admin/notificationSettings")
      .then((response) => setAdminPhoneNumber(response.data.phoneNumber))
      .catch((error) => console.error(error));
  };

  const handleSaveSettings = (e) => {
    e.preventDefault();
    axios
      .put("/admin/notificationSettings", { phoneNumber: adminPhoneNumber })
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
          required
        />
        <button type="submit"> Save Settings</button>
      </form>
    </div>
  );
}

export default NotificationSettings;
