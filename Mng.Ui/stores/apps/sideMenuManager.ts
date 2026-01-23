import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import type { SideMenuItem } from './sideMenu';
import { useAuthStore } from '@/stores/auth';

interface SideMenuManagerState {
  menuItems: SideMenuItem[];
  menuItemsTree: SideMenuItem[];
  selectedItem: SideMenuItem | null;
  loading: boolean;
  error: string | null;
  searchQuery: string;
}

export const useSideMenuManagerStore = defineStore('sideMenuManager', {
  state: (): SideMenuManagerState => ({
    menuItems: [],
    menuItemsTree: [],
    selectedItem: null,
    loading: false,
    error: null,
    searchQuery: '',
  }),

  getters: {
    /**
     * Filtered menu items tree (by search query)
     */
    filteredMenuItemsTree(state): SideMenuItem[] {
      if (!state.searchQuery.trim()) {
        return state.menuItemsTree || [];
      }

      const query = state.searchQuery.toLowerCase().trim();
      const filtered = this.filterTreeItemsRecursive(state.menuItemsTree, query);
      
      return filtered;
    },
  },

  actions: {
    /**
     * Load all menu items from API
     */
    async loadMenuItems() {
      this.loading = true;
      this.error = null;

      try {
        const response = await fetchFromDataGateway('/api/v1/data/@side_menu?limit=10000');

        // Console log: Backend'den gelen raw response
        console.log('[SideMenuManager] Backend Response:', response);

        // Response format: { items: [...], totalCount: number } or direct array
        let loadedItems: any[] = [];
        
        if (response && Array.isArray(response)) {
          loadedItems = response;
        } else if (response && response.items && Array.isArray(response.items)) {
          loadedItems = response.items;
        } else if (response && response.data && Array.isArray(response.data)) {
          loadedItems = response.data;
        } else {
          throw new Error('Invalid response format');
        }

        // Console log: Parsed items array
        console.log('[SideMenuManager] Loaded Items (Raw):', loadedItems);
        console.log('[SideMenuManager] Loaded Items Count:', loadedItems.length);

        // Map items to SideMenuItem format (handle different field name cases)
        this.menuItems = loadedItems.map((item: any) => ({
          __dataId: item.__dataId ?? item.DataId ?? item.dataId,
          dataId: item.__dataId ?? item.DataId ?? item.dataId,
          itemType: item.itemType ?? item.ItemType ?? 'item',
          pageType: item.pageType ?? item.PageType ?? 'user',
          header: item.header ?? item.Header,
          title: item.title ?? item.Title,
          pageCode: item.pageCode ?? item.PageCode,
          icon: item.icon ?? item.Icon,
          iconType: item.iconType ?? item.IconType ?? 'tabler',
          iconName: item.iconName ?? item.IconName,
          to: item.to ?? item.To,
          type: item.type ?? item.Type ?? 'internal',
          order: item.order ?? item.Order ?? 0,
          level: item.level ?? item.Level ?? 0,
          parentId: item.parentId ?? item.ParentId ?? null,
          disabled: item.disabled ?? item.Disabled ?? false,
          subCaption: item.subCaption ?? item.SubCaption,
          chip: item.chip ?? item.Chip,
          chipBgColor: item.chipBgColor ?? item.ChipBgColor,
          chipColor: item.chipColor ?? item.ChipColor,
          chipVariant: item.chipVariant ?? item.ChipVariant,
          chipIcon: item.chipIcon ?? item.ChipIcon,
          permissions: item.permissions ?? item.Permissions,
        })) as SideMenuItem[];
        
        // Console log: Mapped items
        console.log('[SideMenuManager] Mapped Menu Items:', this.menuItems);
        console.log('[SideMenuManager] Mapped Items Count:', this.menuItems.length);
        
        // Console log: Specific items for debugging
        const orneklerItem = this.menuItems.find(item => item.header === 'Örnekler' || item.__dataId === '2d639fcb-4ae1-42c0-9615-871d2e5883b6');
        const girisItem = this.menuItems.find(item => item.header === 'Giriş' || item.__dataId === 'eecc4ad1-505c-41d9-970a-ba69d65d7c8f');
        console.log('[SideMenuManager] Örnekler Item:', orneklerItem);
        console.log('[SideMenuManager] Giriş Item:', girisItem);
        
        this.buildMenuTree();
        
        // Console log: Tree after build
        console.log('[SideMenuManager] Menu Items Tree:', this.menuItemsTree);
        console.log('[SideMenuManager] Tree Items Count:', this.menuItemsTree.length);
      } catch (error: any) {
        this.error = error.message || 'Menu items yüklenirken hata oluştu';
        this.menuItems = [];
        this.menuItemsTree = [];
      } finally {
        this.loading = false;
      }
    },

    /**
     * Build hierarchical tree structure from flat array
     * Sorting: Headers first (by order), then items under each header (by order)
     */
    buildMenuTree() {
      if (!this.menuItems || this.menuItems.length === 0) {
        this.menuItemsTree = [];
        return;
      }

      // First, sort by itemType (headers first) then by order
      const sortedItems = [...this.menuItems].sort((a, b) => {
        // Headers come first
        if (a.itemType === 'header' && b.itemType !== 'header') {
          return -1;
        }
        if (a.itemType !== 'header' && b.itemType === 'header') {
          return 1;
        }
        // Same type: sort by order
        return (a.order || 0) - (b.order || 0);
      });

      // Create a map for quick lookup
      const itemMap = new Map<string, SideMenuItem>();
      const rootItems: SideMenuItem[] = [];

      // First pass: create map and add children property
      sortedItems.forEach(item => {
        item.children = [];
        if (item.__dataId) {
          itemMap.set(item.__dataId, item);
        }
      });

      // Second pass: build tree
      sortedItems.forEach(item => {
        if (!item.parentId || !itemMap.has(item.parentId)) {
          // Root item
          rootItems.push(item);
          // Console log: Root items
          if (item.header === 'Örnekler' || item.header === 'Giriş' || item.__dataId === '2d639fcb-4ae1-42c0-9615-871d2e5883b6' || item.__dataId === 'eecc4ad1-505c-41d9-970a-ba69d65d7c8f') {
            console.log('[SideMenuManager] Root Item Added:', {
              header: item.header,
              __dataId: item.__dataId,
              parentId: item.parentId,
              itemType: item.itemType,
              pageType: item.pageType,
              disabled: item.disabled,
              permissions: item.permissions
            });
          }
        } else {
          // Child item
          const parent = itemMap.get(item.parentId);
          if (parent) {
            if (!parent.children) {
              parent.children = [];
            }
            parent.children.push(item);
          }
        }
      });
      
      // Console log: Root items after tree build
      console.log('[SideMenuManager] Root Items After Build:', rootItems.map(item => ({
        header: item.header,
        title: item.title,
        __dataId: item.__dataId,
        itemType: item.itemType,
        pageType: item.pageType,
        disabled: item.disabled,
        childrenCount: item.children?.length || 0
      })));

      // Third pass: Sort children of each item by order
      const sortChildren = (items: SideMenuItem[]) => {
        items.forEach(item => {
          if (item.children && item.children.length > 0) {
            // Sort children by order
            item.children.sort((a, b) => (a.order || 0) - (b.order || 0));
            // Recursively sort nested children
            sortChildren(item.children);
          }
        });
      };

      // Sort root items by order (headers first, then items)
      rootItems.sort((a, b) => {
        // Headers come first
        if (a.itemType === 'header' && b.itemType !== 'header') {
          return -1;
        }
        if (a.itemType !== 'header' && b.itemType === 'header') {
          return 1;
        }
        // Same type: sort by order
        return (a.order || 0) - (b.order || 0);
      });

      // Sort all children recursively
      sortChildren(rootItems);

      // Manager kullanıcılar için admin sayfalarını filtrele (Manager ve User sayfaları görünür)
      const authStore = useAuthStore();
      console.log('[SideMenuManager] Auth Info:', {
        isAdmin: authStore.isAdmin,
        isManager: authStore.isManager,
        userGroups: authStore.userGroups
      });
      
      if (!authStore.isAdmin && authStore.isManager) {
        // Recursive filter function to remove admin items only
        // Manager ve User sayfaları (ve pageType undefined/null olanlar) görünür
        const filterAdminItems = (items: SideMenuItem[]): SideMenuItem[] => {
          return items
            .filter(item => {
              // Sadece admin sayfalarını filtrele
              // Manager, User ve undefined/null olanlar görünür
              const shouldShow = item.pageType !== 'admin';
              if (item.header === 'Örnekler' || item.header === 'Giriş' || item.__dataId === '2d639fcb-4ae1-42c0-9615-871d2e5883b6' || item.__dataId === 'eecc4ad1-505c-41d9-970a-ba69d65d7c8f') {
                console.log('[SideMenuManager] Filter Check:', {
                  header: item.header,
                  __dataId: item.__dataId,
                  pageType: item.pageType,
                  shouldShow,
                  itemType: item.itemType
                });
              }
              return shouldShow;
            })
            .map(item => {
              if (item.children && item.children.length > 0) {
                return {
                  ...item,
                  children: filterAdminItems(item.children),
                };
              }
              return item;
            });
          // Not: Header'ların children kontrolü kaldırıldı - boş header'lar da görünmeli
        };
        
        this.menuItemsTree = filterAdminItems(rootItems);
        console.log('[SideMenuManager] Filtered Tree (Manager):', this.menuItemsTree.map(item => ({
          header: item.header,
          title: item.title,
          __dataId: item.__dataId,
          childrenCount: item.children?.length || 0
        })));
      } else {
        this.menuItemsTree = rootItems;
        console.log('[SideMenuManager] Tree (Admin/User):', this.menuItemsTree.map(item => ({
          header: item.header,
          title: item.title,
          __dataId: item.__dataId,
          childrenCount: item.children?.length || 0
        })));
      }
    },

    /**
     * Select a menu item
     */
    selectItem(item: SideMenuItem | null) {
      this.selectedItem = item;
    },

    /**
     * Create a new menu item
     */
    async createMenuItem(itemData: Partial<SideMenuItem>): Promise<SideMenuItem> {
      this.loading = true;
      this.error = null;

      try {
        // Clean itemData: Remove internal fields that shouldn't be sent to API
        const cleanData: any = { ...itemData };
        delete cleanData.__dataId; // MongoDB internal field
        delete cleanData.dataId; // Alias field (not in schema)
        delete cleanData.children; // Tree structure field (not in DB schema)
        delete cleanData._id; // MongoDB internal field
        
        // Ensure title is explicitly set (even if empty string, don't remove it)
        if (itemData.title !== undefined) {
          cleanData.title = itemData.title;
        }
        
        // Remove undefined fields (but keep empty strings, especially for title)
        Object.keys(cleanData).forEach((key) => {
          if (cleanData[key] === undefined) {
            delete cleanData[key];
          }
        });

        // Debug: Log the data being sent (only in development)
        if (import.meta.env.DEV && cleanData.title === undefined || cleanData.title === '') {
          console.warn('[SideMenuManager] createMenuItem: title is missing or empty', {
            itemData,
            cleanData,
          });
        }

        const response = await fetchFromDataGateway(
          '/api/v1/data/@side_menu',
          'POST',
          cleanData
        );

        if (response) {
          // Map response to SideMenuItem format
          const mappedItem: SideMenuItem = {
            __dataId: response.__dataId ?? response.DataId ?? response.dataId,
            dataId: response.__dataId ?? response.DataId ?? response.dataId,
            itemType: response.itemType ?? response.ItemType ?? 'item',
            pageType: response.pageType ?? response.PageType ?? 'user',
            header: response.header ?? response.Header,
            title: response.title ?? response.Title,
            pageCode: response.pageCode ?? response.PageCode,
            icon: response.icon ?? response.Icon,
            iconType: response.iconType ?? response.IconType ?? 'tabler',
            iconName: response.iconName ?? response.IconName,
            to: response.to ?? response.To,
            type: response.type ?? response.Type ?? 'internal',
            order: response.order ?? response.Order ?? 0,
            level: response.level ?? response.Level ?? 0,
            parentId: response.parentId ?? response.ParentId ?? null,
            disabled: response.disabled ?? response.Disabled ?? false,
            subCaption: response.subCaption ?? response.SubCaption,
            chip: response.chip ?? response.Chip,
            chipBgColor: response.chipBgColor ?? response.ChipBgColor,
            chipColor: response.chipColor ?? response.ChipColor,
            chipVariant: response.chipVariant ?? response.ChipVariant,
            chipIcon: response.chipIcon ?? response.ChipIcon,
            permissions: response.permissions ?? response.Permissions,
          };
          
          // Reload menu items to get updated tree
          await this.loadMenuItems();
          return mappedItem;
        }

        throw new Error('Create işlemi başarısız');
      } catch (error: any) {
        // Error handled by store error state
        this.error = error.message || 'Menu item oluşturulurken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Update a menu item
     */
    async updateMenuItem(itemId: string, itemData: Partial<SideMenuItem>): Promise<SideMenuItem> {
      this.loading = true;
      this.error = null;

      try {
        // Clean itemData: Remove internal fields that shouldn't be sent to API
        const cleanData: any = { ...itemData };
        delete cleanData.__dataId; // MongoDB internal field
        delete cleanData.dataId; // Alias field (not in schema, use __dataId in URL)
        delete cleanData.children; // Tree structure field (not in DB schema)
        delete cleanData._id; // MongoDB internal field
        
        // Remove undefined fields
        Object.keys(cleanData).forEach((key) => {
          if (cleanData[key] === undefined) {
            delete cleanData[key];
          }
        });

        const response = await fetchFromDataGateway(
          `/api/v1/data/@side_menu/${itemId}`,
          'PUT',
          cleanData
        );

        if (response) {
          // Map response to SideMenuItem format
          const mappedItem: SideMenuItem = {
            __dataId: response.__dataId ?? response.DataId ?? response.dataId,
            dataId: response.__dataId ?? response.DataId ?? response.dataId,
            itemType: response.itemType ?? response.ItemType ?? 'item',
            pageType: response.pageType ?? response.PageType ?? 'user',
            header: response.header ?? response.Header,
            title: response.title ?? response.Title,
            pageCode: response.pageCode ?? response.PageCode,
            icon: response.icon ?? response.Icon,
            iconType: response.iconType ?? response.IconType ?? 'tabler',
            iconName: response.iconName ?? response.IconName,
            to: response.to ?? response.To,
            type: response.type ?? response.Type ?? 'internal',
            order: response.order ?? response.Order ?? 0,
            level: response.level ?? response.Level ?? 0,
            parentId: response.parentId ?? response.ParentId ?? null,
            disabled: response.disabled ?? response.Disabled ?? false,
            subCaption: response.subCaption ?? response.SubCaption,
            chip: response.chip ?? response.Chip,
            chipBgColor: response.chipBgColor ?? response.ChipBgColor,
            chipColor: response.chipColor ?? response.ChipColor,
            chipVariant: response.chipVariant ?? response.ChipVariant,
            chipIcon: response.chipIcon ?? response.ChipIcon,
            permissions: response.permissions ?? response.Permissions,
          };
          
          // Reload menu items to get updated tree
          await this.loadMenuItems();
          
          // Update selected item if it's the updated one
          if (this.selectedItem?.__dataId === itemId) {
            this.selectedItem = mappedItem;
          }
          
          return mappedItem;
        }

        throw new Error('Update işlemi başarısız');
      } catch (error: any) {
        // Error handled by store error state
        this.error = error.message || 'Menu item güncellenirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Delete a menu item
     */
    async deleteMenuItem(itemId: string): Promise<void> {
      this.loading = true;
      this.error = null;

      try {
        await fetchFromDataGateway(
          `/api/v1/data/@side_menu/${itemId}`,
          'DELETE'
        );

        // Deselect if deleted item was selected
        if (this.selectedItem?.__dataId === itemId) {
          this.selectedItem = null;
        }

        // Reload menu items to get updated tree
        await this.loadMenuItems();
      } catch (error: any) {
        // Error handled by store error state
        this.error = error.message || 'Menu item silinirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Update menu item order (for drag & drop)
     */
    async updateMenuItemOrder(itemId: string, newOrder: number, newParentId: string | null): Promise<void> {
      try {
        await this.updateMenuItem(itemId, {
          order: newOrder,
          parentId: newParentId,
          // Recalculate level based on parent
          level: newParentId ? this.calculateLevel(newParentId) + 1 : 0,
        });
      } catch (error) {
        // Error handled by store error state
        throw error;
      }
    },

    /**
     * Calculate level based on parent
     */
    calculateLevel(parentId: string | null): number {
      if (!parentId) return -1;

      const findItem = (items: SideMenuItem[]): SideMenuItem | null => {
        for (const item of items) {
          if (item.__dataId === parentId) {
            return item;
          }
          if (item.children) {
            const found = findItem(item.children);
            if (found) return found;
          }
        }
        return null;
      };

      const parent = findItem(this.menuItemsTree);
      return parent ? parent.level : -1;
    },

    /**
     * Filter tree items recursively by search query
     */
    filterTreeItemsRecursive(items: SideMenuItem[], query: string): SideMenuItem[] {
      const filtered: SideMenuItem[] = [];

      for (const item of items) {
        const matchesQuery = 
          item.title?.toLowerCase().includes(query) ||
          item.header?.toLowerCase().includes(query) ||
          item.to?.toLowerCase().includes(query) ||
          item.subCaption?.toLowerCase().includes(query) ||
          item.pageCode?.toLowerCase().includes(query);

        const filteredChildren = item.children 
          ? this.filterTreeItemsRecursive(item.children, query)
          : [];

        if (matchesQuery || filteredChildren.length > 0) {
          filtered.push({
            ...item,
            children: filteredChildren.length > 0 ? filteredChildren : item.children,
          });
        }
      }

      return filtered;
    },

    /**
     * Set search query
     */
    setSearchQuery(query: string) {
      this.searchQuery = query;
    },

    /**
     * Reset selected item
     */
    resetSelection() {
      this.selectedItem = null;
    },
  },
});
