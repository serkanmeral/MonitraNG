<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { fetchFromMngKeeper } from '@/services/apiService';
import type { SideMenuItem } from '@/stores/apps/sideMenu';
import { useAuthStore } from '@/stores/auth';

// Note: In Event Messages, $t() is only used in template, not in script setup
// For script setup computed properties, we'll access $t via template
// permissionTypes will be computed in template using $t()

const props = defineProps<{
  permissions?: SideMenuItem['permissions'];
}>();

const emit = defineEmits<{
  'change': [permissions: SideMenuItem['permissions']];
}>();

// Permission types - labels will be translated in template using $t()
const permissionTypes = [
  { key: 'view', labelKey: 'side-menu-manager.permissions.labels.view', color: 'info' },
  { key: 'create', labelKey: 'side-menu-manager.permissions.labels.create', color: 'success' },
  { key: 'update', labelKey: 'side-menu-manager.permissions.labels.update', color: 'warning' },
  { key: 'delete', labelKey: 'side-menu-manager.permissions.labels.delete', color: 'error' },
  { key: 'export', labelKey: 'side-menu-manager.permissions.labels.export', color: 'primary' },
] as const;

// Auth Store
const authStore = useAuthStore();

// Groups list
const groups = ref<Array<{ id: string; name: string; isActive: boolean }>>([]);
const loadingGroups = ref(false);

// Filtered groups - Manager kullanıcılar "admins" grubunu göremez
const filteredGroups = computed(() => {
  if (authStore.isAdmin) {
    // Admin kullanıcılar tüm grupları görebilir
    return groups.value.filter(g => g.isActive);
  }
  // Manager kullanıcılar "admins" grubunu göremez
  return groups.value.filter(g => g.isActive && g.name.toLowerCase() !== 'admins');
});

// Load groups from MngKeeper
const loadGroups = async () => {
  loadingGroups.value = true;
  try {
    // Load all groups (we'll filter active ones on frontend)
    // Note: isActive filter might not work correctly, so we load all and filter client-side
    const response = await fetchFromMngKeeper('/group?page=1&pageSize=1000', 'GET');
    
    // Handle different response formats
    let loadedGroups: any[] = [];
    
    if (response && Array.isArray(response)) {
      // Direct array response
      loadedGroups = response;
    } else if (response && response.groups && Array.isArray(response.groups)) {
      // Response with groups property
      loadedGroups = response.groups;
    } else if (response && response.data && Array.isArray(response.data)) {
      // Response with data property (direct array)
      loadedGroups = response.data;
    } else if (response && response.data && response.data.groups && Array.isArray(response.data.groups)) {
      // Response with data.groups property
      loadedGroups = response.data.groups;
    } else if (response && response.Groups && Array.isArray(response.Groups)) {
      // Response with Groups property (capitalized)
      loadedGroups = response.Groups;
    } else if (response && response.items && Array.isArray(response.items)) {
      // Response with items property
      loadedGroups = response.items;
    } else {
      loadedGroups = [];
    }
    
    groups.value = loadedGroups;
    
    // Filter active groups (handle both camelCase and PascalCase)
    if (loadedGroups.length > 0) {
      groups.value = loadedGroups.filter((g: any) => {
        const isActive = g.isActive !== undefined ? g.isActive : (g.IsActive !== undefined ? g.IsActive : true);
        return isActive !== false;
      });
    } else {
      groups.value = [];
    }
  } catch (error: any) {
    groups.value = [];
  } finally {
    loadingGroups.value = false;
  }
};

// Load groups on mount
onMounted(() => {
  loadGroups();
});

// Local permissions state
const localPermissions = ref<SideMenuItem['permissions']>({
  groups: {},
});

// Initialize permissions from props
const initPermissions = () => {
  if (props.permissions && props.permissions.groups) {
    localPermissions.value = {
      groups: { ...props.permissions.groups },
    };
  } else {
    // Initialize with filtered groups having no permissions
    const initialGroups: { [key: string]: any } = {};
    filteredGroups.value.forEach((group) => {
      initialGroups[group.name] = {
        view: false,
        create: false,
        update: false,
        delete: false,
        export: false,
      };
    });
    localPermissions.value = { groups: initialGroups };
  }
};

// Watch props.permissions and filteredGroups to reinitialize
watch([() => props.permissions, filteredGroups], () => {
  initPermissions();
}, { immediate: true, deep: true });

// Get permission value
const getPermission = (groupName: string, permission: string): boolean => {
  return localPermissions.value.groups[groupName]?.[permission as keyof typeof localPermissions.value.groups[string]] || false;
};

// Set permission value
const setPermission = (groupName: string, permission: string, value: boolean) => {
  if (!localPermissions.value.groups[groupName]) {
    localPermissions.value.groups[groupName] = {
      view: false,
      create: false,
      update: false,
      delete: false,
      export: false,
    };
  }
  
  localPermissions.value.groups[groupName][permission as keyof typeof localPermissions.value.groups[string]] = value;
  
  // Emit change
  emit('change', { ...localPermissions.value });
};

// Bulk set permission for a group
const setGroupPermissions = (groupName: string, permission: string, value: boolean) => {
  setPermission(groupName, permission, value);
};

// Bulk set permission for all groups (filtered)
const setAllGroupsPermission = (permission: string, value: boolean) => {
  filteredGroups.value.forEach((group) => {
    setPermission(group.name, permission, value);
  });
};

// Bulk set all permissions for a group
const setAllPermissionsForGroup = (groupName: string, value: boolean) => {
  permissionTypes.forEach((perm) => {
    setPermission(groupName, perm.key, value);
  });
};
</script>

<template>
  <div class="permission-editor">
    <div v-if="loadingGroups" class="text-center pa-4">
      <v-progress-circular indeterminate color="primary"></v-progress-circular>
      <p class="text-caption mt-2">{{ $t('side-menu-manager.permissions.loading') }}</p>
    </div>

    <div v-else-if="groups.length === 0" class="text-center pa-4 text-medium-emphasis">
      <p>{{ $t('side-menu-manager.permissions.empty') }}</p>
      <v-btn variant="text" size="small" @click="loadGroups" class="mt-2">
        {{ $t('side-menu-manager.permissions.retry') }}
      </v-btn>
    </div>

    <div v-else-if="filteredGroups.length === 0" class="text-center pa-4 text-medium-emphasis">
      <p>{{ $t('side-menu-manager.permissions.emptyFiltered') }}</p>
    </div>

    <div v-else>
      <!-- Bulk Actions -->
      <div class="d-flex align-center gap-2 mb-4">
        <span class="text-caption text-medium-emphasis">{{ $t('side-menu-manager.permissions.bulkActions') }}</span>
        <v-btn-toggle density="compact" variant="outlined" divided>
          <v-btn size="x-small" @click="permissionTypes.forEach(p => setAllGroupsPermission(p.key, true))">
            {{ $t('side-menu-manager.permissions.openAll') }}
          </v-btn>
          <v-btn size="x-small" @click="permissionTypes.forEach(p => setAllGroupsPermission(p.key, false))">
            {{ $t('side-menu-manager.permissions.closeAll') }}
          </v-btn>
        </v-btn-toggle>
      </div>

      <!-- Permissions Table -->
      <div class="permissions-table-wrapper">
        <v-table density="compact" class="permissions-table">
          <thead>
            <tr>
              <th class="text-left" style="min-width: 200px;">{{ $t('side-menu-manager.permissions.group') }}</th>
              <th v-for="perm in permissionTypes" :key="perm.key" class="text-center" style="min-width: 100px;">
                {{ $t(perm.labelKey) }}
              </th>
              <th class="text-center" style="min-width: 120px;">{{ $t('side-menu-manager.permissions.actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="group in filteredGroups" :key="group.id">
              <td>
                <div class="d-flex align-center">
                  <v-chip size="small" variant="flat" color="primary">
                    {{ group.name }}
                  </v-chip>
                </div>
              </td>
              <td
                v-for="perm in permissionTypes"
                :key="perm.key"
                class="text-center"
              >
                <v-checkbox
                  :model-value="getPermission(group.name, perm.key)"
                  @update:model-value="setPermission(group.name, perm.key, $event)"
                  density="compact"
                  hide-details
                  :color="perm.color"
                ></v-checkbox>
              </td>
              <td class="text-center">
                <v-btn-toggle density="compact" variant="text" divided>
                  <v-btn size="x-small" @click="setAllPermissionsForGroup(group.name, true)">
                    {{ $t('side-menu-manager.permissions.all') }}
                  </v-btn>
                  <v-btn size="x-small" @click="setAllPermissionsForGroup(group.name, false)">
                    {{ $t('side-menu-manager.permissions.none') }}
                  </v-btn>
                </v-btn-toggle>
              </td>
            </tr>
          </tbody>
        </v-table>
      </div>

    </div>
  </div>
</template>

<style scoped>
.permission-editor {
  width: 100%;
}

.permissions-table-wrapper {
  max-height: 500px;
  overflow-y: auto;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 4px;
}

.permissions-table {
  width: 100%;
}

.permissions-table thead {
  position: sticky;
  top: 0;
  background-color: rgb(var(--v-theme-surface));
  z-index: 1;
}

.permissions-table thead th {
  background-color: rgb(var(--v-theme-surface));
  border-bottom: 2px solid rgba(var(--v-border-color), var(--v-border-opacity));
  padding: 12px 8px;
  font-weight: 600;
}

.permissions-table tbody tr:hover {
  background-color: rgba(var(--v-theme-primary), 0.04);
}

.permissions-table tbody td {
  padding: 8px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
