import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import type { DashboardPermissions } from '@/stores/apps/dashboard';

/**
 * Dashboard Permissions Composable
 * Dashboard bazlı yetkilendirme kontrolü için composable
 */
export function useDashboardPermissions() {
  const authStore = useAuthStore();

  /**
   * Dashboard view permission kontrolü
   * @param permissions Dashboard permissions objesi
   * @returns Kullanıcının dashboard'u görüntüleme yetkisi var mı?
   */
  function canViewDashboard(permissions?: DashboardPermissions | null): boolean {
    // Admin bypass
    if (authStore.isAdmin) {
      return true;
    }

    // Permissions tanımlı değilse herkes erişebilir
    if (!permissions || !permissions.view) {
      return true;
    }

    const viewPerms = permissions.view;

    // Groups kontrolü
    if (viewPerms.groups && viewPerms.groups.length > 0) {
      const hasGroupPermission = authStore.userGroups.some((userGroup) =>
        viewPerms.groups!.some((requiredGroup) =>
          userGroup.toLowerCase() === requiredGroup.toLowerCase()
        )
      );
      if (hasGroupPermission) {
        return true;
      }
    }

    // Users kontrolü (opsiyonel)
    if (viewPerms.users && viewPerms.users.length > 0) {
      const userId = authStore.userInfo?.sub || authStore.userInfo?.uid;
      if (userId && viewPerms.users.includes(userId)) {
        return true;
      }
    }

    // Hiçbir yetki yoksa erişim yok
    return false;
  }

  /**
   * Dashboard edit permission kontrolü
   * @param permissions Dashboard permissions objesi
   * @returns Kullanıcının dashboard'u düzenleme yetkisi var mı?
   */
  function canEditDashboard(permissions?: DashboardPermissions | null): boolean {
    // Admin bypass
    if (authStore.isAdmin) {
      return true;
    }

    // Permissions tanımlı değilse herkes düzenleyebilir (backward compatibility)
    if (!permissions || !permissions.edit) {
      return true;
    }

    const editPerms = permissions.edit;

    // Groups kontrolü
    if (editPerms.groups && editPerms.groups.length > 0) {
      const hasGroupPermission = authStore.userGroups.some((userGroup) =>
        editPerms.groups!.some((requiredGroup) =>
          userGroup.toLowerCase() === requiredGroup.toLowerCase()
        )
      );
      if (hasGroupPermission) {
        return true;
      }
    }

    // Users kontrolü (opsiyonel)
    if (editPerms.users && editPerms.users.length > 0) {
      const userId = authStore.userInfo?.sub || authStore.userInfo?.uid;
      if (userId && editPerms.users.includes(userId)) {
        return true;
      }
    }

    // Hiçbir yetki yoksa düzenleme yok
    return false;
  }

  return {
    canViewDashboard,
    canEditDashboard,
  };
}
