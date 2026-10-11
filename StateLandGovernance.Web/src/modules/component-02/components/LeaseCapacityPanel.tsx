import s from "./FinancialFlow.module.css";

// Integer cents avoid floating point comparisons at the 60% boundary.
export function paymentCapacity(income: string, debt: string, lease: string) {
  const cents = (value: string) => {
    if (!/^\d+(?:\.\d{1,2})?$/.test(value)) return null;
    const [whole, fraction = ""] = value.split(".");
    const amount = Number(whole) * 100 + Number(fraction.padEnd(2, "0"));
    return Number.isSafeInteger(amount) && amount <= 1_000_000_000_000 ? amount : null;
  };
  const i = cents(income), d = cents(debt), l = cents(lease);
  if (i === null || d === null || l === null || i <= 0 || l <= 0) return null;
  const budget = Math.floor(i * 3 / 5);
  return { budget, debt: d, available: Math.max(0, budget - d), requested: l,
    excess: Math.max(0, d + l - budget), within: d + l <= budget };
}

export function LeaseCapacityPanel({ income, debt, lease }: { income: string; debt: string; lease: string }) {
  const capacity = paymentCapacity(income, debt, lease);
  const money = (cents: number) => `LKR ${(cents / 100).toLocaleString("en-LK", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  return <section className={s.card} aria-label="Monthly lease payment capacity">
    <h2>Monthly lease payment capacity</h2>
    <p className={s.muted}>60% of monthly income − existing monthly debt payments. Include credit cards, loans and finance repayments. This is the configured research rule.</p>
    {capacity ? <>
      <dl className={s.details}>
        <dt>Maximum total payments (60%)</dt><dd>{money(capacity.budget)}</dd>
        <dt>Existing monthly debt payments</dt><dd>{money(capacity.debt)}</dd>
        <dt>Available for the land lease</dt><dd><strong>{money(capacity.available)}</strong></dd>
        <dt>Requested lease installment</dt><dd>{money(capacity.requested)}</dd>
      </dl>
      <p role="status" className={capacity.within ? s.success : s.error}>{capacity.within
        ? "The requested installment fits within the 60% limit."
        : `The requested installment exceeds the limit by ${money(capacity.excess)}. Review or reduce the installment; escalation is required.`}</p>
      {capacity.available === 0 && <p className={s.error}>No monthly lease payment capacity remains.</p>}
    </> : <p role="status">Enter positive monthly income and lease payment, and non-negative debt, with up to two decimal places.</p>}
    <p className={s.muted}>Calculated from the fields entered here. Confirm income and all monthly repayments against the documents before using this result. Outstanding loan balances are separate.</p>
  </section>;
}
