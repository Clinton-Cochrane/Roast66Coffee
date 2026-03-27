import React, { useEffect, useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import axios from "../axiosConfig";
import { toast } from "react-toastify";
import "../styles/OrderPage.css";
import FormInput from "../components/common/FormInput";
import Button from "../components/common/Button";
import CategoryType from "../constants/categories";
import { useI18n } from "../i18n/LanguageContext";

const ENABLE_STRIPE_CHECKOUT =
  process.env.REACT_APP_ENABLE_STRIPE_CHECKOUT === "true";

function OrderPage() {
  const { locale, t } = useI18n();
  const navigate = useNavigate();
  const [menuItems, setMenuItems] = useState([]);
  const [orderItems, setOrderItems] = useState([]);
  const [customerName, setCustomerName] = useState("");
  const [customerPhone, setCustomerPhone] = useState("");
  const [customerEmail, setCustomerEmail] = useState("");
  const [emailOptIn, setEmailOptIn] = useState(false);

  useEffect(() => {
    fetchMenuItems();
  }, []);

  const fetchMenuItems = () => {
    axios
      .get("/menu")
      .then((response) => setMenuItems(response.data))
      .catch((error) => console.error(error));
  };

  /** Refetch menu only when empty (e.g. backend was asleep on initial load). Avoids redundant API calls when tabbing. */
  const handleDropdownFocus = () => {
    if (menuItems.length === 0) {
      fetchMenuItems();
    }
  };

  const canOrderDirectly = (item) => {
    return (
      item.categoryType === CategoryType.COFFEE ||
      item.categoryType === CategoryType.DRINKS ||
      item.categoryType === CategoryType.SPECIALS
    );
  };

  const handleDropDownChange = (e) => {
    const value = e.target.value;
    if (!value) {
      return;
    }
    const item = JSON.parse(value);
    addItemToOrder(item);
  };

  const addItemToOrder = (item) => {
    if (canOrderDirectly(item)) {
      setOrderItems([
        ...orderItems,
        { ...item, quantity: 1, notes: "", addOns: [] },
      ]);
    } else {
      toast.warning(t("order.flavorStandaloneWarning"));
    }
  };

  const currencyFormatter = new Intl.NumberFormat(locale, {
    style: "currency",
    currency: "USD",
  });

  const calculateOrderTotal = () => {
    return orderItems.reduce(
      (total, item) => total + calculateTotalPrice(item),
      0
    );
  };

  const calculateTotalPrice = (item) => {
    const basePrice = item.price * item.quantity;
    const addOnsPrice = item.addOns.reduce(
      (total, addOn) => total + addOn.price * addOn.quantity,
      0
    );

    return basePrice + addOnsPrice;
  };

  const handleQuantityChange = (index, quantity) => {
    const newQuantity = Math.min(parseInt(quantity, 10) || 1, 99); // Ensure value is between 1 and 99
    const newOrderItems = [...orderItems];
    newOrderItems[index].quantity = newQuantity;
    setOrderItems(newOrderItems);
  };

  const handleNotesChange = (index, notes) => {
    const newOrderItems = [...orderItems];
    newOrderItems[index].notes = notes;
    setOrderItems(newOrderItems);
  };

  const handleRemoveItem = (index) => {
    const removedItem = orderItems[index];
    const newOrderItems = orderItems.filter((_, i) => i !== index);
    setOrderItems(newOrderItems);
    toast.info(t("order.itemRemoved", { itemName: removedItem.name }));
  };

  const handleAddFlavor = (index, flavor) => {
    if (!flavor || !flavor.id) return;

    const newOrderItems = [...orderItems];
    const addOns = newOrderItems[index].addOns;

    if (!addOns.some((addOn) => addOn.id === flavor.id)) {
      addOns.push({ ...flavor, quantity: 1 });
      setOrderItems(newOrderItems);
    } else {
      toast.warning(t("order.addOnDuplicateWarning"));
    }

    // Reset the dropdown
    document.getElementById(`flavor-select-${index}`).value = "";
  };

  const handleOrderSubmit = (e) => {
    e.preventDefault();
    if (orderItems.length === 0) {
      toast.error(t("order.orderRequiredError"));
      return;
    }
    const orderData = {
      customerName,
      customerPhone,
      customerEmail: customerEmail.trim() || null,
      customerNotificationOptIn: emailOptIn && customerEmail.trim().length > 0,
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
      axios
        .post("/payments/checkout-session", orderData, {
          headers: { "X-Idempotency-Key": idempotencyKey },
        })
        .then((response) => {
          const checkoutUrl = response?.data?.checkoutUrl;
          if (!checkoutUrl) {
            throw new Error(t("order.checkoutMissingUrl"));
          }
          window.location.assign(checkoutUrl);
        })
        .catch((error) => {
          console.error("Checkout session creation failed:", error);
          toast.error(t("order.checkoutFailed"));
        });
      return;
    }

    axios
      .post("/admin/orders", orderData)
      .then((response) => {
        const createdOrder = response.data;
        setOrderItems([]);
        setCustomerName("");
        setCustomerPhone("");
        setCustomerEmail("");
        setEmailOptIn(false);
        navigate("/order/confirmation", { state: { order: createdOrder } });
      })
      .catch((error) => {
        if (error?.response?.status === 409) {
          const existingOrder = error.response?.data?.order;
          const existingOrderId = error.response?.data?.existingOrderId;
          setOrderItems([]);
          setCustomerName("");
          setCustomerPhone("");
          setCustomerEmail("");
          setEmailOptIn(false);
          navigate("/order/duplicate", {
            state: { order: existingOrder, existingOrderId },
          });
        } else {
          console.error(
            "Order submission failed:",
            error?.response?.status,
            error?.response?.data,
            error
          );
          toast.error(
            t("order.submitFailed")
          );
        }
      });
  };

  return (
    <div className="p-6 flex flex-col items-center">
      <div className="w-full max-w-5xl">
      <div className="flex flex-wrap items-center justify-between gap-4 mb-6">
        <h1 className="text-3xl font-bold">{t("order.placeYourOrder")}</h1>
        <Link
          to="/order-status"
          className="text-accent hover:underline font-medium"
        >
          {t("order.checkOrderStatus")} →
        </Link>
      </div>

      <div className="customer-info-container">
        <FormInput
          type="text"
          name="customerName"
          label={t("order.namePlaceholder")}
          placeholder={t("order.namePlaceholder")}
          value={customerName}
          onChange={(e) => setCustomerName(e.target.value)}
          required
        />
        <FormInput
          type="text"
          name="customerPhone"
          label={t("order.phonePlaceholder")}
          placeholder={t("order.phonePlaceholder")}
          value={customerPhone}
          onChange={(e) => setCustomerPhone(e.target.value)}
          required
        />
        <FormInput
          type="email"
          name="customerEmail"
          label={t("order.emailPlaceholder")}
          placeholder={t("order.emailPlaceholder")}
          value={customerEmail}
          onChange={(e) => setCustomerEmail(e.target.value)}
        />
      </div>
      <label className="block text-sm text-gray-600 mb-4">
        <input
          type="checkbox"
          className="mr-2"
          checked={emailOptIn}
          onChange={(e) => setEmailOptIn(e.target.checked)}
        />
        {t("order.emailOptIn")}
      </label>

      <p className="text-gray-600 text-sm mb-4 text-center">
        {t("order.instructions")}
      </p>

      <div className="flex items-center space-x-4 mb-4">
        <select
          id="menu-select"
          onChange={handleDropDownChange}
          onFocus={handleDropdownFocus}
          className="w-full p-2 border rounded mb-4"
          aria-label={t("order.selectMenuItem")}
        >
          <option value="">{t("order.selectMenuItem")}</option>
          {menuItems
            .filter((item) => canOrderDirectly(item))
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
                    min="1"
                    max="99"
                    onChange={(e) =>
                      handleQuantityChange(index, e.target.value)
                    }
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
                  onChange={(e) =>
                    handleAddFlavor(index, JSON.parse(e.target.value))
                  }
                  onFocus={handleDropdownFocus}
                  className="w-full p-2 border rounded mb-2"
                  aria-label={t("order.addFlavor")}
                >
                  <option value="">{t("order.addFlavor")}</option>
                  {menuItems
                    .filter(
                      (menuItem) =>
                        menuItem.categoryType === CategoryType.FLAVORS
                    )
                    .map((flavor) => (
                      <option key={flavor.id} value={JSON.stringify(flavor)}>
                        {flavor.name} - {currencyFormatter.format(flavor.price)}
                      </option>
                    ))}
                </select>

                {item.addOns.length > 0 && (
                  <ul className="add-on-list">
                    {item.addOns.map((addOn, addOnIndex) => (
                      <li key={addOnIndex} className="add-on-item">
                        <span>
                          {addOn.name} - ${addOn.price} x {addOn.quantity}
                        </span>
                        <FormInput
                          type="number"
                          className="quantity-input"
                          min="1"
                          value={addOn.quantity}
                          onChange={(e) => {
                            const newOrderItems = [...orderItems];
                            newOrderItems[index].addOns[addOnIndex].quantity =
                              parseInt(e.target.value, 10);
                            setOrderItems(newOrderItems);
                          }}
                        />
                      </li>
                    ))}
                  </ul>
                )}
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

        <div className="font-bold text-lg">
          {t("order.total")}: {currencyFormatter.format(calculateOrderTotal())}
        </div>

        <Button
          type="submit"
          color="green"
          disabled={orderItems.length === 0}
        >
          {t("order.placeOrder")}
        </Button>
      </form>
      </div>
    </div>
  );
}

export default OrderPage;
