import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type FormEvent,
} from "react";
import axios from "../../axiosConfig";
import { toast } from "react-toastify";
import { FaPen, FaPlus, FaTrashCan, FaXmark } from "react-icons/fa6";
import FormInput from "../common/FormInput";
import Button from "../common/Button";
import type { MenuItemDto } from "../../types/api";
import { useI18n } from "../../i18n/LanguageContext";
import "../../styles/Admin.css";

const MOBILE_MENU_MEDIA_QUERY = "(max-width: 960px)";

type CategoryOption = { id: number; name: string };

type MenuItemFormState = {
  name: string;
  price: string;
  description: string;
  categoryType: string;
};

const emptyMenuItemForm = (): MenuItemFormState => ({
  name: "",
  price: "",
  description: "",
  categoryType: "",
});

function ManageMenu() {
  const { locale, t } = useI18n();
  const [menuItems, setMenuItems] = useState<MenuItemDto[]>([]);
  const [selectedMenuItemId, setSelectedMenuItemId] = useState<number | null>(null);
  const [categories, setCategories] = useState<CategoryOption[]>([]);
  const [updatingSpecialIds, setUpdatingSpecialIds] = useState<Set<number>>(() => new Set());
  const [promotionInputs, setPromotionInputs] = useState<Record<number, string>>({});
  const [menuItemForm, setMenuItemForm] = useState<MenuItemFormState>(emptyMenuItemForm);
  const [isMobileLayout, setIsMobileLayout] = useState(
    () =>
      typeof window !== "undefined" &&
      typeof window.matchMedia === "function" &&
      window.matchMedia(MOBILE_MENU_MEDIA_QUERY).matches
  );
  const [isMobileEditorOpen, setIsMobileEditorOpen] = useState(false);

  const editorRef = useRef<HTMLElement>(null);
  const mobileCloseButtonRef = useRef<HTMLButtonElement>(null);
  const editorTriggerRef = useRef<HTMLButtonElement | null>(null);

  const selectedMenuItem = useMemo(
    () => menuItems.find((item) => item.id === selectedMenuItemId) ?? null,
    [menuItems, selectedMenuItemId]
  );
  const selectedSpecialCount = menuItems.filter((item) => item.isFeaturedOnHome).length;
  const currencyFormatter = useMemo(
    () =>
      new Intl.NumberFormat(locale, {
        style: "currency",
        currency: "USD",
      }),
    [locale]
  );

  const fetchMenuItems = useCallback(() => {
    axios
      .get<MenuItemDto[]>("/admin/menu")
      .then((response) => {
        const items = Array.isArray(response.data) ? response.data : [];
        setMenuItems(items);
        setPromotionInputs(
          Object.fromEntries(items.map((item) => [item.id, item.promotion ?? ""]))
        );
      })
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

  useEffect(() => {
    if (typeof window.matchMedia !== "function") return;

    const mediaQuery = window.matchMedia(MOBILE_MENU_MEDIA_QUERY);
    const handleLayoutChange = (event: MediaQueryListEvent) => {
      setIsMobileLayout(event.matches);
      if (!event.matches) setIsMobileEditorOpen(false);
    };

    setIsMobileLayout(mediaQuery.matches);
    mediaQuery.addEventListener("change", handleLayoutChange);
    return () => mediaQuery.removeEventListener("change", handleLayoutChange);
  }, []);

  useEffect(() => {
    if (!isMobileLayout || !isMobileEditorOpen) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    mobileCloseButtonRef.current?.focus();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        setIsMobileEditorOpen(false);
        editorTriggerRef.current?.focus();
        return;
      }

      if (event.key !== "Tab" || !editorRef.current) return;
      const focusableElements = Array.from(
        editorRef.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]), input:not([disabled]), select:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
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

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isMobileEditorOpen, isMobileLayout]);

  const focusEditor = () => {
    if (!isMobileLayout) {
      requestAnimationFrame(() =>
        editorRef.current?.querySelector<HTMLInputElement>('input[name="name"]')?.focus()
      );
    }
  };

  const openMobileEditor = (trigger: HTMLButtonElement) => {
    editorTriggerRef.current = trigger;
    if (isMobileLayout) setIsMobileEditorOpen(true);
  };

  const startCreating = (trigger?: HTMLButtonElement) => {
    setSelectedMenuItemId(null);
    setMenuItemForm(emptyMenuItemForm());
    if (trigger) openMobileEditor(trigger);
    focusEditor();
  };

  const startEditing = (item: MenuItemDto, trigger: HTMLButtonElement) => {
    setSelectedMenuItemId(item.id);
    setMenuItemForm({
      name: item.name,
      price: item.price.toString(),
      description: item.description,
      categoryType: item.categoryType.toString(),
    });
    openMobileEditor(trigger);
    focusEditor();
  };

  const closeMobileEditor = () => {
    setIsMobileEditorOpen(false);
    editorTriggerRef.current?.focus();
  };

  const updateMenuSpecial = (item: MenuItemDto) => {
    const isSelected = item.categoryType !== 1;
    axios
      .put(`/admin/menu/${item.id}/menu-special`, { isSelected })
      .then(() =>
        setMenuItems((items) =>
          items.map((current) =>
            current.id === item.id
              ? { ...current, categoryType: isSelected ? 1 : 3 }
              : current
          )
        )
      )
      .catch(() => toast.error(t("adminMenu.menuSpecialUpdateFailed")));
  };

  const savePromotion = (item: MenuItemDto) => {
    const promotion = promotionInputs[item.id] ?? "";
    if (promotion === (item.promotion ?? "")) return;

    axios
      .put(`/admin/menu/${item.id}/promotion`, { promotion })
      .then(fetchMenuItems)
      .catch((error: unknown) => {
        setPromotionInputs((values) => ({ ...values, [item.id]: item.promotion ?? "" }));
        const message = (error as { response?: { data?: { message?: string } } })?.response
          ?.data?.message;
        toast.error(message || t("adminMenu.promotionUpdateFailed"));
      });
  };

  const handleFormChange = (event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = event.target;
    setMenuItemForm((previous) => ({ ...previous, [name]: value }));
  };

  const finishEditing = () => {
    setSelectedMenuItemId(null);
    setMenuItemForm(emptyMenuItemForm());
    if (isMobileLayout) closeMobileEditor();
  };

  const handleFormSubmit = (event: FormEvent) => {
    event.preventDefault();
    const formData = {
      id: selectedMenuItemId ?? 0,
      name: menuItemForm.name,
      price: Number.parseFloat(menuItemForm.price) || 0,
      description: menuItemForm.description,
      categoryType: Number.parseInt(menuItemForm.categoryType, 10),
    };

    const request =
      selectedMenuItemId === null
        ? axios.post("/admin/menu", formData)
        : axios.put(`/admin/menu/${selectedMenuItemId}`, formData);

    request
      .then(() => {
        toast.success(t(selectedMenuItemId === null ? "adminMenu.added" : "adminMenu.updated"));
        fetchMenuItems();
        finishEditing();
      })
      .catch(() =>
        toast.error(
          t(selectedMenuItemId === null ? "adminMenu.failedAdd" : "adminMenu.failedUpdate")
        )
      );
  };

  const handleDelete = () => {
    if (!selectedMenuItem) return;
    if (!window.confirm(t("adminMenu.deleteConfirm", { name: selectedMenuItem.name }))) return;

    axios
      .delete(`/admin/menu/${selectedMenuItem.id}`)
      .then(() => {
        toast.success(t("adminMenu.deleted"));
        fetchMenuItems();
        finishEditing();
      })
      .catch(() => toast.error(t("adminMenu.failedDelete")));
  };

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

  const showEditor = !isMobileLayout || isMobileEditorOpen;

  return (
    <div className="r66-admin-menu">
      <div className="r66-admin-menu-heading">
        <h2>{t("adminMenu.title")}</h2>
        <p>{t("adminMenu.description")}</p>
      </div>

      <div className="r66-admin-menu-workspace">
        <section className="r66-admin-menu-list-panel" aria-labelledby="admin-menu-list-title">
          <div className="r66-admin-menu-list-heading">
            <div>
              <h3 id="admin-menu-list-title">{t("adminMenu.listTitle")}</h3>
              <p>{t("adminMenu.listDescription", { count: menuItems.length })}</p>
            </div>
            <button
              type="button"
              className="r66-admin-menu-create-button"
              onClick={(event) => startCreating(event.currentTarget)}
            >
              <FaPlus aria-hidden="true" />
              <span>{t("adminMenu.createNew")}</span>
            </button>
          </div>

          {menuItems.length === 0 ? (
            <p className="r66-admin-menu-empty">{t("adminMenu.emptyState")}</p>
          ) : (
            <ul className="r66-admin-menu-list">
              {menuItems.map((item) => {
                const category = categories.find((option) => option.id === item.categoryType);
                const isSelected = selectedMenuItemId === item.id;
                return (
                  <li
                    key={item.id}
                    className="r66-admin-menu-row"
                    data-selected={isSelected || undefined}
                  >
                    <div className="r66-admin-menu-row-main">
                      <div className="r66-admin-menu-row-copy">
                        <strong>{item.name}</strong>
                        <span>{item.description}</span>
                        {category ? <small>{category.name}</small> : null}
                      </div>
                      <span className="r66-admin-menu-price">
                        {currencyFormatter.format(item.price)}
                      </span>
                      <button
                        type="button"
                        className="r66-admin-menu-edit-button"
                        aria-label={t("adminMenu.editItemAria", { name: item.name })}
                        aria-pressed={isSelected}
                        onClick={(event) => startEditing(item, event.currentTarget)}
                      >
                        <FaPen aria-hidden="true" />
                      </button>
                    </div>

                    <div className="r66-admin-menu-row-controls">
                      <button
                        type="button"
                        aria-label={t("adminMenu.dailySpecialAria", { name: item.name })}
                        aria-pressed={item.isFeaturedOnHome}
                        disabled={
                          updatingSpecialIds.has(item.id) ||
                          (!item.isFeaturedOnHome && selectedSpecialCount >= 3)
                        }
                        onClick={() => handleHomepageSpecialChange(item)}
                        className="r66-admin-menu-compact-toggle"
                      >
                        DS
                      </button>
                      <button
                        type="button"
                        aria-label={t("adminMenu.menuSpecialAria", { name: item.name })}
                        aria-pressed={item.categoryType === 1}
                        onClick={() => updateMenuSpecial(item)}
                        className="r66-admin-menu-compact-toggle"
                      >
                        MS
                      </button>
                      <label
                        className="r66-admin-menu-promotion-control"
                        htmlFor={`promotion-${item.id}`}
                      >
                        <span>{t("adminMenu.promotionLabel")}</span>
                        <input
                          id={`promotion-${item.id}`}
                          value={promotionInputs[item.id] ?? ""}
                          placeholder={t("adminMenu.promotionPlaceholder")}
                          onChange={(event) =>
                            setPromotionInputs((values) => ({
                              ...values,
                              [item.id]: event.target.value,
                            }))
                          }
                          onBlur={() => savePromotion(item)}
                          onKeyDown={(event) => {
                            if (event.key === "Enter") event.currentTarget.blur();
                          }}
                        />
                      </label>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </section>

        {isMobileLayout && isMobileEditorOpen ? (
          <button
            type="button"
            className="r66-admin-menu-backdrop"
            aria-label={t("adminMenu.closeEditor")}
            onClick={closeMobileEditor}
          />
        ) : null}

        {showEditor ? (
          <aside
            ref={editorRef}
            className={`r66-admin-menu-editor-column ${
              isMobileEditorOpen ? "r66-admin-menu-editor-open" : ""
            }`}
            {...(isMobileLayout
              ? {
                  role: "dialog",
                  "aria-modal": true,
                  "aria-labelledby": "admin-menu-editor-title",
                }
              : {})}
          >
            <div className="r66-admin-menu-editor-panel">
              {isMobileLayout ? (
                <div className="r66-admin-menu-sheet-header">
                  <h3>{selectedMenuItem ? t("adminMenu.editItem") : t("adminMenu.createNew")}</h3>
                  <button
                    ref={mobileCloseButtonRef}
                    type="button"
                    aria-label={t("adminMenu.closeEditor")}
                    onClick={closeMobileEditor}
                  >
                    <FaXmark aria-hidden="true" />
                  </button>
                </div>
              ) : null}

              <div className="r66-admin-menu-editor-content">
                <div className="r66-admin-menu-editor-heading">
                  <p className="r66-admin-menu-eyebrow">
                    {selectedMenuItem
                      ? t("adminMenu.editingEyebrow")
                      : t("adminMenu.creatingEyebrow")}
                  </p>
                  <h3 id="admin-menu-editor-title">
                    {selectedMenuItem?.name ?? t("adminMenu.createNew")}
                  </h3>
                  <p>
                    {selectedMenuItem
                      ? t("adminMenu.editDescription")
                      : t("adminMenu.createDescription")}
                  </p>
                </div>

                <form onSubmit={handleFormSubmit} className="r66-admin-menu-form">
                  <FormInput
                    type="text"
                    name="name"
                    label={t("adminMenu.nameLabel")}
                    placeholder={t("adminMenu.namePlaceholder")}
                    value={menuItemForm.name}
                    onChange={handleFormChange}
                    required
                  />
                  <FormInput
                    type="number"
                    name="price"
                    label={t("adminMenu.priceLabel")}
                    placeholder={t("adminMenu.pricePlaceholder")}
                    step="0.01"
                    min="0"
                    value={menuItemForm.price}
                    onChange={handleFormChange}
                    required
                  />
                  <FormInput
                    type="text"
                    name="description"
                    label={t("adminMenu.descriptionLabel")}
                    placeholder={t("adminMenu.descriptionPlaceholder")}
                    value={menuItemForm.description}
                    onChange={handleFormChange}
                    required
                  />
                  <div>
                    <label className="r66-admin-menu-field-label" htmlFor="admin-menu-category">
                      {t("adminMenu.categoryLabel")}
                    </label>
                    <select
                      id="admin-menu-category"
                      name="categoryType"
                      value={menuItemForm.categoryType}
                      onChange={handleFormChange}
                      required
                    >
                      <option value="">{t("adminMenu.selectCategory")}</option>
                      {categories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="r66-admin-menu-form-actions">
                    <Button type="submit" color="green" className="r66-admin-menu-save-button">
                      {selectedMenuItem ? t("adminMenu.submitUpdate") : t("adminMenu.submitAdd")}
                    </Button>
                    {selectedMenuItem ? (
                      <>
                        <Button
                          type="button"
                          color="gray"
                          variant="link"
                          onClick={() => startCreating()}
                        >
                          {t("adminMenu.cancelEditing")}
                        </Button>
                        <Button
                          type="button"
                          color="red"
                          variant="link"
                          className="r66-admin-menu-delete-button"
                          onClick={handleDelete}
                        >
                          <FaTrashCan aria-hidden="true" />
                          <span>{t("adminMenu.delete")}</span>
                        </Button>
                      </>
                    ) : null}
                  </div>
                </form>
              </div>
            </div>
          </aside>
        ) : null}
      </div>
    </div>
  );
}

export default ManageMenu;
