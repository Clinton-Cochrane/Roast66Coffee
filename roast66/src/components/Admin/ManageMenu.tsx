import React, { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import axios from "../../axiosConfig";
import { toast } from "react-toastify";
import "../../styles/ManageMenu.css";
import FormInput from "../common/FormInput";
import Button from "../common/Button";
import Card from "../common/Card";
import type { MenuItemDto } from "../../types/api";
import { useI18n } from "../../i18n/LanguageContext";

type CategoryOption = { id: number; name: string };

type MenuItemFormState = {
  name: string;
  price: string | number;
  description: string;
  categoryType: number;
};

function ManageMenu() {
  const { t } = useI18n();
  const [menuItems, setMenuItems] = useState<MenuItemDto[]>([]);
  const [selectedMenuItemId, setSelectedMenuItemId] = useState("");
  const [categories, setCategories] = useState<CategoryOption[]>([]);
  const [menuItemForm, setMenuItemForm] = useState<MenuItemFormState>({
    name: "",
    price: "",
    description: "",
    categoryType: 0,
  });

  const fetchMenuItems = useCallback(() => {
    axios
      .get<MenuItemDto[]>("/admin/menu")
      .then((response) => setMenuItems(response.data))
      .catch(() => toast.error(t("adminMenu.fetchMenuError")));
  }, [t]);

  const fetchCategories = useCallback(() => {
    axios
      .get<CategoryOption[]>("/admin/categories")
      .then((response) => setCategories(response.data))
      .catch(() => toast.error(t("adminMenu.fetchCategoriesError")));
  }, [t]);

  useEffect(() => {
    fetchMenuItems();
    fetchCategories();
  }, [fetchMenuItems, fetchCategories]);

  const handleSelectChange = (e: ChangeEvent<HTMLSelectElement>) => {
    const selectedId = e.target.value;
    setSelectedMenuItemId(selectedId);

    if (selectedId === "new") {
      setMenuItemForm({
        name: "",
        price: 0,
        description: "",
        categoryType: 0,
      });
    } else {
      const selectedItem = menuItems.find((item) => item.id === parseInt(selectedId, 10));
      if (selectedItem) {
        setMenuItemForm({
          name: selectedItem.name,
          price: selectedItem.price.toString(),
          description: selectedItem.description,
          categoryType: selectedItem.categoryType,
        });
      }
    }
  };

  const handleFormChange = (e: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setMenuItemForm((prev) => ({
      ...prev,
      [name]:
        name === "price"
          ? parseFloat(value) || 0
          : name === "categoryType"
            ? parseInt(value, 10) || 0
            : value,
    }));
  };

  const handleFormSubmit = (e: FormEvent) => {
    e.preventDefault();

    const id = selectedMenuItemId === "new" ? 0 : parseInt(selectedMenuItemId, 10);

    const formData = {
      id,
      name: menuItemForm.name,
      price: parseFloat(String(menuItemForm.price)) || 0,
      description: menuItemForm.description,
      categoryType: parseInt(String(menuItemForm.categoryType), 10) || 0,
    };

    if (selectedMenuItemId === "new") {
      axios
        .post("/admin/menu", formData)
        .then(() => {
          toast.success(t("adminMenu.added"));
          fetchMenuItems();
          setMenuItemForm({ name: "", price: "", description: "", categoryType: 0 });
          setSelectedMenuItemId("");
        })
        .catch(() => toast.error(t("adminMenu.failedAdd")));
    } else {
      axios
        .put(`/admin/menu/${selectedMenuItemId}`, formData)
        .then(() => {
          toast.success(t("adminMenu.updated"));
          fetchMenuItems();
          setMenuItemForm({ name: "", price: "", description: "", categoryType: 0 });
          setSelectedMenuItemId("");
        })
        .catch(() => toast.error(t("adminMenu.failedUpdate")));
    }
  };

  const handleDelete = (id: number) => {
    axios
      .delete(`/admin/menu/${id}`)
      .then(() => {
        toast.success(t("adminMenu.deleted"));
        fetchMenuItems();
      })
      .catch(() => toast.error(t("adminMenu.failedDelete")));
  };

  return (
    <div className="manage-menu space-y-4">
      <h2 className="text-2xl font-bold mb-4">{t("adminMenu.title")}</h2>

      <Card>
        <select
          value={selectedMenuItemId}
          onChange={handleSelectChange}
          className="w-full p-2 border rounded mb-4 top-select"
        >
          <option value="">{t("adminMenu.selectPlaceholder")}</option>
          <option value="new">{t("adminMenu.addNewOption")}</option>
          {menuItems.map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
            </option>
          ))}
        </select>

        <form onSubmit={handleFormSubmit} className="space-y-4">
          <FormInput
            type="text"
            name="name"
            placeholder={t("adminMenu.namePlaceholder")}
            value={menuItemForm.name}
            onChange={handleFormChange}
            required
          />
          <FormInput
            type="number"
            name="price"
            placeholder={t("adminMenu.pricePlaceholder")}
            step="0.01"
            value={menuItemForm.price}
            onChange={handleFormChange}
            required
          />
          <FormInput
            type="text"
            name="description"
            placeholder={t("adminMenu.descriptionPlaceholder")}
            value={menuItemForm.description}
            onChange={handleFormChange}
            required
          />
          <select
            name="categoryType"
            value={menuItemForm.categoryType}
            onChange={handleFormChange}
            className="w-full p-2 border rounded"
            required
          >
            <option value="">{t("adminMenu.selectCategory")}</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>

          <Button type="submit" color="green">
            {selectedMenuItemId === "new" ? t("adminMenu.submitAdd") : t("adminMenu.submitUpdate")}
          </Button>
        </form>
      </Card>

      <Card title={t("adminMenu.listTitle")}>
        <ul className="space-y-2">
          {menuItems.map((item) => (
            <li key={item.id} className="flex justify-between items-center border-b pb-2">
              <span className="flex-1">
                {item.name} - ${item.price} - {item.description}
              </span>
              <Button onClick={() => handleDelete(item.id)} color="red">
                {t("adminMenu.delete")}
              </Button>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}

export default ManageMenu;
