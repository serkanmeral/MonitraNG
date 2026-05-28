import type { OcFormFieldRuntimeDto, OcFormRuntimeContext } from '@/types/apps/operationCore';

export interface OcFormValidationIssue {
  fieldKey: string;
  label: string;
}

/** Layout sırasına göre görünür alan anahtarları (OcDynamicForm ile uyumlu). */
export function visibleOcFormFieldKeys(ctx: OcFormRuntimeContext): string[] {
  const keys: string[] = [];
  if (ctx.layout?.sections?.length) {
    for (const section of ctx.layout.sections) {
      for (const key of section.fields) {
        const behavior = ctx.fieldBehaviors[key];
        if (behavior?.visible === false) continue;
        keys.push(key);
      }
    }
    return [...new Set(keys)];
  }
  return Object.keys(ctx.fields).filter((key) => ctx.fieldBehaviors[key]?.visible !== false);
}

export function isOcFieldValueFilled(
  value: unknown,
  meta?: OcFormFieldRuntimeDto | null,
  options?: { required?: boolean }
): boolean {
  if (meta?.fieldType === 'bool' && options?.required) {
    return value === true;
  }
  if (value === undefined || value === null) return false;
  if (Array.isArray(value)) return value.length > 0;
  if (typeof value === 'boolean') return true;
  if (typeof value === 'number' && !Number.isNaN(value)) return true;
  return String(value).trim() !== '';
}

export function collectOcFormValidationIssues(
  ctx: OcFormRuntimeContext,
  model: Record<string, unknown>
): OcFormValidationIssue[] {
  const issues: OcFormValidationIssue[] = [];
  const seen = new Set<string>();

  function pushIssue(fieldKey: string) {
    if (seen.has(fieldKey)) return;
    seen.add(fieldKey);
    const label = ctx.fields[fieldKey]?.label?.trim() || fieldKey;
    issues.push({ fieldKey, label });
  }

  for (const key of visibleOcFormFieldKeys(ctx)) {
    const behavior = ctx.fieldBehaviors[key];
    if (behavior?.required !== true) continue;
    if (!isOcFieldValueFilled(model[key], ctx.fields[key], { required: true })) {
      pushIssue(key);
    }
  }

  for (const key of ['title', 'typeId'] as const) {
    if (seen.has(key)) continue;
    if (!(key in ctx.fields)) continue;
    if (ctx.fieldBehaviors[key]?.visible === false) continue;
    if (!isOcFieldValueFilled(model[key], ctx.fields[key], { required: true })) {
      pushIssue(key);
    }
  }

  return issues;
}

export function validateOcFormModel(
  ctx: OcFormRuntimeContext,
  model: Record<string, unknown>
): boolean {
  return collectOcFormValidationIssues(ctx, model).length === 0;
}
