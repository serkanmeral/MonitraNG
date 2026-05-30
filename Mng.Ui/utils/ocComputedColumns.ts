import { Parser, type Expression } from 'expr-eval';

/**
 * Dinamik (computed) liste sütunları için güvenli ifade değerlendirici.
 * `expr-eval` `eval` kullanmaz; yalnızca aritmetik/mantıksal ifadeler + whitelist'li
 * matematik fonksiyonlarını destekler. Faz-1: display-only (sunucu sort/filter yok).
 */
const parser = new Parser();

const parseCache = new Map<string, Expression | null>();

function parseExpr(expr: string): Expression | null {
  const trimmed = expr?.trim();
  if (!trimmed) return null;
  if (parseCache.has(trimmed)) return parseCache.get(trimmed) ?? null;
  let parsed: Expression | null = null;
  try {
    parsed = parser.parse(trimmed);
  } catch {
    parsed = null;
  }
  parseCache.set(trimmed, parsed);
  return parsed;
}

/** İfade söz dizimsel olarak geçerli mi? (admin editöründe anlık doğrulama) */
export function isValidComputedExpr(expr: string): boolean {
  return parseExpr(expr) != null;
}

export interface ComputedEvalResult {
  ok: boolean;
  value: unknown;
  error: string | null;
}

/** Bir satır bağlamında ifadeyi değerlendirir; hata olursa `ok=false`. */
export function evaluateComputedExpr(
  expr: string,
  scope: Record<string, unknown>
): ComputedEvalResult {
  const parsed = parseExpr(expr);
  if (!parsed) return { ok: false, value: null, error: 'parse' };
  try {
    const vars = buildVariables(parsed, scope);
    const value = parsed.evaluate(vars);
    return { ok: true, value, error: null };
  } catch (e) {
    return { ok: false, value: null, error: e instanceof Error ? e.message : 'eval' };
  }
}

/** İfadenin kullandığı değişkenleri bağlamdan toplar; eksikler güvenli varsayılana iner. */
function buildVariables(parsed: Expression, scope: Record<string, unknown>): Record<string, unknown> {
  let names: string[] = [];
  try {
    names = parsed.variables({ withMembers: false });
  } catch {
    names = [];
  }
  const vars: Record<string, unknown> = {};
  for (const name of names) {
    vars[name] = coerceValue(scope[name]);
  }
  return vars;
}

/**
 * Ham alan değerini ifade için kullanılabilir skalere çevirir:
 * - number/boolean → aynen
 * - sayısal string → number, değilse string
 * - dizi → eleman sayısı (çoklu değer/etiket sayımı için kullanışlı)
 * - null/undefined/nesne → 0
 */
function coerceValue(v: unknown): number | string | boolean {
  if (typeof v === 'number' || typeof v === 'boolean') return v;
  if (typeof v === 'string') {
    const trimmed = v.trim();
    if (trimmed === '') return '';
    const n = Number(trimmed);
    return Number.isFinite(n) ? n : v;
  }
  if (Array.isArray(v)) return v.length;
  return 0;
}
