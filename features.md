# Roast 66 Coffee - Future Features

Implementation notes for future development. Use this file as a guide when implementing each feature.

---

## 1. Request My Presence

**Description:** Event hosts can request the coffee trailer to pull up at their event. Admin reviews and approves/declines requests.

**User flow:**
- Public form: Event name, date/time, location/address, contact name, phone, email, estimated attendees, notes
- Submit request
- Admin sees list of requests in dashboard
- Admin can approve, decline, or schedule
- Optional: Email/SMS to requester when status changes

**Implementation notes:**
- **Backend:** New `PresenceRequest` model: Id, EventName, EventDate, Location, ContactName, ContactPhone, ContactEmail, EstimatedAttendees, Notes, Status (Pending/Approved/Declined/Scheduled), CreatedAt
- **Backend:** `PresenceRequestController` - POST (public) to create, GET/PUT (admin) to list and update status
- **Backend:** Migration for `presencerequests` table
- **Frontend:** New page `/request-presence` with form
- **Frontend:** Admin section "Presence Requests" in AdminPage - table with status badges, approve/decline buttons
- **Optional:** Twilio/email notification when admin updates status

---

## 2. Where Are We Today

**Description:** Show current location and hours on the home page so customers know where to find the trailer.

**Implementation notes:**
- **Backend:** New `LocationSettings` model or extend existing config: Address, City, State, Hours (e.g. "9am-2pm"), Optional: lat/lng for map
- **Backend:** GET endpoint (public) for current location, PUT (admin) to update
- **Frontend:** Location component on HomePage - fetch and display. If no location set, show "Check back for today's location" or similar
- **Admin:** Add "Today's Location" section in Admin dashboard - simple form to set address and hours
- **Optional:** Google Maps embed with lat/lng

---

## 3. Pre-Order for Time Window

**Description:** Customers can place an order for a future pickup time (e.g. "Ready by 2pm").

**Implementation notes:**
- **Backend:** Add `RequestedReadyTime` (DateTime?) to Order model. Migration.
- **Frontend:** OrderPage - add optional "Ready by" date/time picker. Default to "As soon as possible" (null).
- **Admin:** ViewOrders - show requested time. Consider sorting/filtering by requested time.
- **Business logic:** No validation on requested time for now; admin can use it as a guide. Future: reject if too soon or too far out.

---

## 4. Catering / Large Orders

**Description:** Separate flow for large orders (20+ drinks) with advance notice.

**Implementation notes:**
- **Option A:** Add `IsCatering` (bool) and `EstimatedAttendees` (int?) to Order. When order is large (e.g. >10 items or total quantity >20), prompt user to mark as catering and add notes.
- **Option B:** Separate `/catering` page with form: Event date, contact, estimated headcount, items (or "we'll contact you"), notes.
- **Backend:** Either extend Order or new `CateringRequest` model. Admin sees catering requests separately.
- **Admin:** Notification for catering requests (SMS/email). Different workflow (e.g. "Contact customer" vs. "Prepare order").

---

## 5. Tip

**Description:** Optional tip at checkout.

**Implementation notes:**
- **Backend:** Add `TipAmount` (decimal) to Order model. Migration.
- **Frontend:** OrderPage - before Place Order, add optional tip: preset amounts (e.g. $1, $2, $5) or custom. Include in total.
- **Backend:** OrderController - accept tipAmount in POST body. Add to Order.
- **Payment:** If cash-only, tip is collected at pickup. If adding payment later (Stripe, etc.), include tip in payment flow.
- **Admin:** ViewOrders - show tip amount. Reporting: total tips per day/week.

---

## 6. Estimated Wait

**Description:** Admin sets an estimated wait time (e.g. "~15 min") that customers see.

**Implementation notes:**
- **Backend:** New `EstimatedWaitMinutes` (int?) in app settings or `LocationSettings`. Or a simple key-value store.
- **Backend:** GET endpoint (public) for estimated wait. PUT (admin) to update.
- **Frontend:** OrderPage and/or OrderConfirmation - display "Estimated wait: ~15 minutes" when set.
- **Admin:** Simple input in Admin dashboard: "Current estimated wait: [ ] minutes".

---

## 7. SMS When Ready

**Description:** When admin marks order as "Ready for pickup", send SMS to customer.

**Implementation notes:**
- **Backend:** NotificationService - add `SendOrderReadyNotificationAsync(Order order)`. When order.OrderStatus changes to ReadyForPickup, call this. Use `order.CustomerPhone` and Twilio.
- **Backend:** Hook into OrderService.UpdateStatus - when status becomes ReadyForPickup, call NotificationService.
- **Admin:** No UI change; happens automatically when status is advanced.
- **Message:** "Your Roast 66 order #{} is ready for pickup! See you soon."
- **Privacy:** Ensure CustomerPhone is stored. Already added in Order model.

---

## 8. Order Status (Completed)

Implemented in current release:
- Order confirmation page with tracker
- Order status lookup (order ID + phone)
- Admin advances status: Received → Preparing → ReadyForPickup → Completed
- SMS to admin when new order arrives
- CustomerPhone stored on Order

---

## Priority Order (Suggested)

1. **Request My Presence** - High value for mobile business
2. **Where Are We Today** - Essential for customers finding the trailer
3. **SMS When Ready** - Quick win, uses existing Twilio
4. **Pre-Order** - Moderate complexity
5. **Estimated Wait** - Simple
6. **Tip** - Simple, depends on payment strategy
7. **Catering** - More involved, separate flow
