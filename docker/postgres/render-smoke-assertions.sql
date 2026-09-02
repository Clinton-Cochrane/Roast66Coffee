DO $$
BEGIN
    IF (SELECT count(*) FROM menuitems WHERE name = 'CI Smoke Latte') <> 1 THEN
        RAISE EXCEPTION 'Upgrade regression: expected smoke menu item was not preserved';
    END IF;

    IF (
        SELECT count(*)
        FROM orders
        WHERE customername = 'CI Smoke Customer'
          AND orderstatus = 0
          AND trackingtoken = repeat('s', 43)
    ) <> 1 THEN
        RAISE EXCEPTION 'Upgrade regression: expected incomplete order was not preserved';
    END IF;

    IF (
        SELECT count(*)
        FROM orderitems AS line
        JOIN orders AS customer_order ON customer_order.id = line.orderid
        JOIN menuitems AS menu_item ON menu_item.id = line.menuitemid
        WHERE customer_order.customername = 'CI Smoke Customer'
          AND menu_item.name = 'CI Smoke Latte'
          AND line.quantity = 1
          AND line.unit_price = 5.25
          AND line.item_name = 'CI Smoke Latte'
          AND line.item_description = 'Known menu item preserved across the candidate deployment'
    ) <> 1 THEN
        RAISE EXCEPTION 'Upgrade regression: expected order line or snapshot was not preserved';
    END IF;

    IF (
        SELECT count(*)
        FROM addons AS add_on
        JOIN orderitems AS line ON line."Id" = add_on.orderitemid
        JOIN orders AS customer_order ON customer_order.id = line.orderid
        JOIN menuitems AS menu_item ON menu_item.id = add_on.menuitemid
        WHERE customer_order.customername = 'CI Smoke Customer'
          AND menu_item.name = 'CI Smoke Flavor'
          AND add_on.quantity = 1
          AND add_on.unit_price = 0.75
          AND add_on.item_name = 'CI Smoke Flavor'
          AND add_on.item_description = 'Known add-on preserved across the candidate deployment'
    ) <> 1 THEN
        RAISE EXCEPTION 'Upgrade regression: expected add-on or snapshot was not preserved';
    END IF;
END $$;
