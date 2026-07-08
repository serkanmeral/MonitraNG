/** MngDocument API hata gövdesi (DocumentExceptionFilter). */
export type DocumentsApiErrorPayload = {
  code?: string;
  message?: string;
  messageTr?: string;
};

const GENERIC_BY_STATUS: Record<number, string> = {
  400: 'İstek geçersiz. Girdiğiniz bilgileri kontrol edin.',
  401: 'Oturum süresi dolmuş olabilir. Lütfen tekrar giriş yapın.',
  403: 'Bu işlem için yetkiniz yok.',
  404: 'İstenen kaynak bulunamadı.',
  409: 'İşlem çakışması nedeniyle tamamlanamadı.',
  502: 'Belge servisine ulaşılamadı. Gateway veya MngDocument çalışıyor mu kontrol edin.',
  503: 'Belge servisi şu an kullanılamıyor. Lütfen daha sonra tekrar deneyin.',
};

const GENERIC_SERVER =
  'Belge işlemi sırasında sunucu hatası oluştu. Ayrıntılar için yöneticiye başvurun veya MngDocument loglarını kontrol edin.';

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

/** $fetch / proxy `error.data` gövdesinden MngDocument alanlarını çıkarır. */
export function parseDocumentsApiErrorBody(data: unknown): DocumentsApiErrorPayload | null {
  if (!isPlainObject(data)) {
    if (typeof data === 'string' && data.trim() && data !== 'Internal Server Error') {
      return { message: data.trim() };
    }
    return null;
  }

  const messageTr = typeof data.messageTr === 'string' ? data.messageTr.trim() : '';
  const message = typeof data.message === 'string' ? data.message.trim() : '';
  const code = typeof data.code === 'string' ? data.code.trim() : '';

  if (messageTr || message || code) {
    return {
      code: code || undefined,
      message: message || undefined,
      messageTr: messageTr || undefined,
    };
  }

  // ASP.NET ProblemDetails
  const detail = typeof data.detail === 'string' ? data.detail.trim() : '';
  const title = typeof data.title === 'string' ? data.title.trim() : '';
  if (detail) {
    return { message: detail, messageTr: detail };
  }
  if (title && title !== 'Internal Server Error') {
    return { message: title };
  }

  const nestedError = data.error;
  if (typeof nestedError === 'string' && nestedError.trim() && nestedError !== 'Internal Server Error') {
    return { message: nestedError.trim() };
  }
  if (isPlainObject(nestedError)) {
    const nestedMessage =
      typeof nestedError.messageTr === 'string'
        ? nestedError.messageTr.trim()
        : typeof nestedError.message === 'string'
          ? nestedError.message.trim()
          : '';
    const nestedCode = typeof nestedError.code === 'string' ? nestedError.code.trim() : '';
    if (nestedMessage || nestedCode) {
      return {
        code: nestedCode || undefined,
        message: nestedMessage || undefined,
        messageTr:
          typeof nestedError.messageTr === 'string' ? nestedError.messageTr.trim() : undefined,
      };
    }
  }

  return null;
}

/** Kullanıcıya gösterilecek Türkçe/okunabilir mesaj. */
export function documentsApiErrorUserMessage(
  data: unknown,
  statusCode = 500,
  fallback = GENERIC_SERVER,
): string {
  const parsed = parseDocumentsApiErrorBody(data);
  if (parsed?.messageTr) return parsed.messageTr;
  if (parsed?.message && parsed.message !== 'Internal Server Error') return parsed.message;
  return GENERIC_BY_STATUS[statusCode] ?? fallback;
}

/** Proxy / client için normalize edilmiş hata gövdesi. */
export function normalizeDocumentsApiErrorData(
  data: unknown,
  statusCode: number,
): DocumentsApiErrorPayload {
  const parsed = parseDocumentsApiErrorBody(data);
  if (parsed?.messageTr || parsed?.message || parsed?.code) {
    return {
      code: parsed.code ?? 'DOCUMENT_API_ERROR',
      message: parsed.message,
      messageTr: parsed.messageTr ?? documentsApiErrorUserMessage(data, statusCode),
    };
  }

  const messageTr = documentsApiErrorUserMessage(data, statusCode);
  return {
    code: 'DOCUMENT_API_ERROR',
    message: typeof data === 'string' ? data : undefined,
    messageTr,
  };
}
