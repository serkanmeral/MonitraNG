import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useSideMenuStore } from '@/stores/apps/sideMenu';
import type { SideMenuItem } from '@/stores/apps/sideMenu';

/**
 * Page Permissions Composable
 * Sayfa bazlı yetkilendirme kontrolü için composable
 */
export function usePagePermissions() {
  const route = useRoute();
  const authStore = useAuthStore();
  const menuStore = useSideMenuStore();

  /**
   * Route path'ten menu item bul
   */
  const menuItem = computed<SideMenuItem | null>(() => {
    const routePath = route.path;
    return menuStore.getMenuItemByRoute(routePath);
  });

  /**
   * Sayfa tipine göre erişim kontrolü
   */
  const canAccessPageType = computed<boolean>(() => {
    if (!menuItem.value) {
      // Menu item bulunamadı, varsayılan olarak erişilebilir
      return true;
    }

    const pageType = menuItem.value.pageType || 'user';

    // Admin tüm sayfa tiplerine erişebilir
    if (authStore.isAdmin) {
      return true;
    }

    // Sayfa tipi kontrolü
    switch (pageType) {
      case 'admin':
        // Sadece admin kullanıcılar erişebilir
        return authStore.isAdmin === true;

      case 'manager':
        // Manager veya admin kullanıcılar erişebilir
        return authStore.isManager === true || authStore.isAdmin === true;

      case 'user':
      default:
        // Tüm kullanıcılar erişebilir (permission kontrolü sonra yapılır)
        return true;
    }
  });

  /**
   * View permission kontrolü
   */
  const canView = computed<boolean>(() => {
    if (!menuItem.value) {
      return true; // Menu item yoksa varsayılan olarak erişilebilir
    }

    // Admin bypass
    if (authStore.isAdmin) {
      return true;
    }

    // Sayfa tipi kontrolü
    if (!canAccessPageType.value) {
      return false;
    }

    const pageType = menuItem.value.pageType || 'user';

    // Manager sayfaları: Manager kullanıcılar kısıtlamasız erişebilir (permission kontrolü yok)
    if (pageType === 'manager' && authStore.isManager) {
      return true;
    }

    // Permission kontrolü
    if (!menuItem.value.permissions || !menuItem.value.permissions.groups) {
      return true; // Permission yoksa herkes erişebilir (backward compatibility)
    }

    // Kullanıcının dahil olduğu gruplardan herhangi birinde view yetkisi var mı?
    return authStore.userGroups.some(groupName => {
      const groupPerms = menuItem.value!.permissions!.groups[groupName];
      return groupPerms?.view === true;
    });
  });

  /**
   * Create permission kontrolü
   */
  const canCreate = computed<boolean>(() => {
    if (!menuItem.value) {
      return false;
    }

    // Admin bypass
    if (authStore.isAdmin) {
      return true;
    }

    // Sayfa tipi kontrolü
    if (!canAccessPageType.value) {
      return false;
    }

    const pageType = menuItem.value.pageType || 'user';

    // Manager sayfaları: Manager kullanıcılar kısıtlamasız erişebilir (permission kontrolü yok)
    if (pageType === 'manager' && authStore.isManager) {
      return true;
    }

    // Permission kontrolü
    if (!menuItem.value.permissions || !menuItem.value.permissions.groups) {
      return false; // Permission yoksa varsayılan olarak false
    }

    // Kullanıcının dahil olduğu gruplardan herhangi birinde create yetkisi var mı?
    return authStore.userGroups.some(groupName => {
      const groupPerms = menuItem.value!.permissions!.groups[groupName];
      return groupPerms?.create === true;
    });
  });

  /**
   * Update permission kontrolü
   */
  const canUpdate = computed<boolean>(() => {
    if (!menuItem.value) {
      return false;
    }

    // Admin bypass
    if (authStore.isAdmin) {
      return true;
    }

    // Sayfa tipi kontrolü
    if (!canAccessPageType.value) {
      return false;
    }

    const pageType = menuItem.value.pageType || 'user';

    // Manager sayfaları: Manager kullanıcılar kısıtlamasız erişebilir (permission kontrolü yok)
    if (pageType === 'manager' && authStore.isManager) {
      return true;
    }

    // Permission kontrolü
    if (!menuItem.value.permissions || !menuItem.value.permissions.groups) {
      return false; // Permission yoksa varsayılan olarak false
    }

    // Kullanıcının dahil olduğu gruplardan herhangi birinde update yetkisi var mı?
    return authStore.userGroups.some(groupName => {
      const groupPerms = menuItem.value!.permissions!.groups[groupName];
      return groupPerms?.update === true;
    });
  });

  /**
   * Delete permission kontrolü
   */
  const canDelete = computed<boolean>(() => {
    if (!menuItem.value) {
      return false;
    }

    // Admin bypass
    if (authStore.isAdmin) {
      return true;
    }

    // Sayfa tipi kontrolü
    if (!canAccessPageType.value) {
      return false;
    }

    const pageType = menuItem.value.pageType || 'user';

    // Manager sayfaları: Manager kullanıcılar kısıtlamasız erişebilir (permission kontrolü yok)
    if (pageType === 'manager' && authStore.isManager) {
      return true;
    }

    // Permission kontrolü
    if (!menuItem.value.permissions || !menuItem.value.permissions.groups) {
      return false; // Permission yoksa varsayılan olarak false
    }

    // Kullanıcının dahil olduğu gruplardan herhangi birinde delete yetkisi var mı?
    return authStore.userGroups.some(groupName => {
      const groupPerms = menuItem.value!.permissions!.groups[groupName];
      return groupPerms?.delete === true;
    });
  });

  /**
   * Export permission kontrolü
   */
  const canExport = computed<boolean>(() => {
    if (!menuItem.value) {
      return false;
    }

    // Admin bypass
    if (authStore.isAdmin) {
      return true;
    }

    // Sayfa tipi kontrolü
    if (!canAccessPageType.value) {
      return false;
    }

    const pageType = menuItem.value.pageType || 'user';

    // Manager sayfaları: Manager kullanıcılar kısıtlamasız erişebilir (permission kontrolü yok)
    if (pageType === 'manager' && authStore.isManager) {
      return true;
    }

    // Permission kontrolü
    if (!menuItem.value.permissions || !menuItem.value.permissions.groups) {
      return false; // Permission yoksa varsayılan olarak false
    }

    // Kullanıcının dahil olduğu gruplardan herhangi birinde export yetkisi var mı?
    return authStore.userGroups.some(groupName => {
      const groupPerms = menuItem.value!.permissions!.groups[groupName];
      return groupPerms?.export === true;
    });
  });

  /**
   * Sayfa read-only durumu
   * Admin sayfaları ve Manager sayfaları UI'da read-only görünecek
   */
  const isPageReadOnly = computed<boolean>(() => {
    if (!menuItem.value) {
      return false;
    }

    const pageType = menuItem.value.pageType || 'user';

    // Admin sayfaları: sadece admin kullanıcılar editable, diğerleri göremez (zaten canView false)
    if (pageType === 'admin') {
      return !authStore.isAdmin;
    }

    // Manager sayfaları: sadece manager/admin editable, diğerleri göremez (zaten canView false)
    if (pageType === 'manager') {
      return !authStore.isManager && !authStore.isAdmin;
    }

    // User sayfaları: permission'a göre belirlenir
    // Eğer update/create yetkisi yoksa read-only
    return !canUpdate.value && !canCreate.value;
  });

  /**
   * Belirli bir permission kontrolü
   */
  const hasPermission = (permissionType: 'view' | 'create' | 'update' | 'delete' | 'export'): boolean => {
    if (!menuItem.value) {
      return permissionType === 'view'; // View için varsayılan true, diğerleri false
    }

    // Admin bypass
    if (authStore.isAdmin) {
      return true;
    }

    // Sayfa tipi kontrolü
    if (!canAccessPageType.value) {
      return false;
    }

    const pageType = menuItem.value.pageType || 'user';

    // Manager sayfaları: Manager kullanıcılar kısıtlamasız erişebilir (permission kontrolü yok)
    if (pageType === 'manager' && authStore.isManager) {
      return true;
    }

    // Permission kontrolü
    if (!menuItem.value.permissions || !menuItem.value.permissions.groups) {
      return permissionType === 'view'; // View için varsayılan true, diğerleri false
    }

    // Kullanıcının dahil olduğu gruplardan herhangi birinde belirtilen yetki var mı?
    return authStore.userGroups.some(groupName => {
      const groupPerms = menuItem.value!.permissions!.groups[groupName];
      return groupPerms?.[permissionType] === true;
    });
  };

  return {
    menuItem,
    canView,
    canCreate,
    canUpdate,
    canDelete,
    canExport,
    isPageReadOnly,
    canAccessPageType,
    hasPermission,
  };
}
