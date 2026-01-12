import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import { useHubStore } from '@/stores/hub';
import type { menu } from '@/components/lc/Full/vertical-sidebar/sidebarItem';

/**
 * Side Menu Item Interface (API'den gelen format)
 */
export interface SideMenuItem {
  __dataId?: string;
  order: number;
  itemType: 'header' | 'item';
  pageType?: 'user' | 'manager' | 'admin';
  
  // Header için
  header?: string;
  
  // Item için
  title?: string;
  pageCode?: string; // i18n key için kullanılacak unique identifier
  icon?: string;
  iconType?: 'mdi' | 'tabler';
  to?: string;
  type?: 'internal' | 'external';
  
  // Hiyerarşi
  parentId?: string | null;
  level: number;
  
  // Chip/Badge
  chip?: string;
  chipBgColor?: string;
  chipColor?: string;
  chipVariant?: string;
  chipIcon?: string;
  
  // Diğer
  disabled?: boolean;
  subCaption?: string;
  
  // Yetkilendirme
  permissions?: {
    groups: {
      [groupName: string]: {
        view: boolean;
        create: boolean;
        update: boolean;
        delete: boolean;
        export: boolean;
      };
    };
  };
  
  // Children (frontend'de tree yapısı için)
  children?: SideMenuItem[];
}

/**
 * Menu Item Permissions Interface
 */
interface MenuItemPermissions {
  groups: {
    [groupName: string]: {
      view: boolean;
      create: boolean;
      update: boolean;
      delete: boolean;
      export: boolean;
    };
  };
}

interface SideMenuState {
  menuItems: SideMenuItem[];
  menuItemsTree: SideMenuItem[];
  loading: boolean;
  error: string | null;
  lastUpdated: number | null;
}

export const useSideMenuStore = defineStore('sideMenu', {
  state: (): SideMenuState => ({
    menuItems: [],
    menuItemsTree: [],
    loading: false,
    error: null,
    lastUpdated: null,
  }),

  getters: {
    /**
     * Kullanıcının görebileceği menu items (permission bazlı filtrelenmiş)
     */
    visibleMenuItems(state): SideMenuItem[] {
      const authStore = useAuthStore();
      
      // Admin kullanıcılar her şeyi görebilir
      if (authStore.isAdmin) {
        return state.menuItemsTree;
      }
      
      // Permission kontrolü ile filtrele
      const filtered = this.filterMenuItemsByPermission(state.menuItemsTree, authStore.userGroups);
      
      return filtered;
    },

    /**
     * Flat menu items (tree değil, tüm item'lar)
     */
    allMenuItems(state): SideMenuItem[] {
      return state.menuItems;
    },
  },

  actions: {
    /**
     * API'den menu items'ı yükle
     */
    async loadMenuItems(forceRefresh: boolean = false) {
      const authStore = useAuthStore();
      
      if (!authStore.userInfo || !authStore.domainName) {
        this.error = 'User info veya domain bilgisi yok';
        return;
      }

      // Cache kontrolü (localStorage)
      if (!forceRefresh) {
        try {
          const cacheKey = `menuItems_${authStore.userInfo.sub}_${authStore.domainName}`;
          const ttlKey = `menuItems_${authStore.userInfo.sub}_${authStore.domainName}_ttl`;
          
          const cachedData = localStorage.getItem(cacheKey);
          const ttlData = localStorage.getItem(ttlKey);
          
          if (cachedData && ttlData) {
            try {
              const { timestamp, ttl } = JSON.parse(ttlData);
              const now = Date.now();
              
              // TTL kontrolü (10 dakika)
              if (now - timestamp < (ttl || 600000)) {
                const items = JSON.parse(cachedData) as SideMenuItem[];
                this.menuItems = items;
                this.menuItemsTree = this.buildMenuTree(items);
                this.lastUpdated = timestamp;
                return;
              }
            } catch {
              // Cache parse hatası, devam et
            }
          }
        } catch {
          // Cache okuma hatası, devam et
        }
      }

      // API'den yükle
      this.loading = true;
      this.error = null;

      try {
        // API endpoint: fetchFromDataGateway handles baseUrl automatically
        // Full endpoint path: /api/v1/data/@side_menu
        const response = await fetchFromDataGateway(
          '/api/v1/data/@side_menu?skip=0&limit=10000&sort=order:asc,level:asc',
          'GET'
        );

        // Response format kontrolü (MngDataGateway List endpoint direkt array döndürüyor)
        let items: SideMenuItem[] = [];
        
        if (Array.isArray(response)) {
          // Direkt array response
          items = response as SideMenuItem[];
        } else if (response.data && Array.isArray(response.data)) {
          // Wrapped response: { data: [] }
          items = response.data;
        } else if (response.items && Array.isArray(response.items)) {
          // Alternative format: { items: [] }
          items = response.items;
        }
        
        if (!Array.isArray(items)) {
          throw new Error('Invalid response format: data is not an array');
        }

        this.menuItems = items as SideMenuItem[];
        this.menuItemsTree = this.buildMenuTree(this.menuItems);
        this.lastUpdated = Date.now();

        // Cache'e kaydet
        try {
          const cacheKey = `menuItems_${authStore.userInfo.sub}_${authStore.domainName}`;
          const ttlKey = `menuItems_${authStore.userInfo.sub}_${authStore.domainName}_ttl`;
          
          localStorage.setItem(cacheKey, JSON.stringify(this.menuItems));
          localStorage.setItem(ttlKey, JSON.stringify({
            timestamp: this.lastUpdated,
            ttl: 600000, // 10 dakika
          }));
        } catch {
          // Cache yazma hatası, devam et
        }
      } catch (error: any) {
        this.error = error.message || 'Menu items yüklenirken hata oluştu';
        
        // Fallback menu sadece environment variable ile etkinse kullanılır
        // Default: devre dışı (boş menu gösterilir)
        if (process.client) {
          const config = useRuntimeConfig();
          const enableFallbackMenu = config.public.enableFallbackMenu === true;
          
          if (enableFallbackMenu) {
            // Fallback: Hard-coded menu (compat klasöründen)
            this.loadFallbackMenu();
          } else {
            // Fallback devre dışı - boş menu
            this.menuItems = [];
            this.menuItemsTree = [];
          }
        } else {
          // SSR durumunda boş menu
          this.menuItems = [];
          this.menuItemsTree = [];
        }
      } finally {
        this.loading = false;
      }
    },

    /**
     * Cache'i temizle ve yeniden yükle
     */
    async refreshMenuItems() {
      const authStore = useAuthStore();
      
      if (authStore.userInfo && authStore.domainName) {
        try {
          const cacheKey = `menuItems_${authStore.userInfo.sub}_${authStore.domainName}`;
          const ttlKey = `menuItems_${authStore.userInfo.sub}_${authStore.domainName}_ttl`;
          
          localStorage.removeItem(cacheKey);
          localStorage.removeItem(ttlKey);
        } catch {
          // Cache temizleme hatası, devam et
        }
      }
      
      return this.loadMenuItems(true);
    },

    /**
     * Flat array'i tree yapısına çevir
     */
    buildMenuTree(items: SideMenuItem[]): SideMenuItem[] {
      // Önce parent-child ilişkilerini kur
      const itemMap = new Map<string, SideMenuItem>();
      const rootItems: SideMenuItem[] = [];

      // Tüm item'ları map'e ekle
      items.forEach(item => {
        if (item.__dataId) {
          itemMap.set(item.__dataId, { ...item, children: [] });
        }
      });

      // Parent-child ilişkilerini kur
      items.forEach(item => {
        if (!item.__dataId) return;

        const menuItem = itemMap.get(item.__dataId)!;
        
        if (item.parentId && itemMap.has(item.parentId)) {
          // Parent var, children array'ine ekle
          const parent = itemMap.get(item.parentId)!;
          if (!parent.children) {
            parent.children = [];
          }
          parent.children.push(menuItem);
        } else {
          // Root item
          rootItems.push(menuItem);
        }
      });

      // Children'ları order'a göre sırala
      const sortChildren = (items: SideMenuItem[]): SideMenuItem[] => {
        return items
          .sort((a, b) => a.order - b.order)
          .map(item => ({
            ...item,
            children: item.children ? sortChildren(item.children) : undefined,
          }));
      };

      return sortChildren(rootItems);
    },

    /**
     * Permission bazlı filtreleme
     */
    filterMenuItemsByPermission(
      items: SideMenuItem[],
      userGroups: string[]
    ): SideMenuItem[] {
      const authStore = useAuthStore();
      
      // Admin bypass
      if (authStore.isAdmin) {
        return items;
      }

      // Filter and sort items by order
      const filterAndSort = (items: SideMenuItem[]): SideMenuItem[] => {
        return items
          .filter(item => {
            // Page type kontrolü (ÖNCE page type kontrolü yap, sonra permission)
            const pageType = item.pageType || 'user'; // Default: user
            
            // Admin sayfaları sadece admin kullanıcılar görebilir
            if (pageType === 'admin' && !authStore.isAdmin) {
              return false;
            }

            // Manager sayfaları sadece manager veya admin kullanıcılar görebilir
            if (pageType === 'manager' && !authStore.isManager && !authStore.isAdmin) {
              return false;
            }

            // Permission kontrolü
            if (item.permissions?.groups && Object.keys(item.permissions.groups).length > 0) {
              // Permissions tanımlı - kullanıcının herhangi bir grubunda view yetkisi var mı?
              const hasViewPermission = userGroups.some(groupName => {
                const groupPermission = item.permissions?.groups[groupName];
                return groupPermission?.view === true;
              });

              if (!hasViewPermission) {
                // View yetkisi yok - gizle
                return false;
              }
            } else {
              // Permissions tanımlı değil veya boş
              // Güvenli varsayılan: Sadece user type item'lar gösterilir
              // Admin ve manager type item'lar permissions olmadan gösterilmez
              if (pageType === 'admin' || pageType === 'manager') {
                // Admin veya manager type ama permissions yok - gizle (güvenlik için)
                return false;
              }
            }

            return true;
          })
          .map(item => {
            // Children'ları recursive olarak filtrele ve sırala
            if (item.children && item.children.length > 0) {
              return {
                ...item,
                children: filterAndSort(item.children), // Recursive: filter and sort children
              };
            }
            return item;
          })
          .filter(item => {
            // Children'ı olmayan header'ları gizle
            if (item.itemType === 'header' && (!item.children || item.children.length === 0)) {
              return false;
            }
            return true;
          })
          .sort((a, b) => {
            // Order'a göre sırala (filtreleme sonrası sıralama korunmalı)
            return (a.order || 0) - (b.order || 0);
          });
      };

      return filterAndSort(items);
    },

    /**
     * Route path'ten menu item bul
     */
    getMenuItemByRoute(routePath: string): SideMenuItem | null {
      const findItem = (items: SideMenuItem[]): SideMenuItem | null => {
        for (const item of items) {
          if (item.to === routePath) {
            return item;
          }
          
          if (item.children) {
            const found = findItem(item.children);
            if (found) return found;
          }
        }
        return null;
      };

      return findItem(this.menuItemsTree);
    },

    /**
     * Fallback menu yükle (compat klasöründen)
     */
    loadFallbackMenu() {
      try {
        // TODO: compat/sidebarItem.fallback.ts dosyasından yükle
        // Şimdilik boş array, sonra implement edilecek
        this.menuItems = [];
        this.menuItemsTree = [];
      } catch (error) {
        this.menuItems = [];
        this.menuItemsTree = [];
      }
    },

    /**
     * Menu item'ı mevcut menu interface formatına çevir (mevcut sidebar için)
     */
    convertToMenuFormat(item: SideMenuItem): menu {
      const menuItem: menu = {
        header: item.header,
        title: item.title,
        pageCode: item.pageCode, // pageCode'u kopyala (i18n için gerekli)
        to: item.to,
        chip: item.chip,
        chipBgColor: item.chipBgColor,
        chipColor: item.chipColor,
        chipVariant: item.chipVariant,
        chipIcon: item.chipIcon,
        disabled: item.disabled,
        type: item.type,
        subCaption: item.subCaption,
      };

      // Icon handling: Support both old format (component) and new format (iconName + iconType)
      if (item.icon) {
        // New format: iconName + iconType (from database)
        if (item.iconType) {
          menuItem.icon = item.icon; // iconName as string (for backward compatibility)
          menuItem.iconType = item.iconType;
          menuItem.iconName = item.icon;
        } else {
          // Old format: assume Tabler icon
          // Keep as string - Icon component will handle the component lookup
          menuItem.icon = typeof item.icon === 'string' ? item.icon : item.icon;
          menuItem.iconName = typeof item.icon === 'string' ? item.icon : undefined;
          menuItem.iconType = 'tabler'; // Default to tabler
        }
      }

      // Children recursion
      if (item.children && item.children.length > 0) {
        menuItem.children = item.children.map(child => this.convertToMenuFormat(child));
      }

      return menuItem;
    },

    /**
     * SignalR Hub bağlantısını başlat (real-time menu updates için)
     * Uses shared Hub store connection with subscription pattern
     */
    async connectToHub() {
      // Client-side only
      if (process.server) return;

      const hubStore = useHubStore();
      const subscriptionId = 'side-menu';
      
      // Hub store'dan bağlantıyı başlat (eğer bağlı değilse)
      await hubStore.connectToHub();

      // Subscription zaten varsa, önce kaldır
      if (hubStore.hasSubscription(subscriptionId)) {
        hubStore.unsubscribe(subscriptionId);
      }

      // Filter: @side_menu dataset event'lerini filtrele
      const filter = (data: { routingKey: string; message: any; timestamp: string }) => {
        // Routing key formatları:
        // MngDataGateway: {domainName}.{eventType} (örn: "meral.datacreatedevent")
        // MngKeeper: {domainId}.{eventType} veya "domain.{domainName}.{eventType}"
        
        const routingKey = data.routingKey || '';
        const routingKeyLower = routingKey.toLowerCase();
        
        // 1. Dataset event type'larını kontrol et (sadece data event'leri)
        const isDatasetEvent = routingKeyLower.includes('datacreatedevent') || 
                               routingKeyLower.includes('dataupdatedevent') || 
                               routingKeyLower.includes('datadeletedevent') ||
                               routingKeyLower.includes('datarestoredevent');
        
        if (!isDatasetEvent) {
          return false;
        }
        
        // 2. Message içinde DatasetName kontrolü (en güvenilir yöntem)
        let datasetName: string | null = null;
        
        if (data.message && typeof data.message === 'object') {
          datasetName = data.message.DatasetName || 
                       data.message.datasetName || 
                       data.message.Dataset || 
                       data.message.dataset || 
                       null;
          
          if (datasetName && typeof datasetName !== 'string') {
            datasetName = String(datasetName);
          }
        }
        
        // 3. Sadece @side_menu dataset'i için true dön
        return datasetName?.toLowerCase() === '@side_menu';
      };

      // Handler: Menu'yu refresh et
      const handler = (data: { routingKey: string; message: any; timestamp: string }) => {
        // Debounce: Eğer çok hızlı ardışık event'ler gelirse sadece bir kez refresh yap
        const now = Date.now();
        const lastRefreshTime = (this as any).lastRefreshTime || 0;
        const refreshDebounceMs = 500; // 500ms debounce
        
        if (now - lastRefreshTime < refreshDebounceMs) {
          return;
        }
        
        (this as any).lastRefreshTime = now;
        
        // Refresh işlemini başlat
        this.refreshMenuItems();
      };
      
      // Subscription'ı ekle
      hubStore.subscribe(subscriptionId, { filter, handler });
    },

    /**
     * SignalR Hub bağlantısını kapat
     * Note: Hub store connection is shared, so we don't disconnect here
     * Subscription'ı kaldır
     */
    async disconnectFromHub() {
      const hubStore = useHubStore();
      const subscriptionId = 'side-menu';
      
      // Subscription'ı kaldır
      hubStore.unsubscribe(subscriptionId);
    },
  },
});
