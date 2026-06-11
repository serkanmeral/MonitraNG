/**
 * Transition dialog defaults — pool field conventions (partial shipment, quality accept qty).
 */

function toNumber(value: unknown): number | null {
  if (value == null || value === '') return null;
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? n : null;
}

function poolField(model: Record<string, unknown>, key: string): unknown {
  return model[key];
}

/** Shippable cap = acceptedQty ?? quantity */
export function ocShippableQty(model: Record<string, unknown>): number {
  const accepted = toNumber(poolField(model, 'acceptedQty'));
  if (accepted != null && accepted > 0) return accepted;
  return toNumber(poolField(model, 'quantity')) ?? 0;
}

export function ocRemainingShipQty(model: Record<string, unknown>): number {
  const cap = ocShippableQty(model);
  const shipped = toNumber(poolField(model, 'shippedQty')) ?? 0;
  return Math.max(0, cap - shipped);
}

/**
 * Seed transition field model when opening the dialog.
 */
export function ocSeedTransitionFieldModel(
  transitionKey: string,
  workItemModel: Record<string, unknown>,
  requiredKeys: string[]
): Record<string, unknown> {
  const seed: Record<string, unknown> = {};
  for (const key of requiredKeys) {
    const cur = workItemModel[key];
    if (cur != null && cur !== '') seed[key] = cur;
  }

  const remaining = ocRemainingShipQty(workItemModel);

  if (transitionKey === 'ship_partial' || transitionKey === 'ship') {
    if (seed.shipmentQty == null && remaining > 0) seed.shipmentQty = remaining;
  }

  if (transitionKey === 'approve_quality') {
    const qty = toNumber(poolField(workItemModel, 'quantity'));
    if (seed.acceptedQty == null && qty != null && qty > 0) seed.acceptedQty = qty;
  }

  return seed;
}

/**
 * Merge transition payload before POST (client-side hints; MO validates again).
 */
export function ocEnrichTransitionFields(
  transitionKey: string,
  workItemModel: Record<string, unknown>,
  fields: Record<string, unknown> | null
): Record<string, unknown> | null {
  if (!fields) return null;

  const out = { ...fields };

  if (transitionKey === 'approve_quality') {
    const result = String(out.qualityResult ?? '').toLowerCase();
    if (result === 'uygun' && out.acceptedQty == null) {
      const qty = toNumber(poolField(workItemModel, 'quantity'));
      if (qty != null && qty > 0) out.acceptedQty = qty;
    }
  }

  if (transitionKey === 'hold_quality') {
    if (!out.qualityResult) out.qualityResult = 'uygunsuz';
  }

  return out;
}
