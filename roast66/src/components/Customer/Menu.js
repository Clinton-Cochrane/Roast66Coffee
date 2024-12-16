// src/components/Customer/Menu.jsx
import React, { useEffect, useState } from "react";
import axios from "../../axiosConfig";
import Card from "../common/Card";
import CategoryType from "../../constants/categories";
import Loading from "../common/Loading";

function Menu() {
  const [menuItems, setMenuItems] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  const getCategoryName = (categoryType) => {
    switch (categoryType) {
      case CategoryType.COFFEE:
        return "Coffee";
      case CategoryType.SPECIALS:
        return "Specials";
      case CategoryType.FLAVORS:
        return "Flavors";
      case CategoryType.DRINKS:
        return "Drink";
      default:
        return "Unknown";
    }
  };

  useEffect(() => {
    axios
      .get("/admin/menu")
      .then((response) => {
        setMenuItems(response.data);
      })
      .catch((error) => {
        console.error("Error fetching menu items:", error);
      })
      .finally(() => {
        //setIsLoading(false);
      });
  }, []);

  const groupedItems = {
    Drinks: menuItems.filter(
      (item) =>
        item.categoryType === CategoryType.COFFEE ||
        item.categoryType === CategoryType.DRINKS
    ),
    Specials: menuItems.filter(
      (item) => item.categoryType === CategoryType.SPECIALS
    ),
    Flavors: menuItems.filter(
      (item) => item.categoryType === CategoryType.FLAVORS
    ),
  };

  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <h2 className="text-3xl font-bold mb-6 text-center">Our Menu</h2>

      {isLoading ? (
        <Loading />
      ) : (
        Object.entries(groupedItems).map(
          ([categoryName, items]) =>
            items.length > 0 && (
              <div key={categoryName} className="mb-8">
                <h3 className="text-2xl font-semibold mb-4">{categoryName}</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                  {items.map((item) => (
                    <Card key={item.id}>
                      <h3 className="text-xl font-semibold mb-2">
                        {item.name}
                      </h3>
                      <p className="text-gray-700">${item.price.toFixed(2)}</p>
                      <p className="text-gray-700">{item.description}</p>
                      <p className="text-gray-700">
                        {getCategoryName(item.categoryType)}
                      </p>
                    </Card>
                  ))}
                </div>
              </div>
            )
        )
      )}
    </div>
  );
}

export default Menu;
