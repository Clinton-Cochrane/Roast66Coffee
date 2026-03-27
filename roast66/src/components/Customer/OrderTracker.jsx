import React from "react";
import PropTypes from "prop-types";
import {
  ORDER_STATUS,
  ORDER_STATUS_LABELS,
  ORDER_STATUS_DESCRIPTIONS,
} from "../../constants/orderStatus";
import { useI18n } from "../../i18n/LanguageContext";

const STAGES = [
  ORDER_STATUS.Received,
  ORDER_STATUS.Preparing,
  ORDER_STATUS.ReadyForPickup,
  ORDER_STATUS.Completed,
];

function OrderTracker({ currentStatus }) {
  const { t } = useI18n();
  const currentIndex = STAGES.indexOf(
    typeof currentStatus === "number" ? currentStatus : ORDER_STATUS.Received
  );
  const safeIndex = currentIndex >= 0 ? currentIndex : 0;

  const localizedLabels = {
    [ORDER_STATUS.Received]: t("orderStatus.trackerReceivedLabel"),
    [ORDER_STATUS.Preparing]: t("orderStatus.trackerPreparingLabel"),
    [ORDER_STATUS.ReadyForPickup]: t("orderStatus.trackerReadyLabel"),
    [ORDER_STATUS.Completed]: t("orderStatus.trackerCompletedLabel"),
  };

  const localizedDescriptions = {
    [ORDER_STATUS.Received]: t("orderStatus.trackerReceivedDescription"),
    [ORDER_STATUS.Preparing]: t("orderStatus.trackerPreparingDescription"),
    [ORDER_STATUS.ReadyForPickup]: t("orderStatus.trackerReadyDescription"),
    [ORDER_STATUS.Completed]: t("orderStatus.trackerCompletedDescription"),
  };

  return (
    <div className="order-tracker space-y-4">
      {STAGES.map((status, index) => {
        const isComplete = index < safeIndex || status === currentStatus;
        const isCurrent = status === currentStatus;
        return (
          <div
            key={status}
            className={`flex items-start gap-4 ${
              isCurrent ? "text-green-800 font-semibold" : ""
            }`}
          >
            <div
              className={`flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center ${
                isComplete
                  ? "bg-green-600 text-white"
                  : "bg-gray-200 text-gray-500"
              }`}
            >
              {index < safeIndex ? (
                <span className="text-lg">✓</span>
              ) : (
                <span className="text-sm">{index + 1}</span>
              )}
            </div>
            <div className="flex-1">
              <p className="font-medium">
                {localizedLabels[status] ?? ORDER_STATUS_LABELS[status]}
              </p>
              <p className="text-sm text-gray-600">
                {localizedDescriptions[status] ?? ORDER_STATUS_DESCRIPTIONS[status]}
              </p>
            </div>
          </div>
        );
      })}
    </div>
  );
}

OrderTracker.propTypes = {
  currentStatus: PropTypes.number.isRequired,
};

export default OrderTracker;
