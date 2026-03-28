import React, { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { useNavigate, useLocation, Link } from "react-router-dom";
import axios from "axios";
import axiosInstance from "../axiosConfig";
import { toast } from "react-toastify";
import "../styles/OrderPage.css";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import CategoryType from "../constants/categories";
import { useI18n } from "../i18n/LanguageContext";
import { canOrderMenuItemDirectly } from "../utils/canOrderMenuItemDirectly";
import type { MenuItemDto, OrderDto } from "../types/api";

const ENABLE_STRIPE_CHECKOUT = import.meta.env.VITE_ENABLE_STRIPE_CHECKOUT === "true";

type CartAddOn = MenuItemDto & { quantity: number };
type CartLine = MenuItemDto & { quantity: number; notes: string; addOns: CartAddOn[] };

function OrderPage() {
  const { locale, t } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuItems, setMenuItems] = useState<MenuItemDto[]>([]);
  const [orderItems, setOrderItems] = useState<CartLine[]>([]);
  const [customerName, setCustomerName] = useState("");
  const [customerEmail, setCustomerEmail] = useState("");

  const wakeInFlightRef = useRef(false);
  const prefillAppliedForLocationKeyRef = useRef<string | null>(null);

  useEffect(() => {
    void ensureMenuItemsLoaded();
  }, []);

  const fetchMenuItems = async (): Promise<number> => {
    try {
      const response = await axiosInstance.get<MenuItemDto[]>("/menu");
      const items = Array.isArray(response.data) ? response.data : [];
      setMenuItems(items);
      return items.length;
    } catch (error: unknown) {
      console.error(error);
      return 0;
    }
  };

  const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

  const ensureMenuItemsLoaded = async () => {
    if (wakeInFlightRef.current) return;
    wakeInFlightRef.current = true;
    try {
      const maxAttempts = 4;
      for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
        const count = await fetchMenuItems();
        if (count > 0) return;
        if (attempt < maxAttempts - 1) {
          await sleep(1200);
        }
      }
    } finally {
      wakeInFlightRef.current = false;
    }
  };

  const handleDropdownFocus = () => {
    if (menuItems.length === 0) {
      void ensureMenuItemsLoaded();
    }
  };

  const handleDropDownChange = (e: ChangeEvent<HTMLSelectElement>) => {
    const value = e.target.value;
    if (!value) {
      return;
    }
    const item = JSON.parse(value) as MenuItemDto;
    addItemToOrder(item);
  };

  const addItemToOrder = (item: MenuItemDto) => {
    if (canOrderMenuItemDirectly(item)) {
      setOrderItems((prev) => [...prev, { ...item, quantity: 1, notes: "", addOns: [] }]);
    } else {
      toast.warning(t("order.flavorStandaloneWarning"));
    }
  };

  useEffect(() => {
    const state = location.state as { menuItemId?: number } | null | undefined;
    const menuItemId = state?.menuItemId;
    if (menuItemId == null || menuItems.length === 0) {
      return;
    }
    if (prefillAppliedForLocationKeyRef.current === location.key) {
      return;
    }

    const id = Number(menuItemId);
    const item = menuItems.find((m) => m.id === id);

    const clearPrefillState = () => {
      navigate("/order", { replace: true, state: {} });
    };

    if (!item) {
      prefillAppliedForLocationKeyRef.current = location.key;
      clearPrefillState();
      return;
    }
    if (!canOrderMenuItemDirectly(item)) {
      toast.warning(t("order.flavorStandaloneWarning"));
      prefillAppliedForLocationKeyRef.current = location.key;
      clearPrefillState();
      return;
    }

    prefillAppliedForLocationKeyRef.current = location.key;
    setOrderItems((prev) => [...prev, { ...item, quantity: 1, notes: "", addOns: [] }]);
    clearPrefillState();
  }, [menuItems, location.key, location.state, navigate, t]);

  const currencyFormatter = new Intl.NumberFormat(locale, {
    style: "currency",
    currency: "USD",
  });

  const calculateTotalPrice = (item: CartLine) => {
    const basePrice = item.price * item.quantity;
    const addOnsPrice = item.addOns.reduce(
      (total, addOn) => total + addOn.price * addOn.quantity,
      0
    );
    return basePrice + addOnsPrice;
  };

  const calculateOrderTotal = () => {
    return orderItems.reduce((total, item) => total + calculateTotalPrice(item), 0);
  };

  const handleQuantityChange = (index: number, quantity: string) => {
    const newQuantity = Math.min(parseInt(quantity, 10) || 1, 99);
    const newOrderItems = [...orderItems];
    newOrderItems[index].quantity = newQuantity;
    setOrderItems(newOrderItems);
  };

  const handleNotesChange = (index: number, notes: string) => {
    const newOrderItems = [...orderItems];
    newOrderItems[index].notes = notes;
    setOrderItems(newOrderItems);
  };

  const handleRemoveItem = (index: number) => {
    const removedItem = orderItems[index];
    const newOrderItems = orderItems.filter((_, i) => i !== index);
    setOrderItems(newOrderItems);
    toast.info(t("order.itemRemoved", { itemName: removedItem.name }));
  };

  const handleAddFlavor = (index: number, flavor: MenuItemDto) => {
    if (!flavor?.id) return;

    const newOrderItems = [...orderItems];
    const addOns = newOrderItems[index].addOns;

    if (!addOns.some((addOn) => addOn.id === flavor.id)) {
      addOns.push({ ...flavor, quantity: 1 });
      setOrderItems(newOrderItems);
    } else {
      toast.warning(t("order.addOnDuplicateWarning"));
    }

    const el = document.getElementById(`flavor-select-${index}`) as HTMLSelectElement | null;
    if (el) el.value = "";
  };

  const hasValidEmail = (email: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());

  const handleOrderSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (orderItems.length === 0) {
      toast.error(t("order.orderRequiredError"));
      return;
    }
    const orderData = {
      customerName,
      customerPhone: null,
      customerEmail: customerEmail.trim() || null,
      customerNotificationOptIn: hasValidEmail(customerEmail),
      orderItems: orderItems.map((item) => ({
        menuItemId: item.id,
        quantity: item.quantity,
        notes: item.notes,
        addOns: item.addOns.map((addOn) => ({
          menuItemId: addOn.id,
          quantity: addOn.quantity,
        })),
      })),
    };
    if (ENABLE_STRIPE_CHECKOUT) {
      const idempotencyKey =
        window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`;
      axiosInstance
        .post("/payments/checkout-session", orderData, {
          headers: { "X-Idempotency-Key": idempotencyKey },
        })
        .then((response) => {
          const checkoutUrl = response?.data?.checkoutUrl as string | undefined;
          if (!checkoutUrl) {
            throw new Error(t("order.checkoutMissingUrl"));
          }
          window.location.assign(checkoutUrl);
        })
        .catch((error: unknown) => {
          console.error("Checkout session creation failed:", error);
          toast.error(t("order.checkoutFailed"));
        });
      return;
    }

    axiosInstance
      .post<OrderDto>("/admin/orders", orderData)
      .then((response) => {
        const createdOrder = response.data;
        setOrderItems([]);
        setCustomerName("");
        setCustomerEmail("");
        navigate("/order/confirmation", { state: { order: createdOrder } });
      })
      .catch((error: unknown) => {
        if (axios.isAxiosError(error) && error.response?.status === 409) {
          const existingOrder = error.response?.data?.order as OrderDto | undefined;
          const existingOrderId = error.response?.data?.existingOrderId as number | undefined;
          setOrderItems([]);
          setCustomerName("");
          setCustomerEmail("");
          navigate("/order/duplicate", {
            state: { order: existingOrder, existingOrderId },
          });
        } else {
          console.error(
            "Order submission failed:",
            axios.isAxiosError(error) ? error.response?.status : undefined,
            axios.isAxiosError(error) ? error.response?.data : undefined,
            error
          );
          toast.error(t("order.submitFailed"));
        }
      });
  };

  return (
    <div className="p-6 flex flex-col items-center">
      <div className="w-full max-w-5xl">
        <div className="flex flex-wrap items-center justify-between gap-4 mb-3">
          <h1 className="text-3xl md:text-4xl font-bold tracking-[0.01em] text-[#4a3326]">
            {t("order.placeYourOrder")}
          </h1>
          <Link to="/order-status" className="text-[#6c89a2] hover:underline font-semibold">
            {t("order.checkOrderStatus")} →
          </Link>
        </div>
        <p className="r66-subtitle mb-6">Build your homemade drink for the road in just a few taps.</p>

        <div className="customer-info-container r66-panel p-4">
          <div className="flex-1 min-w-0">
            <FormInput
              type="text"
              name="customerName"
              placeholder={t("order.namePlaceholder")}
              title={t("order.namePlaceholder")}
              aria-label={t("order.namePlaceholder")}
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
              required
            />
          </div>
          <div className="flex-1 min-w-0">
            <FormInput
              type="email"
              name="customerEmail"
              placeholder={t("order.emailPlaceholder")}
              title={t("order.emailPlaceholder")}
              aria-label={t("order.emailPlaceholder")}
              value={customerEmail}
              onChange={(e) => setCustomerEmail(e.target.value)}
            />
            <div className="group relative -mt-1">
              <span
                tabIndex={0}
                className="block w-full cursor-help overflow-hidden text-ellipsis whitespace-nowrap text-[11px] leading-tight text-[#7a675a] focus:outline-none focus:text-[#5b4940]"
                title="We only send order status updates when a valid email address is provided."
              >
                We only send order status updates when a valid email address is provided.
              </span>
              <div className="pointer-events-none absolute bottom-full left-1 z-20 mb-1 hidden w-64 rounded-md border border-[#d8c8ba] bg-[#fffaf3] px-2 py-1 text-[11px] leading-tight text-[#5b4940] shadow-md group-hover:block group-focus-within:block">
                We only send order status updates when a valid email address is provided.
              </div>
            </div>
          </div>
        </div>

        <p className="text-[#5b4940] text-sm mb-4 text-center">{t("order.instructions")}</p>

        <div className="flex items-center space-x-4 mb-4">
          <select
            id="menu-select"
            onChange={handleDropDownChange}
            onFocus={handleDropdownFocus}
            className="w-full p-2 border border-[#cbb8a8] rounded-md mb-4 bg-[#fffaf3]"
            aria-label={t("order.selectMenuItem")}
          >
            <option value="">{t("order.selectMenuItem")}</option>
            {menuItems
              .filter((item) => canOrderMenuItemDirectly(item))
              .map((item) => (
                <option key={item.id} value={JSON.stringify(item)}>
                  {item.name} - {currencyFormatter.format(item.price)} - {item.description}
                </option>
              ))}
          </select>
        </div>

        <form onSubmit={handleOrderSubmit} className="space-y-4">
          <ul className="space-y-4 order-items-list">
            {orderItems.map((item, index) => (
              <li key={index} className="order-item">
                <div className="order-item-header">
                  <div className="order-item-quantity">
                    <FormInput
                      type="number"
                      label={t("order.quantityLabel", { itemName: item.name })}
                      className="quantity-input"
                      value={item.quantity}
                      min={1}
                      max={99}
                      onChange={(e) => handleQuantityChange(index, e.target.value)}
                      required
                    />
                  </div>

                  <div className="order-item-details">
                    {item.name} - ${calculateTotalPrice(item).toFixed(2)}
                  </div>

                  <Button
                    type="button"
                    onClick={() => handleRemoveItem(index)}
                    className="button-remove"
                    color="red"
                  >
                    X
                  </Button>
                </div>

                <div className="add-ons-section">
                  <select
                    id={`flavor-select-${index}`}
                    onChange={(e) => handleAddFlavor(index, JSON.parse(e.target.value) as MenuItemDto)}
                    onFocus={handleDropdownFocus}
                    className="w-full p-2 border border-[#cbb8a8] rounded-md mb-2 bg-[#fffaf3]"
                    aria-label={t("order.addFlavor")}
                  >
                    <option value="">{t("order.addFlavor")}</option>
                    {menuItems
                      .filter((menuItem) => menuItem.categoryType === CategoryType.FLAVORS)
                      .map((flavor) => (
                        <option key={flavor.id} value={JSON.stringify(flavor)}>
                          {flavor.name} - {currencyFormatter.format(flavor.price)}
                        </option>
                      ))}
                  </select>

                  {item.addOns.length > 0 ? (
                    <ul className="add-on-list">
                      {item.addOns.map((addOn, addOnIndex) => (
                        <li key={addOnIndex} className="add-on-item">
                          <span>
                            {addOn.name} - ${addOn.price} x {addOn.quantity}
                          </span>
                          <FormInput
                            type="number"
                            className="quantity-input"
                            min={1}
                            value={addOn.quantity}
                            onChange={(e) => {
                              const newOrderItems = [...orderItems];
                              newOrderItems[index].addOns[addOnIndex].quantity = parseInt(
                                e.target.value,
                                10
                              );
                              setOrderItems(newOrderItems);
                            }}
                          />
                        </li>
                      ))}
                    </ul>
                  ) : null}
                </div>

                <FormInput
                  type="text"
                  placeholder={t("order.notesPlaceholder")}
                  value={item.notes}
                  onChange={(e) => handleNotesChange(index, e.target.value)}
                  className="placeholder-gray-400"
                />
              </li>
            ))}
          </ul>

          <div className="font-bold text-xl text-[#4a3326]">
            {t("order.total")}: {currencyFormatter.format(calculateOrderTotal())}
          </div>

          <Button type="submit" color="green" disabled={orderItems.length === 0}>
            {t("order.placeOrder")}
          </Button>
        </form>
      </div>
    </div>
  );
}

export default OrderPage;
