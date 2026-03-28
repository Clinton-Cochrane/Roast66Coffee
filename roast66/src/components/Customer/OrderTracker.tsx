import React from "react";
import { ORDER_STATUS, type OrderStatusValue } from "../../constants/orderStatus";
import { useI18n } from "../../i18n/LanguageContext";

const STAGES: OrderStatusValue[] = [
  ORDER_STATUS.Received,
  ORDER_STATUS.Preparing,
  ORDER_STATUS.ReadyForPickup,
  ORDER_STATUS.Completed,
];

type OrderTrackerProps = {
  currentStatus: number;
};

function OrderTracker({ currentStatus }: OrderTrackerProps) {
  const { t } = useI18n();
  const numericStatus = typeof currentStatus === "number" ? currentStatus : Number(currentStatus);
  const currentIndex = STAGES.indexOf(numericStatus as OrderStatusValue);
  const safeIndex = currentIndex >= 0 ? currentIndex : 0;

  const localizedLabels: Record<OrderStatusValue, string> = {
    [ORDER_STATUS.Received]: t("orderStatus.trackerReceivedLabel"),
    [ORDER_STATUS.Preparing]: t("orderStatus.trackerPreparingLabel"),
    [ORDER_STATUS.ReadyForPickup]: t("orderStatus.trackerReadyLabel"),
    [ORDER_STATUS.Completed]: t("orderStatus.trackerCompletedLabel"),
  };

  const localizedDescriptions: Record<OrderStatusValue, string> = {
    [ORDER_STATUS.Received]: t("orderStatus.trackerReceivedDescription"),
    [ORDER_STATUS.Preparing]: t("orderStatus.trackerPreparingDescription"),
    [ORDER_STATUS.ReadyForPickup]: t("orderStatus.trackerReadyDescription"),
    [ORDER_STATUS.Completed]: t("orderStatus.trackerCompletedDescription"),
  };

  return (
    <div className="order-tracker space-y-4">
      {STAGES.map((status, index) => {
        const isComplete = index < safeIndex || status === numericStatus;
        const isCurrent = status === numericStatus;
        return (
          <div
            key={status}
            className={`flex items-start gap-4 ${isCurrent ? "text-green-800 font-semibold" : ""}`}
          >
            <div
              className={`flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center ${
                isComplete ? "bg-green-600 text-white" : "bg-gray-200 text-gray-500"
              }`}
            >
              {index < safeIndex ? <span className="text-lg">✓</span> : <span className="text-sm">{index + 1}</span>}
            </div>
            <div className="flex-1">
              <p className="font-medium">{localizedLabels[status]}</p>
              <p className="text-sm text-gray-600">{localizedDescriptions[status]}</p>
            </div>
          </div>
        );
      })}
    </div>
  );
}

export default OrderTracker;
