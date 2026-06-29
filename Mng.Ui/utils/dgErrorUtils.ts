export interface DgValidationError {
  field?: string;
  code?: string;
  message?: string;
  value?: unknown;
}

export interface DgErrorPayload {
  success?: boolean;
  error?: {
    code?: string;
    message?: string;
    details?: unknown;
  };
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : null;
}

/** Resolves nested error payloads from Nuxt/H3 wrapping. */
export function dgResolveErrorPayload(error: unknown): DgErrorPayload | null {
  const root = asRecord(error);
  if (!root) return null;

  const data = asRecord(root.data) ?? root;
  if (data?.error && typeof data.error === 'object') {
    return data as DgErrorPayload;
  }

  const nested = asRecord(data?.data);
  if (nested?.error && typeof nested.error === 'object') {
    return nested as DgErrorPayload;
  }

  if (data?.success === false && data.error) {
    return data as DgErrorPayload;
  }

  return null;
}

/** HTTP status code (e.g. 409 conflict). */
export function dgErrorStatus(error: unknown): number | null {
  const root = asRecord(error);
  if (!root) return null;
  const sc = root.statusCode ?? root.status;
  return typeof sc === 'number' ? sc : null;
}

/** Machine-readable error code from DG response. */
export function dgErrorCode(error: unknown): string | null {
  const payload = dgResolveErrorPayload(error);
  const code = payload?.error?.code;
  return typeof code === 'string' && code ? code : null;
}

export function dgIsDuplicateError(error: unknown): boolean {
  if (dgErrorCode(error) === 'DUPLICATE_KEY' || dgErrorStatus(error) === 409) {
    return true;
  }
  return dgExtractValidationErrors(error).some(
    (e) => e.code === 'VALIDATION_UNIQUE_CONSTRAINT' || e.code === 'DUPLICATE_KEY',
  );
}

export function dgIsValidationError(error: unknown): boolean {
  const status = dgErrorStatus(error);
  const code = dgErrorCode(error);
  return status === 400
    || status === 409
    || code === 'VALIDATION_ERROR'
    || code === 'DUPLICATE_KEY'
    || code === 'VALIDATION_UNIQUE_CONSTRAINT'
    || (dgExtractValidationErrors(error).length > 0);
}

/** Field-level validation errors from `error.details` array. */
export function dgExtractValidationErrors(error: unknown): DgValidationError[] {
  const payload = dgResolveErrorPayload(error);
  const details = payload?.error?.details;

  if (Array.isArray(details)) {
    return details
      .filter((item): item is DgValidationError => !!item && typeof item === 'object')
      .map((item) => ({
        field: typeof item.field === 'string' ? item.field : undefined,
        code: typeof item.code === 'string' ? item.code : undefined,
        message: typeof item.message === 'string' ? item.message : undefined,
        value: item.value,
      }));
  }

  // Single validation error object (some proxies serialize one-item arrays as object)
  if (details && typeof details === 'object') {
    const item = details as DgValidationError;
    if (item.field || item.message) {
      return [{
        field: typeof item.field === 'string' ? item.field : undefined,
        code: typeof item.code === 'string' ? item.code : undefined,
        message: typeof item.message === 'string' ? item.message : undefined,
        value: item.value,
      }];
    }
  }

  return [];
}

function extractInnerMessage(error: unknown): string | null {
  const payload = dgResolveErrorPayload(error);
  const details = payload?.error?.details;

  if (typeof details === 'string' && details) return details;

  if (details && typeof details === 'object' && !Array.isArray(details)) {
    const d = details as Record<string, unknown>;
    if (typeof d.innerException === 'string' && d.innerException) return d.innerException;
    if (typeof d.message === 'string' && d.message) return d.message;
  }

  return null;
}

/** Human-readable message; avoids generic "internal server error" when details exist. */
export function dgExtractMessage(error: unknown, fallback: string): string {
  const payload = dgResolveErrorPayload(error);
  const inner = extractInnerMessage(error);

  const candidates = [
    payload?.error?.message,
    inner,
    error instanceof Error ? error.message : null,
    asRecord(error)?.statusMessage as string | undefined,
  ];

  for (const candidate of candidates) {
    if (typeof candidate !== 'string' || !candidate.trim()) continue;
    const lower = candidate.toLowerCase();
    if (lower === 'internal server error' || lower === 'failed to create data') continue;
    return candidate;
  }

  const validationErrors = dgExtractValidationErrors(error);
  if (validationErrors.length > 0) {
    return validationErrors.map((e) => e.message || e.field || '').filter(Boolean).join('\n');
  }

  return fallback;
}

/** Maps validation errors to field → messages for inline form display. */
export function dgValidationErrorMap(error: unknown): Record<string, string[]> {
  const map: Record<string, string[]> = {};
  for (const item of dgExtractValidationErrors(error)) {
    const field = item.field || '_form';
    if (!map[field]) map[field] = [];
    if (item.message) map[field].push(item.message);
  }
  return map;
}
