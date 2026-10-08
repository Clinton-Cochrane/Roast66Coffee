import React, { useEffect, useId, useRef } from "react";
import { createPortal } from "react-dom";
import { FaXmark } from "react-icons/fa6";
import Button from "../common/Button";
import { useI18n } from "../../i18n/LanguageContext";
import "../../styles/Admin.css";

type PaymentChooserProps = {
  orderId: number;
  total: number | null | undefined;
  onClose: () => void;
  onViewOrder?: (orderId: number) => void;
};

function PaymentChooser({ orderId, total, onClose, onViewOrder }: PaymentChooserProps) {
  const { locale, t } = useI18n();
  const titleId = useId();
  const dialogRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const currencyFormatter = new Intl.NumberFormat(locale, { style: "currency", currency: "USD" });
  const hasTotal = typeof total === "number" && Number.isFinite(total) && total >= 0;

  useEffect(() => {
    const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    const root = document.getElementById("root");
    const wasInert = root?.hasAttribute("inert");
    root?.setAttribute("inert", "");
    document.body.style.overflow = "hidden";
    closeButtonRef.current?.focus();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }
      if (event.key !== "Tab" || !dialogRef.current) return;
      const focusableElements = Array.from(dialogRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
      ));
      const first = focusableElements[0];
      const last = focusableElements[focusableElements.length - 1];
      if (!first || !last) return;
      if (!dialogRef.current.contains(document.activeElement)) {
        event.preventDefault();
        first.focus();
      } else if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = previousOverflow;
      if (!wasInert) root?.removeAttribute("inert");
      if (trigger?.isConnected) trigger.focus();
    };
  }, [onClose]);

  return createPortal(
    <div className="r66-payment-backdrop" onClick={(event) => {
      if (event.target === event.currentTarget) onClose();
    }}>
      <section
        ref={dialogRef}
        className="r66-payment-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <div className="flex items-center justify-between gap-4 mb-4">
          <h2 id={titleId} className="text-2xl font-bold">{t("adminOrders.takePayment")}</h2>
          <button
            ref={closeButtonRef}
            type="button"
            aria-label={t("adminOrders.closePaymentChooser")}
            onClick={onClose}
            className="min-h-11 min-w-11 flex items-center justify-center rounded-md focus:outline-hidden focus:ring-2 focus:ring-[#99bfdd]"
          >
            <FaXmark aria-hidden="true" />
          </button>
        </div>
        <p className="text-lg font-semibold mb-3">{t("adminOrders.orderCardTitle", { id: orderId })}</p>
        <div className="flex flex-wrap items-baseline justify-between gap-3 mb-6">
          <span>{t("order.total")}</span>
          <span className="text-2xl font-bold">
            {hasTotal ? currencyFormatter.format(total) : t("adminOrders.totalUnavailable")}
          </span>
        </div>
        <Button color="gray" disabled={!onViewOrder} onClick={() => onViewOrder?.(orderId)} className="w-full mb-4">
          {t("adminOrders.viewOrder")}
        </Button>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <Button disabled>{t("adminOrders.cashPayment")}</Button>
          <Button disabled>{t("adminOrders.cardPayment")}</Button>
          <Button disabled>{t("adminOrders.otherPayment")}</Button>
        </div>
      </section>
    </div>,
    document.body
  );
}

export default PaymentChooser;
