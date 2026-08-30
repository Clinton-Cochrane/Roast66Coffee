WITH drink AS (
    INSERT INTO menuitems (
        name,
        price,
        description,
        "CategoryType",
        is_featured_on_home,
        is_archived,
        promotion_type,
        promotion_value)
    VALUES (
        'CI Smoke Latte',
        5.25,
        'Known menu item preserved across the candidate deployment',
        0,
        false,
        false,
        NULL,
        NULL)
    RETURNING id
), flavor AS (
    INSERT INTO menuitems (
        name,
        price,
        description,
        "CategoryType",
        is_featured_on_home,
        is_archived,
        promotion_type,
        promotion_value)
    VALUES (
        'CI Smoke Flavor',
        0.75,
        'Known add-on preserved across the candidate deployment',
        2,
        false,
        false,
        NULL,
        NULL)
    RETURNING id
), smoke_order AS (
    INSERT INTO orders (
        customername,
        customerphone,
        customeremail,
        customernotificationoptin,
        orderdate,
        orderstatus,
        trackingtoken)
    VALUES (
        'CI Smoke Customer',
        '555-0199',
        'smoke@example.test',
        false,
        now(),
        0,
        repeat('s', 43))
    RETURNING id
), line AS (
    INSERT INTO orderitems (
        orderid,
        menuitemid,
        quantity,
        "Customer_Notes",
        unit_price,
        item_name,
        item_description,
        item_category_type)
    SELECT
        smoke_order.id,
        drink.id,
        1,
        'Release smoke fixture',
        5.25,
        'CI Smoke Latte',
        'Known menu item preserved across the candidate deployment',
        0
    FROM smoke_order, drink
    RETURNING "Id"
)
INSERT INTO addons (
    menuitemid,
    quantity,
    orderitemid,
    unit_price,
    item_name,
    item_description,
    item_category_type)
SELECT
    flavor.id,
    1,
    line."Id",
    0.75,
    'CI Smoke Flavor',
    'Known add-on preserved across the candidate deployment',
    2
FROM flavor, line;
