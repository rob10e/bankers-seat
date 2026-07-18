export const formatMoney = (amountBaseUnits: number): string => {
  return new Intl.NumberFormat("en-US", {
    maximumFractionDigits: 0,
  }).format(amountBaseUnits);
};
