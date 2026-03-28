import React, { useState, type ChangeEvent, type FormEvent } from "react";
import axios from "../../axiosConfig";
import FormInput from "../common/FormInput";
import Button from "../common/Button";
import Card from "../common/Card";
import { useI18n } from "../../i18n/LanguageContext";

function Order() {
  const { t } = useI18n();
  const [order, setOrder] = useState({
    customerName: "",
    menuItemId: "",
    quantity: 1,
  });
  const [message, setMessage] = useState("");

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    setOrder({ ...order, [e.target.name]: e.target.value });
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    axios
      .post("/orders", order)
      .then(() => setMessage(t("order.legacySuccessMessage")))
      .catch(() => setMessage(t("order.legacyErrorMessage")));
  };

  const success = message === t("order.legacySuccessMessage");

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <Card title={t("order.legacyCardTitle")}>
        <form onSubmit={handleSubmit} className="space-y-4">
          <FormInput
            type="text"
            name="customerName"
            placeholder={t("order.namePlaceholder")}
            value={order.customerName}
            onChange={handleChange}
            required
          />
          <FormInput
            type="number"
            name="menuItemId"
            placeholder={t("order.legacyMenuItemIdPlaceholder")}
            value={order.menuItemId}
            onChange={handleChange}
            required
          />
          <FormInput
            type="number"
            name="quantity"
            placeholder={t("order.legacyQuantityPlaceholder")}
            value={order.quantity}
            onChange={handleChange}
            min={1}
            required
          />
          <Button type="submit" color="green">
            {t("order.placeOrder")}
          </Button>
        </form>
        {message ? (
          <p className={`mt-4 text-center ${success ? "text-green-500" : "text-red-500"}`}>
            {message}
          </p>
        ) : null}
      </Card>
    </div>
  );
}

export default Order;
