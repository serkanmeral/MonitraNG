/** Set only by a successful password login. Page refresh does not set it. */
export const LOGIN_WELCOME_FLAG = 'mng.ui.loginWelcome';

export function markLoginWelcomePending(): void {
  if (!import.meta.client) return;
  sessionStorage.setItem(LOGIN_WELCOME_FLAG, '1');
}

export function clearLoginWelcomePending(): void {
  if (!import.meta.client) return;
  sessionStorage.removeItem(LOGIN_WELCOME_FLAG);
}

/** Returns true once per login. Later calls in the same tab return false. */
export function consumeLoginWelcomePending(): boolean {
  if (!import.meta.client) return false;
  if (sessionStorage.getItem(LOGIN_WELCOME_FLAG) !== '1') return false;
  sessionStorage.removeItem(LOGIN_WELCOME_FLAG);
  return true;
}
