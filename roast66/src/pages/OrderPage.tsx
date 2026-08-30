import React, { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate, useLocation, Link } from "react-router-dom";
import axios from "axios";
import axiosInstance from "../axiosConfig";
import { toast } from "react-toastify";
import {
  FaCheck,
  FaChevronUp,
  FaMagnifyingGlass,
  FaPen,
  FaPlus,
  FaTrashCan,
  FaXmark,
} from "react-icons/fa6";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import CategoryType from "../constants/categories";
import { useI18n } from "../i18n/LanguageContext";
import { canOrderMenuItemDirectly } from "../utils/canOrderMenuItemDirectly";
import type { MenuItemDto, OrderDto } from "../types/api";
import PromotionPrice, { effectivePrice } from "../components/common/PromotionPrice";
import "../styles/OrderPage.css";

const MOBILE_ORDER_MEDIA_QUERY = "(max-width: 960px)";
const MAX_LINE_QUANTITY = 12;
const MAX_PRIMARY_LINES = 20;
const MAX_DRINK_UNITS = 50;
const MAX_FLAVORS_PER_DRINK = 12;
const MAX_NOTES_LENGTH = 500;
const MAX_ORDER_VALUE = 500;

type CartAddOn = MenuItemDto & { quantity: number };
type CartLine = MenuItemDto & {
  cartLineId: number;
  quantity: number;
  notes: string;
  addOns: CartAddOn[];
};
type DrinkCategoryFilter = "dailySpecials" | "coffee" | "drinks" | "all";

function OrderPage() {
  const { locale, t } = useI18n();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuItems, setMenuItems] = useState<MenuItemDto[]>([]);
  const [orderItems, setOrderItems] = useState<CartLine[]>([]);
  const [customerName, setCustomerName] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [drinkSearch, setDrinkSearch] = useState("");
  const [activeDrinkCategory, setActiveDrinkCategory] =
    useState<DrinkCategoryFilter>("dailySpecials");
  const [isMobileOrderLayout, setIsMobileOrderLayout] = useState(
    () =>
      typeof window !== "undefined" &&
      typeof window.matchMedia === "function" &&
      window.matchMedia(MOBILE_ORDER_MEDIA_QUERY).matches
  );
  const [isMobileOrderPanelOpen, setIsMobileOrderPanelOpen] = useState(false);
  const [activeCartLineId, setActiveCartLineId] = useState<number | null>(null);

  const wakeInFlightRef = useRef(false);
  const submissionInFlightRef = useRef(false);
  const nextCartLineIdRef = useRef(1);
  const prefillAppliedForLocationKeyRef = useRef<string | null>(null);
  const mobileOrderPanelRef = useRef<HTMLElement>(null);
  const orderPanelContentRef = useRef<HTMLDivElement>(null);
  const mobileOrderCloseButtonRef = useRef<HTMLButtonElement>(null);
  const mobileOrderTriggerRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    void ensureMenuItemsLoaded();
  }, []);

  useEffect(() => {
    if (typeof window.matchMedia !== "function") return;

    const mediaQuery = window.matchMedia(MOBILE_ORDER_MEDIA_QUERY);
    const handleLayoutChange = (event: MediaQueryListEvent) => {
      setIsMobileOrderLayout(event.matches);
      if (!event.matches) {
        setIsMobileOrderPanelOpen(false);
      }
    };

    setIsMobileOrderLayout(mediaQuery.matches);
    mediaQuery.addEventListener("change", handleLayoutChange);
    return () => mediaQuery.removeEventListener("change", handleLayoutChange);
  }, []);

  useEffect(() => {
    if (!isMobileOrderLayout || !isMobileOrderPanelOpen) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    mobileOrderCloseButtonRef.current?.focus();

    const handleDialogKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        setIsMobileOrderPanelOpen(false);
        mobileOrderTriggerRef.current?.focus();
        return;
      }

      if (event.key !== "Tab" || !mobileOrderPanelRef.current) return;

      const focusableElements = Array.from(
        mobileOrderPanelRef.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
        )
      );
      const firstFocusable = focusableElements[0];
      const lastFocusable = focusableElements[focusableElements.length - 1];
      if (!firstFocusable || !lastFocusable) return;

      if (event.shiftKey && document.activeElement === firstFocusable) {
        event.preventDefault();
        lastFocusable.focus();
      } else if (!event.shiftKey && document.activeElement === lastFocusable) {
        event.preventDefault();
        firstFocusable.focus();
      }
    };

    document.addEventListener("keydown", handleDialogKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleDialogKeyDown);
    };
  }, [isMobileOrderLayout, isMobileOrderPanelOpen]);

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

  const openMobileOrderPanel = (trigger?: HTMLButtonElement) => {
    if (!isMobileOrderLayout) return;
    if (trigger) {
      mobileOrderTriggerRef.current = trigger;
    }
    setIsMobileOrderPanelOpen(true);
  };

  const closeMobileOrderPanel = () => {
    setIsMobileOrderPanelOpen(false);
    mobileOrderTriggerRef.current?.focus();
  };

  const editOrderItem = (cartLineId: number) => {
    setActiveCartLineId(cartLineId);
    requestAnimationFrame(() => {
      const scrollContainer = isMobileOrderLayout
        ? orderPanelContentRef.current
        : mobileOrderPanelRef.current;
      scrollContainer?.scrollTo({ top: 0 });
    });
  };

  const addItemToOrder = (item: MenuItemDto, trigger?: HTMLButtonElement) => {
    if (!canOrderMenuItemDirectly(item)) {
      toast.warning(t("order.flavorStandaloneWarning"));
      return;
    }

    if (orderItems.length >= MAX_PRIMARY_LINES) {
      toast.warning(t("order.primaryLineLimit"));
      return;
    }
    const currentUnits = orderItems.reduce((total, orderItem) => total + orderItem.quantity, 0);
    if (currentUnits >= MAX_DRINK_UNITS) {
      toast.warning(t("order.drinkUnitLimit"));
      return;
    }

    const cartLineId = nextCartLineIdRef.current;
    nextCartLineIdRef.current += 1;
    setOrderItems((prev) => [
      ...prev,
      { ...item, cartLineId, quantity: 1, notes: "", addOns: [] },
    ]);
    setActiveCartLineId(cartLineId);
    openMobileOrderPanel(trigger);
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
    const cartLineId = nextCartLineIdRef.current;
    nextCartLineIdRef.current += 1;
    setOrderItems((prev) => [
      ...prev,
      { ...item, cartLineId, quantity: 1, notes: "", addOns: [] },
    ]);
    setActiveCartLineId(cartLineId);
    if (isMobileOrderLayout) {
      setIsMobileOrderPanelOpen(true);
    }
    clearPrefillState();
  }, [isMobileOrderLayout, menuItems, location.key, location.state, navigate, t]);

  const currencyFormatter = new Intl.NumberFormat(locale, {
    style: "currency",
    currency: "USD",
  });
  const orderableMenuItems = menuItems.filter((item) => canOrderMenuItemDirectly(item));
  const categoryFilters: Array<{ id: DrinkCategoryFilter; label: string }> = [
    { id: "dailySpecials", label: t("order.categoryDailySpecials") },
    { id: "coffee", label: t("order.categoryCoffee") },
    { id: "drinks", label: t("order.categoryDrinks") },
    { id: "all", label: t("order.categoryAll") },
  ];
  const normalizedDrinkSearch = drinkSearch.trim().toLocaleLowerCase(locale);
  const visibleMenuItems = orderableMenuItems.filter((item) => {
    const matchesCategory =
      activeDrinkCategory === "all" ||
      (activeDrinkCategory === "dailySpecials" &&
        (item.categoryType === CategoryType.SPECIALS || item.isFeaturedOnHome)) ||
      (activeDrinkCategory === "coffee" && item.categoryType === CategoryType.COFFEE) ||
      (activeDrinkCategory === "drinks" && item.categoryType === CategoryType.DRINKS);

    if (!matchesCategory || !normalizedDrinkSearch) {
      return matchesCategory;
    }

    return `${item.name} ${item.description}`
      .toLocaleLowerCase(locale)
      .includes(normalizedDrinkSearch);
  });

  const calculateTotalPrice = (item: CartLine) => {
    const basePrice = effectivePrice(item) * item.quantity;
    const addOnsPrice = item.addOns.reduce(
      (total, addOn) => total + effectivePrice(addOn) * addOn.quantity,
      0
    );
    return basePrice + addOnsPrice;
  };

  const calculateOrderTotal = () => {
    return orderItems.reduce((total, item) => total + calculateTotalPrice(item), 0);
  };
  const orderItemCount = orderItems.reduce((total, item) => total + item.quantity, 0);
  const orderedMenuItemIds = new Set(orderItems.map((item) => item.id));
  const requestedActiveItemIndex = orderItems.findIndex(
    (item) => item.cartLineId === activeCartLineId
  );
  const activeOrderItemIndex =
    requestedActiveItemIndex >= 0 ? requestedActiveItemIndex : orderItems.length - 1;
  const activeOrderItem = orderItems[activeOrderItemIndex];

  const handleQuantityChange = (index: number, quantity: string) => {
    const parsedQuantity = Number.parseInt(quantity, 10);
    if (Number.isNaN(parsedQuantity)) return;
    if (parsedQuantity <= 0) {
      handleRemoveItem(index);
      return;
    }

    const otherUnits = orderItems.reduce(
      (total, item, itemIndex) => total + (itemIndex === index ? 0 : item.quantity),
      0
    );
    const maximumForLine = Math.min(MAX_LINE_QUANTITY, MAX_DRINK_UNITS - otherUnits);
    if (parsedQuantity > maximumForLine) {
      toast.warning(t("order.drinkUnitLimit"));
    }

    setOrderItems((previousItems) =>
      previousItems.map((item, itemIndex) =>
        itemIndex === index ? { ...item, quantity: Math.min(parsedQuantity, maximumForLine) } : item
      )
    );
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
    if (removedItem.cartLineId === activeCartLineId) {
      setActiveCartLineId(null);
    }
    toast.info(t("order.itemRemoved", { itemName: removedItem.name }));
  };

  const handleAddFlavor = (index: number, flavor: MenuItemDto) => {
    if (!flavor?.id) return;

    const newOrderItems = [...orderItems];
    const addOns = newOrderItems[index].addOns;

    if (addOns.length >= MAX_FLAVORS_PER_DRINK) {
      toast.warning(t("order.flavorLimit"));
    } else if (!addOns.some((addOn) => addOn.id === flavor.id)) {
      addOns.push({ ...flavor, quantity: 1 });
      setOrderItems(newOrderItems);
    } else {
      toast.warning(t("order.addOnDuplicateWarning"));
    }

    const el = document.getElementById(`flavor-select-${index}`) as HTMLSelectElement | null;
    if (el) el.value = "";
  };

  const handleAddOnQuantityChange = (
    itemIndex: number,
    addOnIndex: number,
    quantity: string
  ) => {
    const parsedQuantity = Number.parseInt(quantity, 10);
    if (Number.isNaN(parsedQuantity)) return;

    setOrderItems((previousItems) =>
      previousItems.map((item, currentItemIndex) => {
        if (currentItemIndex !== itemIndex) return item;

        const addOns =
          parsedQuantity <= 0
            ? item.addOns.filter((_, currentAddOnIndex) => currentAddOnIndex !== addOnIndex)
            : item.addOns.map((addOn, currentAddOnIndex) =>
                currentAddOnIndex === addOnIndex
                  ? { ...addOn, quantity: Math.min(parsedQuantity, MAX_LINE_QUANTITY) }
                  : addOn
              );

        return { ...item, addOns };
      })
    );
  };

  const handleOrderSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (submissionInFlightRef.current) {
      return;
    }
    if (orderItems.length === 0) {
      toast.error(t("order.orderRequiredError"));
      return;
    }
    if (calculateOrderTotal() > MAX_ORDER_VALUE) {
      toast.error(t("order.orderValueLimit"));
      return;
    }
    const orderData = {
      customerName: customerName.trim(),
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
    submissionInFlightRef.current = true;
    setIsSubmitting(true);

    const resetSubmissionState = () => {
      submissionInFlightRef.current = false;
      setIsSubmitting(false);
    };

    try {
      const response = await axiosInstance.post<OrderDto>("/admin/orders", orderData);
      const createdOrder = response.data;
      setOrderItems([]);
      setActiveCartLineId(null);
      setCustomerName("");
      navigate("/order/confirmation", { state: { order: createdOrder } });
    } catch (error: unknown) {
      if (axios.isAxiosError(error) && error.response?.status === 409) {
        const existingOrder = error.response?.data?.order as OrderDto | undefined;
        const existingOrderId = error.response?.data?.existingOrderId as number | undefined;
        setOrderItems([]);
        setActiveCartLineId(null);
        setCustomerName("");
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
        resetSubmissionState();
      }
    }
  };

  return (
    <div className="r66-order-page">
      <div className="r66-order-shell">
        <div className="r66-order-heading-row">
          <h1 className="r66-order-title">{t("order.placeYourOrder")}</h1>
          <Link to="/order-status" className="r66-order-status-link">
            {t("order.checkOrderStatus")} →
          </Link>
        </div>
        <p className="r66-order-subtitle">{t("order.pageSubtitle")}</p>

        <form onSubmit={handleOrderSubmit}>
          <div className="r66-order-customer-grid">
            <div className="min-w-0">
              <FormInput
                type="text"
                name="customerName"
                autoComplete="name"
                placeholder={t("order.namePlaceholder")}
                title={t("order.namePlaceholder")}
                aria-label={t("order.namePlaceholder")}
                value={customerName}
                onChange={(e) => setCustomerName(e.target.value)}
                maxLength={100}
                required
              />
            </div>
          </div>

          <div className="r66-order-workspace">
            <section className="r66-order-selector-panel" aria-labelledby="drink-selector-heading">
              <h2 id="drink-selector-heading" className="r66-order-section-title">
                {t("order.chooseDrink")}
              </h2>
              <p className="r66-order-selector-instructions">{t("order.instructions")}</p>
              <div className="r66-order-search-field">
                <FaMagnifyingGlass aria-hidden="true" />
                <input
                  type="search"
                  name="drinkSearch"
                  autoComplete="off"
                  spellCheck={false}
                  value={drinkSearch}
                  onChange={(event) => setDrinkSearch(event.target.value)}
                  onFocus={handleDropdownFocus}
                  placeholder={t("order.searchDrinksPlaceholder")}
                  aria-label={t("order.searchDrinksLabel")}
                />
              </div>
              <nav className="r66-order-category-nav" aria-label={t("order.categoryNavigation")}>
                {categoryFilters.map((category) => {
                  const isActive = activeDrinkCategory === category.id;
                  return (
                    <button
                      key={category.id}
                      type="button"
                      className="r66-order-category-chip"
                      aria-pressed={isActive}
                      aria-controls="order-drink-results"
                      onClick={() => setActiveDrinkCategory(category.id)}
                    >
                      {category.label}
                    </button>
                  );
                })}
              </nav>
              <div id="order-drink-results">
                {visibleMenuItems.length > 0 ? (
                  <ul className="r66-order-drink-list" aria-label={t("order.availableDrinks")}>
                    {visibleMenuItems.map((item) => {
                      const isInOrder = orderedMenuItemIds.has(item.id);
                      return (
                        <li key={item.id}>
                          <button
                            type="button"
                            className="r66-order-drink-row"
                            data-in-order={isInOrder ? "true" : undefined}
                            onClick={(event) => addItemToOrder(item, event.currentTarget)}
                            aria-label={t(
                              isInOrder ? "menu.orderAnotherItem" : "menu.orderItem",
                              { itemName: item.name }
                            )}
                          >
                            <span className="r66-order-drink-copy">
                              <span className="r66-order-drink-name">{item.name}</span>
                              <span className="r66-order-drink-description">{item.description}</span>
                            </span>
                            <span className="r66-order-drink-price">
                              {currencyFormatter.format(effectivePrice(item))}
                            </span>
                            <span className="r66-order-drink-add-icon" aria-hidden="true">
                              {isInOrder ? <FaCheck /> : <FaPlus />}
                            </span>
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                ) : (
                  <p className="r66-order-drinks-empty" role="status">
                    {menuItems.length === 0
                      ? t("order.drinksLoading")
                      : orderableMenuItems.length === 0
                        ? t("order.noDrinksAvailable")
                        : t("order.noMatchingDrinks")}
                  </p>
                )}
              </div>
            </section>

            {isMobileOrderLayout && isMobileOrderPanelOpen ? (
              <div
                className="r66-mobile-order-backdrop"
                aria-hidden="true"
                onClick={closeMobileOrderPanel}
              />
            ) : null}

            <aside
              id="mobile-order-panel"
              ref={mobileOrderPanelRef}
              className={`r66-order-right-column ${
                isMobileOrderLayout && isMobileOrderPanelOpen
                  ? "r66-mobile-order-sheet-open"
                  : ""
              }`}
              aria-label={!isMobileOrderLayout ? t("order.currentOrder") : undefined}
              aria-labelledby={
                isMobileOrderLayout && isMobileOrderPanelOpen
                  ? "mobile-order-sheet-heading"
                  : undefined
              }
              aria-hidden={
                isMobileOrderLayout && !isMobileOrderPanelOpen ? true : undefined
              }
              aria-modal={
                isMobileOrderLayout && isMobileOrderPanelOpen ? true : undefined
              }
              role={isMobileOrderLayout && isMobileOrderPanelOpen ? "dialog" : undefined}
            >
              <div ref={orderPanelContentRef} className="r66-order-panel">
                {isMobileOrderLayout ? (
                  <div className="r66-mobile-order-sheet-header">
                    <h2 id="mobile-order-sheet-heading">{t("order.orderDetails")}</h2>
                    <button
                      ref={mobileOrderCloseButtonRef}
                      type="button"
                      className="r66-mobile-order-sheet-close"
                      onClick={closeMobileOrderPanel}
                      aria-label={t("order.closeOrderDetails")}
                    >
                      <FaXmark aria-hidden="true" />
                    </button>
                  </div>
                ) : null}
                <section className="r66-order-customizer" aria-labelledby="order-customizer-heading">
                  <h2 id="order-customizer-heading" className="sr-only">
                    {t("order.customizeOrder")}
                  </h2>

                  {orderItems.length === 0 ? (
                    <div className="r66-order-empty-customizer">
                      <h3>{t("order.customizeOrder")}</h3>
                      <p>{t("order.customizerEmpty")}</p>
                    </div>
                  ) : activeOrderItem ? (
                    <ul className="r66-order-customizer-list">
                      <li
                        key={activeOrderItem.cartLineId}
                        className="r66-order-customizer-item"
                      >
                        <div className="r66-order-item-heading">
                            <div>
                              <h3>
                                {t("order.customizeItem", {
                                  itemName: activeOrderItem.name,
                                })}
                              </h3>
                              <div className="r66-order-item-price">
                                {currencyFormatter.format(calculateTotalPrice(activeOrderItem))}
                              </div>
                              {activeOrderItem.promotion ? (
                                <PromotionPrice
                                  item={activeOrderItem}
                                  className="r66-order-promotion-price"
                                />
                              ) : null}
                            </div>
                            <Button
                              type="button"
                              onClick={() => handleRemoveItem(activeOrderItemIndex)}
                              variant="link"
                              className="r66-order-remove-button"
                              color="red"
                            >
                              <FaTrashCan aria-hidden="true" />
                              <span>{t("order.removeItem")}</span>
                            </Button>
                        </div>

                        <div className="r66-order-control-group r66-order-quantity-group">
                            <FormInput
                              id={`order-quantity-${activeOrderItemIndex}`}
                              type="number"
                              label={t("order.quantity")}
                              aria-label={t("order.quantityLabel", {
                                itemName: activeOrderItem.name,
                              })}
                              aria-describedby="order-quantity-help"
                              className="r66-order-quantity-input"
                              value={activeOrderItem.quantity}
                              min={0}
                              max={MAX_LINE_QUANTITY}
                              step={1}
                              inputMode="numeric"
                              onChange={(e) =>
                                handleQuantityChange(activeOrderItemIndex, e.target.value)
                              }
                              required
                            />
                        </div>

                        <div className="r66-order-control-group">
                            <label
                              htmlFor={`flavor-select-${activeOrderItemIndex}`}
                              className="r66-order-control-label"
                            >
                              {t("order.addFlavor")}
                            </label>
                            <select
                              id={`flavor-select-${activeOrderItemIndex}`}
                              onChange={(e) =>
                                handleAddFlavor(
                                  activeOrderItemIndex,
                                  JSON.parse(e.target.value) as MenuItemDto
                                )
                              }
                              onFocus={handleDropdownFocus}
                              className="r66-order-select"
                            >
                              <option value="">{t("order.addFlavor")}</option>
                              {menuItems
                                .filter(
                                  (menuItem) => menuItem.categoryType === CategoryType.FLAVORS
                                )
                                .map((flavor) => (
                                  <option key={flavor.id} value={JSON.stringify(flavor)}>
                                    {flavor.name} - {flavor.promotion ? `${flavor.promotion} off, ` : ""}
                                    {currencyFormatter.format(effectivePrice(flavor))}
                                  </option>
                                ))}
                            </select>

                            {activeOrderItem.addOns.length > 0 ? (
                              <ul
                                className="r66-order-flavor-list"
                                aria-label={t("order.selectedFlavors")}
                              >
                                {activeOrderItem.addOns.map((addOn, addOnIndex) => (
                                  <li key={addOnIndex} className="r66-order-flavor-row">
                                    <div className="r66-order-flavor-copy">
                                      <span className="r66-order-flavor-name">{addOn.name}</span>
                                      <span className="r66-order-flavor-price">
                                        {currencyFormatter.format(effectivePrice(addOn))}
                                        {addOn.promotion
                                          ? ` (${addOn.promotion} off; was ${currencyFormatter.format(addOn.price)})`
                                          : ""}
                                      </span>
                                    </div>
                                    <FormInput
                                      type="number"
                                      aria-label={t("order.quantityLabel", { itemName: addOn.name })}
                                      aria-describedby="order-quantity-help"
                                      className="r66-order-addon-quantity-input"
                                      min={0}
                                      max={MAX_LINE_QUANTITY}
                                      step={1}
                                      inputMode="numeric"
                                      value={addOn.quantity}
                                      onChange={(e) =>
                                        handleAddOnQuantityChange(
                                          activeOrderItemIndex,
                                          addOnIndex,
                                          e.target.value
                                        )
                                      }
                                    />
                                  </li>
                                ))}
                              </ul>
                            ) : null}
                        </div>

                        <div className="r66-order-control-group">
                            <label
                              htmlFor={`order-notes-${activeOrderItemIndex}`}
                              className="r66-order-control-label"
                            >
                              {t("order.notesPlaceholder")}
                            </label>
                            <textarea
                              id={`order-notes-${activeOrderItemIndex}`}
                              name={`orderNotes-${activeOrderItemIndex}`}
                              autoComplete="off"
                              value={activeOrderItem.notes}
                              maxLength={MAX_NOTES_LENGTH}
                              onChange={(e) =>
                                handleNotesChange(activeOrderItemIndex, e.target.value)
                              }
                              className="r66-order-notes"
                              placeholder={t("order.notesPlaceholder")}
                              rows={2}
                            />
                        </div>
                      </li>
                    </ul>
                  ) : null}
                </section>

                <section className="r66-current-order" aria-labelledby="current-order-heading">
                  <h2 id="current-order-heading" className="r66-order-section-title">
                    {t("order.currentOrder")}
                  </h2>

                  {orderItems.length > 0 ? (
                    <ul className="r66-current-order-list">
                      {orderItems.map((item, index) => {
                        const isEditing = index === activeOrderItemIndex;
                        return (
                        <li
                          key={item.cartLineId}
                          data-testid="order-item"
                          className="r66-current-order-item"
                        >
                          <button
                            type="button"
                            className="r66-current-order-edit-button"
                            aria-label={t("order.editItem", { itemName: item.name })}
                            aria-pressed={isEditing}
                            onClick={() => editOrderItem(item.cartLineId)}
                          >
                            <div className="r66-current-order-copy">
                              <div className="r66-current-order-name-row">
                                <span className="r66-current-order-name">{item.name}</span>
                                <span className="r66-current-order-quantity">× {item.quantity}</span>
                              </div>
                              <p>
                                {item.addOns.length > 0
                                  ? item.addOns
                                      .map((addOn) => `${addOn.name} × ${addOn.quantity}`)
                                      .join(", ")
                                  : t("order.noFlavors")}
                              </p>
                              {item.notes ? <p>{item.notes}</p> : null}
                            </div>
                            <span className="r66-current-order-price">
                              {currencyFormatter.format(calculateTotalPrice(item))}
                            </span>
                            <FaPen className="r66-current-order-edit-icon" aria-hidden="true" />
                          </button>
                          <button
                            type="button"
                            className="r66-current-order-remove-button"
                            aria-label={t("order.removeNamedItem", { itemName: item.name })}
                            onClick={() => handleRemoveItem(index)}
                          >
                            <FaTrashCan aria-hidden="true" />
                          </button>
                        </li>
                        );
                      })}
                    </ul>
                  ) : (
                    <p className="r66-current-order-empty">{t("order.currentOrderEmpty")}</p>
                  )}

                  <div
                    className="r66-current-order-total-row"
                    aria-live="polite"
                    aria-atomic="true"
                  >
                    <span>{t("order.total")}</span>
                    <span>{currencyFormatter.format(calculateOrderTotal())}</span>
                  </div>

                  <Button
                    type="submit"
                    color="green"
                    className="r66-place-order-button"
                    disabled={orderItems.length === 0 || isSubmitting}
                  >
                    {isSubmitting ? (
                      <span className="r66-order-submit-progress" aria-live="polite">
                        <span className="r66-order-submit-spinner" aria-hidden="true" />
                        {t("order.placingOrder")}
                      </span>
                    ) : (
                      t("order.placeOrder")
                    )}
                  </Button>
                </section>
              </div>
            </aside>
          </div>

          {isMobileOrderLayout ? (
            <div className="r66-mobile-order-bar">
              <button
                type="button"
                onClick={(event) => openMobileOrderPanel(event.currentTarget)}
                aria-controls="mobile-order-panel"
                aria-expanded={isMobileOrderPanelOpen}
              >
                <span className="r66-mobile-order-bar-copy">
                  <span>{t("order.currentOrder")}</span>
                  <span>
                    {t(orderItemCount === 1 ? "order.itemCountOne" : "order.itemCountMany", {
                      count: orderItemCount,
                    })}
                  </span>
                </span>
                <span className="r66-mobile-order-bar-total">
                  {currencyFormatter.format(calculateOrderTotal())}
                </span>
                <span className="r66-mobile-order-bar-action">
                  {t("order.viewOrder")}
                  <FaChevronUp aria-hidden="true" />
                </span>
              </button>
            </div>
          ) : null}

          <p id="order-quantity-help" className="sr-only">
            {t("order.quantityHelp")}
          </p>
        </form>
      </div>
    </div>
  );
}

export default OrderPage;
