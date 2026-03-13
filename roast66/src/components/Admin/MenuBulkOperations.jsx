/**
 * MenuBulkOperations - Seed, download, and upload menu as JSON.
 * For mass changes: download JSON, edit, re-upload.
 * For small changes: use ManageMenu form.
 */
import React, { useState } from "react";
import axios from "../../axiosConfig";
import Button from "../common/Button";
import Card from "../common/Card";
import { toast } from "react-toastify";

function MenuBulkOperations({ onMenuUpdated }) {
  const [isSeeding, setIsSeeding] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [importError, setImportError] = useState("");

  const handleSeedMenu = async () => {
    if (!window.confirm("This will replace all menu items with the default seed. Continue?")) {
      return;
    }
    setIsSeeding(true);
    try {
      await axios.get("/admin/seed-menu?confirm=true");
      toast.success("Menu seeded successfully.");
      onMenuUpdated?.();
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to seed menu.");
    } finally {
      setIsSeeding(false);
    }
  };

  const handleDownloadMenu = async () => {
    setIsExporting(true);
    try {
      const { data } = await axios.get("/admin/menu/export");
      const blob = new Blob([JSON.stringify(data, null, 2)], {
        type: "application/json",
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `roast66-menu-${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);
      toast.success("Menu downloaded.");
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to download menu.");
    } finally {
      setIsExporting(false);
    }
  };

  const handleFileUpload = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setImportError("");
    const reader = new FileReader();
    reader.onload = async (event) => {
      try {
        const json = JSON.parse(event.target.result);
        const items = Array.isArray(json) ? json : json.menuItems ?? json.items ?? [];
        if (items.length === 0) {
          setImportError("File must contain an array of menu items.");
          return;
        }
        setIsImporting(true);
        await axios.post("/admin/menu/import", items);
        toast.success(`Imported ${items.length} menu items.`);
        onMenuUpdated?.();
      } catch (err) {
        const msg = err instanceof SyntaxError
          ? "Invalid JSON file."
          : err.response?.data?.message || "Import failed.";
        setImportError(msg);
        toast.error(msg);
      } finally {
        setIsImporting(false);
        e.target.value = "";
      }
    };
    reader.readAsText(file);
  };

  return (
    <Card title="Bulk Menu Operations">
      <p className="text-sm text-gray-600 mb-4">
        Download menu as JSON to edit offline, or upload a JSON file to replace the entire menu.
        Use the form below for small edits.
      </p>
      <div className="flex flex-wrap gap-3">
        <Button
          onClick={handleSeedMenu}
          disabled={isSeeding}
          color="blue"
        >
          {isSeeding ? "Seeding…" : "Seed Default Menu"}
        </Button>
        <Button
          onClick={handleDownloadMenu}
          disabled={isExporting}
          color="green"
        >
          {isExporting ? "Downloading…" : "Download Menu (JSON)"}
        </Button>
        <label className="cursor-pointer inline-block">
          <input
            type="file"
            accept=".json"
            onChange={handleFileUpload}
            disabled={isImporting}
            className="hidden"
          />
          <span
            className={`inline-block bg-green-900 hover:bg-green-500 text-white py-2 px-4 rounded ${
              isImporting ? "opacity-50 cursor-not-allowed" : ""
            }`}
          >
            {isImporting ? "Importing…" : "Upload JSON"}
          </span>
        </label>
      </div>
      {importError && (
        <p className="text-red-600 text-sm mt-2">{importError}</p>
      )}
    </Card>
  );
}

export default MenuBulkOperations;
