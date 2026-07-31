/**
 * Lightweight Windows Security (4624/4625/4634) message field parsing.
 * Prefer Target / New Logon account over Subject (often SYSTEM / HOST$).
 */

export interface WindowsSecurityLogonParsed {
  targetAccount: string | null;
  subjectAccount: string | null;
  logonType: string | null;
  displayUser: string | null;
}

/** Normalize real and literal escape sequences from Event Log message blobs. */
export function normalizeSecurityMessage(message: string): string {
  return message
    .replace(/\u0000/g, '')
    // Literal escapes that sometimes survive JSON / preview pipelines
    .replace(/\\r\\n/g, '\n')
    .replace(/\\n/g, '\n')
    .replace(/\\t/g, '\t')
    .replace(/\\r/g, '\n')
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n');
}

function cleanAccount(raw: string | null | undefined): string | null {
  if (!raw) return null;
  let t = normalizeSecurityMessage(raw)
    .replace(/\t/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  // Drop junk if capture swallowed following fields
  if (t.includes('Account Domain:') || t.includes('Logon ID:') || t.includes('Security ID:')) {
    t = t.split(/\s{2,}|Account Domain:|Logon ID:|Security ID:/i)[0]?.trim() || '';
  }
  if (!t || t === '-' || t === '—') return null;
  // SAM / UPN-ish token only
  if (t.length > 128) return null;
  if (/\\[rtn]/.test(t)) return null;
  return t;
}

function accountFromSection(section: string): string | null {
  // Prefer tight token after Account Name (tabs/spaces allowed)
  const nameMatch =
    section.match(/Account Name:\s*([A-Za-z0-9._$-]+)/i)
    || section.match(/Account Name:\s*([^\n]+)/i);
  const domainMatch =
    section.match(/Account Domain:\s*([A-Za-z0-9._$-]+)/i)
    || section.match(/Account Domain:\s*([^\n]+)/i);
  const name = cleanAccount(nameMatch?.[1]);
  if (!name) return null;
  const domain = cleanAccount(domainMatch?.[1]);
  if (domain && domain !== '-' && !name.includes('\\')) return `${domain}\\${name}`;
  return name;
}

function sectionAfter(blob: string, header: RegExp): string | null {
  const m = blob.match(header);
  if (!m || m.index == null) return null;
  const start = m.index + m[0].length;
  const rest = blob.slice(start);
  const next = rest.search(
    /\n(?:Subject|Logon Information|New Logon|Network Information|Process Information|Detailed Authentication Information|Account For Which Logon Failed|Account That Was Logged On|Failure Information|Additional Information):/i,
  );
  return next >= 0 ? rest.slice(0, next) : rest;
}

export function parseWindowsSecurityLogonMessage(message: string | null | undefined): WindowsSecurityLogonParsed {
  const blob = normalizeSecurityMessage(message || '');
  if (!blob.trim()) {
    return { targetAccount: null, subjectAccount: null, logonType: null, displayUser: null };
  }

  const subject = accountFromSection(sectionAfter(blob, /Subject:/i) || '');
  const newLogon = accountFromSection(sectionAfter(blob, /New Logon:/i) || '');
  const accountLoggedOn = accountFromSection(
    sectionAfter(blob, /Account That Was Logged On:/i) || '',
  );
  const failedAccount = accountFromSection(
    sectionAfter(blob, /Account For Which Logon Failed:/i) || '',
  );

  let targetAccount = newLogon || accountLoggedOn || failedAccount;

  if (!targetAccount) {
    const allNames = [...blob.matchAll(/Account Name:\s*([A-Za-z0-9._$-]+)/gi)]
      .map((m) => cleanAccount(m[1]))
      .filter((x): x is string => !!x);
    if (allNames.length >= 2) targetAccount = allNames[1]!;
    else if (allNames.length === 1) targetAccount = allNames[0]!;
  }

  const logonTypeMatch =
    blob.match(/Logon Type:\s*(\d+)/i)
    || blob.match(/LogonType[=:]\s*(\d+)/i);

  const displayUser = targetAccount || subject;

  return {
    targetAccount,
    subjectAccount: subject,
    logonType: logonTypeMatch?.[1] ?? null,
    displayUser,
  };
}

/** Computer accounts end with $ (e.g. TERMINAL$). */
export function isWindowsMachineAccount(account: string | null | undefined): boolean {
  if (!account) return false;
  const leaf = account.includes('\\') ? account.split('\\').pop()! : account;
  return leaf.trim().endsWith('$');
}

/**
 * Logon types that usually mean a human session (not service/network noise).
 * 2 Interactive, 7 Unlock, 10 RemoteInteractive (RDP), 11 CachedInteractive
 */
export const INTERACTIVE_LOGON_TYPES = new Set(['2', '7', '10', '11']);

export function isInteractiveLogonType(logonType: string | null | undefined): boolean {
  if (!logonType) return false;
  return INTERACTIVE_LOGON_TYPES.has(String(logonType).trim());
}

export function securityMessageFromEventFields(
  fields?: Record<string, unknown> | null,
  raw?: string | null,
  rawPreview?: string | null,
  /** Windows list API often puts the full Event Log text in eventAction. */
  eventAction?: string | null,
): string {
  const fromFields = fields?.message;
  if (typeof fromFields === 'string' && fromFields.trim()) return fromFields;
  if (raw && raw.trim()) return raw;
  // Prefer long eventAction blob over short rawPreview when present
  if (eventAction && eventAction.trim().length > 40) return eventAction;
  if (rawPreview && rawPreview.trim()) return rawPreview;
  if (eventAction && eventAction.trim()) return eventAction;
  return '';
}

/** Terminal Services LocalSessionManager Operational (21/23/24/25). */
export function parseWindowsRdpSessionMessage(message: string | null | undefined): {
  user: string | null;
  sourceAddress: string | null;
} {
  const blob = normalizeSecurityMessage(message || '');
  if (!blob.trim()) return { user: null, sourceAddress: null };

  const userMatch =
    blob.match(/User:\s*([^\r\n]+)/i)
    || blob.match(/User Name:\s*([^\r\n]+)/i);
  const addrMatch =
    blob.match(/Source Network Address:\s*([^\r\n]+)/i)
    || blob.match(/Client Address:\s*([^\r\n]+)/i);

  const user = cleanAccount(userMatch?.[1]?.trim());
  let sourceAddress = (addrMatch?.[1] || '').trim();
  if (!sourceAddress || sourceAddress === '-' || sourceAddress === '—') sourceAddress = '';

  return {
    user,
    sourceAddress: sourceAddress || null,
  };
}

/** Security auth + RDP session channel event IDs used by host session history. */
export const SESSION_HISTORY_SECURITY_EVENT_IDS = new Set(['4624', '4625', '4634', '4647']);
export const SESSION_HISTORY_RDP_EVENT_IDS = new Set(['21', '23', '24', '25']);
export const SESSION_HISTORY_EVENT_IDS = new Set([
  ...SESSION_HISTORY_SECURITY_EVENT_IDS,
  ...SESSION_HISTORY_RDP_EVENT_IDS,
]);
