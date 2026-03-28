/**
 * MenuBulkOperations - Seed, download, and upload menu as JSON.
 */
import React, { useState, type ChangeEvent } from "react";
import axios from "axios";
import axiosInstance from "../../axiosConfig";
import Button from "../common/Button";
import Card from "../common/Card";
import { toast } from "react-toastify";
import type { MenuItemDto } from "../../types/api";
import { useI18n } from "../../i18n/LanguageContext";

type MenuBulkOperationsProps = {
  onMenuUpdated?: () => void;
};

function MenuBulkOperations({ onMenuUpdated }: MenuBulkOperationsProps) {
  const { t } = useI18n();
  const [isSeeding, setIsSeeding] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [importError, setImportError] = useState("");

  const handleSeedMenu = async () => {
    if (!window.confirm(t("adminBulk.seedConfirm"))) {
      return;
    }
    setIsSeeding(true);
    try {
      await axiosInstance.get("/admin/seed-menu?confirm=true");
      toast.success(t("adminBulk.seedSuccess"));
      onMenuUpdated?.();
    } catch (err: unknown) {
      const msg = axios.isAxiosError(err)
        ? (err.response?.data as { message?: string })?.message
        : undefined;
      toast.error(msg || t("adminBulk.seedFailed"));
    } finally {
      setIsSeeding(false);
    }
  };

  const handleDownloadMenu = async () => {
    setIsExporting(true);
    try {
      const { data } = await axiosInstance.get<unknown>("/admin/menu/export");
      const blob = new Blob([JSON.stringify(data, null, 2)], {
        type: "application/json",
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `roast66-menu-${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);
      toast.success(t("adminBulk.downloadSuccess"));
    } catch (err: unknown) {
      const msg = axios.isAxiosError(err)
        ? (err.response?.data as { message?: string })?.message
        : undefined;
      toast.error(msg || t("adminBulk.downloadFailed"));
    } finally {
      setIsExporting(false);
    }
  };

  const handleFileUpload = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setImportError("");
    const reader = new FileReader();
    reader.onload = async (event) => {
      try {
        const text = event.target?.result;
        if (typeof text !== "string") {
          setImportError(t("adminBulk.couldNotReadFile"));
          return;
        }
        const json = JSON.parse(text) as unknown;
        const items = Array.isArray(json)
          ? json
          : json && typeof json === "object" && "menuItems" in json
            ? (json as { menuItems: MenuItemDto[] }).menuItems
            : json && typeof json === "object" && "items" in json
              ? (json as { items: MenuItemDto[] }).items
              : [];
        if (!Array.isArray(items) || items.length === 0) {
          setImportError(t("adminBulk.fileMustContainArray"));
          return;
        }
        setIsImporting(true);
        await axiosInstance.post("/admin/menu/import", items);
        toast.success(t("adminBulk.importCountSuccess", { count: items.length }));
        onMenuUpdated?.();
      } catch (err: unknown) {
        const msg =
          err instanceof SyntaxError
            ? t("adminBulk.invalidJson")
            : axios.isAxiosError(err)
              ? (err.response?.data as { message?: string })?.message || t("adminBulk.importFailed")
              : t("adminBulk.importFailed");
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
    <Card title={t("adminBulk.title")}>
      <p className="text-sm text-gray-600 mb-4">{t("adminBulk.description")}</p>
      <div className="flex flex-wrap gap-3">
        <Button onClick={handleSeedMenu} disabled={isSeeding} color="blue">
          {isSeeding ? t("adminBulk.seeding") : t("adminBulk.seedDefault")}
        </Button>
        <Button onClick={handleDownloadMenu} disabled={isExporting} color="green">
          {isExporting ? t("adminBulk.downloading") : t("adminBulk.downloadJson")}
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
            {isImporting ? t("adminBulk.importing") : t("adminBulk.uploadJson")}
          </span>
        </label>
      </div>
      {importError ? <p className="text-red-600 text-sm mt-2">{importError}</p> : null}
    </Card>
  );
}

export default MenuBulkOperations;
