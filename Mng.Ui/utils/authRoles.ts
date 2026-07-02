/** JWT / store userInfo — manager rolü kontrolü için minimum alanlar. */
export interface AuthRoleUserInfo {
  sub?: string;
  isAdmin?: boolean;
  is_admin?: boolean;
  is_manager?: boolean;
  isManager?: boolean;
  user_groups?: string[];
  userGroups?: string[];
  groups?: string[];
  roles?: string | string[];
}

const MANAGER_GROUP_NAMES = new Set(['managers', 'manager']);

function normalizeUserGroups(userInfo: AuthRoleUserInfo | null | undefined): string[] {
  const groups =
    userInfo?.user_groups ||
    userInfo?.userGroups ||
    userInfo?.groups ||
    userInfo?.roles ||
    [];

  if (Array.isArray(groups)) {
    return groups.map((g) => String(g).trim()).filter(Boolean);
  }

  if (typeof groups === 'string') {
    return groups
      .split(',')
      .map((g) => g.trim())
      .filter(Boolean);
  }

  return [];
}

/** JWT claim + managers grubu (Keeper PrivilegeGroupResolver ile uyumlu yedek). */
export function userHasManagerRole(userInfo: AuthRoleUserInfo | null | undefined): boolean {
  if (!userInfo) return false;
  if (userInfo.isAdmin === true || userInfo.is_admin === true) return true;
  if (userInfo.is_manager === true || userInfo.isManager === true) return true;

  return normalizeUserGroups(userInfo).some((group) =>
    MANAGER_GROUP_NAMES.has(group.toLowerCase())
  );
}

interface AuthReadyTarget {
  userInfo: AuthRoleUserInfo | null;
  accessToken: string | null;
  initializeAuth: () => Promise<void>;
}

/** Route middleware: token varken userInfo henüz hydrate olmamışsa doldur. */
export async function ensureAuthUserReady(auth: AuthReadyTarget): Promise<void> {
  if (auth.userInfo?.sub) return;

  const accessTokenCookie = useCookie<string | null>('access_token');
  if (!auth.accessToken && !accessTokenCookie.value) return;

  await auth.initializeAuth();
}
