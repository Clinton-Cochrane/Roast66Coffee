import React, { useCallback, useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import axios from "../../axiosConfig";
import { toast } from "react-toastify";
import FormInput from "../common/FormInput";
import Button from "../common/Button";
import Card from "../common/Card";
import type { MenuItemDto } from "../../types/api";
import { useI18n } from "../../i18n/LanguageContext";
import { FaRegStar, FaStar } from "react-icons/fa";

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
  const [updatingSpecialIds, setUpdatingSpecialIds] = useState<Set<number>>(() => new Set());
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

  const selectedSpecialCount = menuItems.filter((item) => item.isFeaturedOnHome).length;

  const handleHomepageSpecialChange = (item: MenuItemDto) => {
    const isSelected = !item.isFeaturedOnHome;

    setUpdatingSpecialIds((current) => new Set(current).add(item.id));
    setMenuItems((current) =>
      current.map((currentItem) =>
        currentItem.id === item.id
          ? { ...currentItem, isFeaturedOnHome: isSelected }
          : currentItem
      )
    );

    axios
      .put(`/admin/menu/${item.id}/homepage-special`, { isSelected })
      .then(() => {
        toast.success(isSelected ? t("adminMenu.specialSelected") : t("adminMenu.specialRemoved"));
      })
      .catch(() => {
        setMenuItems((current) =>
          current.map((currentItem) =>
            currentItem.id === item.id
              ? { ...currentItem, isFeaturedOnHome: item.isFeaturedOnHome }
              : currentItem
          )
        );
        toast.error(t("adminMenu.specialUpdateFailed"));
      })
      .finally(() => {
        setUpdatingSpecialIds((current) => {
          const next = new Set(current);
          next.delete(item.id);
          return next;
        });
      });
  };

  return (
    <div className="space-y-4">
      <h2 className="text-2xl font-bold mb-4">{t("adminMenu.title")}</h2>

      <Card>
        <select
          value={selectedMenuItemId}
          onChange={handleSelectChange}
          className="mx-auto mb-4 block w-full max-w-[800px] rounded border p-2"
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
            className="mx-auto max-w-[400px]"
            required
          />
          <FormInput
            type="number"
            name="price"
            placeholder={t("adminMenu.pricePlaceholder")}
            step="0.01"
            value={menuItemForm.price}
            onChange={handleFormChange}
            className="mx-auto max-w-[400px]"
            required
          />
          <FormInput
            type="text"
            name="description"
            placeholder={t("adminMenu.descriptionPlaceholder")}
            value={menuItemForm.description}
            onChange={handleFormChange}
            className="mx-auto max-w-[400px]"
            required
          />
          <select
            name="categoryType"
            value={menuItemForm.categoryType}
            onChange={handleFormChange}
            className="mx-auto block w-full max-w-[400px] rounded border p-2"
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
              <span className="flex items-center gap-2">
                <label
                  className={`inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-md border px-3 py-2 font-semibold ${
                    item.isFeaturedOnHome
                      ? "border-[#d39a22] bg-[#fff4bf] text-[#e0a400] shadow-[0_0_0_2px_rgba(224,164,0,0.16)]"
                      : "border-[#d8c5b3] bg-white text-[#7b6d62] hover:border-[#d39a22] hover:text-[#b47b00]"
                  }`}
                  title={t("adminMenu.specialToggleHelp")}
                >
                  <input
                    type="checkbox"
                    checked={item.isFeaturedOnHome}
                    disabled={
                      updatingSpecialIds.has(item.id) ||
                      (!item.isFeaturedOnHome && selectedSpecialCount >= 3)
                    }
                    onChange={() => handleHomepageSpecialChange(item)}
                    className="sr-only"
                  />
                  {item.isFeaturedOnHome ? (
                    <FaStar className="text-xl text-[#e0a400]" aria-hidden="true" />
                  ) : (
                    <FaRegStar className="text-xl" aria-hidden="true" />
                  )}
                  <span className="sr-only">{t("adminMenu.specialCheckbox", { name: item.name })}</span>
                </label>
                <Button onClick={() => handleDelete(item.id)} color="red">
                  {t("adminMenu.delete")}
                </Button>
              </span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}

export default ManageMenu;
