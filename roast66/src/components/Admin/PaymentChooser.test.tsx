import React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { LanguageProvider } from "../../i18n/LanguageContext";
import PaymentChooser from "./PaymentChooser";

describe("PaymentChooser", () => {
  beforeEach(() => {
    localStorage.setItem("roast66_locale", "en");
  });

  afterEach(() => {
    localStorage.removeItem("roast66_locale");
    document.getElementById("root")?.remove();
    document.body.style.overflow = "";
  });

  it("shows the order, formatted total, and disabled choices without Cancel", () => {
    const onClose = vi.fn();
    render(<LanguageProvider><PaymentChooser orderId={66} total={7.25} onClose={onClose} /></LanguageProvider>);
    const chooser = screen.getByRole("dialog", { name: "Take Payment" });

    expect(chooser).toHaveAttribute("aria-modal", "true");
    expect(within(chooser).getByText("Order #66")).toBeInTheDocument();
    expect(within(chooser).getByText("Total")).toBeInTheDocument();
    expect(within(chooser).getByText("$7.25")).toBeInTheDocument();
    for (const name of ["View Order", "Cash", "Card", "Other"]) {
      const choice = within(chooser).getByRole("button", { name });
      expect(choice).toBeDisabled();
      fireEvent.click(choice);
    }
    fireEvent.click(within(chooser).getByText("Order #66"));
    expect(chooser).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Cancel" })).not.toBeInTheDocument();
  });

  it.each([undefined, null])("does not invent a total when the server total is %s", (total) => {
    render(<LanguageProvider><PaymentChooser orderId={66} total={total} onClose={vi.fn()} /></LanguageProvider>);
    expect(screen.getByText("Total unavailable")).toBeInTheDocument();
    expect(screen.queryByText("$0.00")).not.toBeInTheDocument();
  });

  it("displays a valid zero total", () => {
    render(<LanguageProvider><PaymentChooser orderId={66} total={0} onClose={vi.fn()} /></LanguageProvider>);
    expect(screen.getByText("$0.00")).toBeInTheDocument();
    expect(screen.queryByText("Total unavailable")).not.toBeInTheDocument();
  });

  it.each(["Cash", "Other"])("requires confirmation before recording %s and supports going back", (label) => {
    const onRecordPayment = vi.fn();
    render(<LanguageProvider><PaymentChooser orderId={66} total={7.25} onClose={vi.fn()} onRecordPayment={onRecordPayment} /></LanguageProvider>);
    expect(screen.getByRole("button", { name: "Card" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: label }));
    expect(screen.getByText(`Have you received $7.25 by ${label} for Order #66?`)).toBeInTheDocument();
    expect(onRecordPayment).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Confirm payment received" })).toHaveFocus();
    fireEvent.click(screen.getByRole("button", { name: "Back" }));
    expect(screen.getByRole("button", { name: label })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: label }));
    fireEvent.click(screen.getByRole("button", { name: "Confirm payment received" }));
    expect(onRecordPayment).toHaveBeenCalledExactlyOnceWith(label.toLowerCase());
  });

  it("cannot start a manual payment without an authoritative total", () => {
    render(<LanguageProvider><PaymentChooser orderId={66} total={null} onClose={vi.fn()} onRecordPayment={vi.fn()} /></LanguageProvider>);
    expect(screen.getByRole("button", { name: "Cash" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Other" })).toBeDisabled();
  });

  it("traps focus between the close button and enabled payment choices", () => {
    render(<LanguageProvider><PaymentChooser orderId={66} total={7.25} onClose={vi.fn()} onRecordPayment={vi.fn()} /></LanguageProvider>);
    const close = screen.getByRole("button", { name: "Close payment chooser" });
    const other = screen.getByRole("button", { name: "Other" });
    fireEvent.keyDown(document, { key: "Tab", shiftKey: true });
    expect(other).toHaveFocus();
    fireEvent.keyDown(document, { key: "Tab" });
    expect(close).toHaveFocus();
  });

  it("translates manual payment confirmation", () => {
    localStorage.setItem("roast66_locale", "es");
    render(<LanguageProvider><PaymentChooser orderId={66} total={7.25} onClose={vi.fn()} onRecordPayment={vi.fn()} /></LanguageProvider>);
    fireEvent.click(screen.getByRole("button", { name: "Efectivo" }));
    expect(screen.getByText(/¿Recibiste .* mediante Efectivo para el pedido #66\?/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Confirmar pago recibido" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Volver" })).toBeEnabled();
  });

  it("enables View Order and passes the exact selected order ID to its callback", () => {
    const onViewOrder = vi.fn();
    const onClose = vi.fn();
    render(<LanguageProvider>
      <PaymentChooser orderId={66} total={7.25} onClose={onClose} onViewOrder={onViewOrder} />
    </LanguageProvider>);
    const viewOrder = screen.getByRole("button", { name: "View Order" });
    expect(viewOrder).toBeEnabled();
    fireEvent.click(viewOrder);
    expect(onViewOrder).toHaveBeenCalledWith(66);
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Cash" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Card" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Other" })).toBeDisabled();
  });

  it.each(["escape", "outside", "close icon"])("dismisses via %s", (method) => {
    const onClose = vi.fn();
    render(<LanguageProvider><PaymentChooser orderId={66} total={7.25} onClose={onClose} /></LanguageProvider>);
    const chooser = screen.getByRole("dialog");

    if (method === "escape") fireEvent.keyDown(document, { key: "Escape" });
    if (method === "outside") fireEvent.click(chooser.parentElement!);
    if (method === "close icon") fireEvent.click(screen.getByRole("button", { name: "Close payment chooser" }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("traps focus and restores background scrolling, focus, and inert state on unmount", () => {
    const root = document.createElement("div");
    root.id = "root";
    document.body.appendChild(root);
    const trigger = document.createElement("button");
    root.appendChild(trigger);
    trigger.focus();
    document.body.style.overflow = "auto";
    const { unmount } = render(<LanguageProvider>
      <PaymentChooser orderId={66} total={7.25} onClose={vi.fn()} />
    </LanguageProvider>);
    const close = screen.getByRole("button", { name: "Close payment chooser" });

    expect(close).toHaveFocus();
    expect(root).toHaveAttribute("inert");
    expect(document.body.style.overflow).toBe("hidden");
    fireEvent.keyDown(document, { key: "Tab" });
    expect(close).toHaveFocus();
    fireEvent.keyDown(document, { key: "Tab", shiftKey: true });
    expect(close).toHaveFocus();

    unmount();
    expect(trigger).toHaveFocus();
    expect(root).not.toHaveAttribute("inert");
    expect(document.body.style.overflow).toBe("auto");
    root.remove();
    document.body.style.overflow = "";
  });

  it("preserves an already inert background and removes its dismissal listener", () => {
    const root = document.createElement("div");
    root.id = "root";
    root.setAttribute("inert", "");
    document.body.appendChild(root);
    const onClose = vi.fn();
    const { unmount } = render(<LanguageProvider>
      <PaymentChooser orderId={66} total={7.25} onClose={onClose} />
    </LanguageProvider>);
    unmount();

    expect(root).toHaveAttribute("inert");
    fireEvent.keyDown(document, { key: "Escape" });
    expect(onClose).not.toHaveBeenCalled();
    root.remove();
  });

  it("translates the chooser labels", () => {
    localStorage.setItem("roast66_locale", "es");
    render(<LanguageProvider><PaymentChooser orderId={66} total={7.25} onClose={vi.fn()} /></LanguageProvider>);
    const chooser = screen.getByRole("dialog", { name: "Cobrar" });

    expect(within(chooser).getByText("Pedido #66")).toBeInTheDocument();
    expect(within(chooser).getByRole("button", { name: "Ver pedido" })).toBeDisabled();
    expect(within(chooser).getByRole("button", { name: "Efectivo" })).toBeDisabled();
    expect(within(chooser).getByRole("button", { name: "Tarjeta" })).toBeDisabled();
    expect(within(chooser).getByRole("button", { name: "Otro" })).toBeDisabled();
  });
});
