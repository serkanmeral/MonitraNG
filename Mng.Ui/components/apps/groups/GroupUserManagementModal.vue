<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useGroupStore } from '@/stores/apps/group';
import { useUserStore, type User } from '@/stores/apps/user';
import { UserMinusIcon, UserPlusIcon } from 'vue-tabler-icons';

interface Props {
  groupId: string;
  groupName: string;
  isOpen: boolean;
}

interface Emits {
  (e: 'close'): void;
  (e: 'updated'): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

const groupStore = useGroupStore();
const userStore = useUserStore();

// State
const loading = ref(false);
const addingUsers = ref(false);
const removingUserId = ref<string | null>(null);
const errorMessage = ref<string>('');
const successMessage = ref<string>('');

// User lists
const allUsers = ref<User[]>([]);
const searchQuery = ref('');
const selectedUserIds = ref<string[]>([]);

// Computed: Mevcut grup üyeleri (user.groups içinde groupName varsa)
const currentMembers = computed(() => {
  return allUsers.value.filter(user => 
    user.groups && user.groups.includes(props.groupName)
  );
});

// Computed: Grupta olmayan kullanıcılar (ekleme için)
const availableUsers = computed(() => {
  const memberUsernames = new Set(currentMembers.value.map(m => m.username));
  let filtered = allUsers.value.filter(user => 
    !memberUsernames.has(user.username)
  );
  
  // Search filter
  if (searchQuery.value.trim()) {
    const query = searchQuery.value.toLowerCase().trim();
    filtered = filtered.filter(user => 
      user.username.toLowerCase().includes(query) ||
      user.email.toLowerCase().includes(query) ||
      `${user.firstName} ${user.lastName}`.toLowerCase().includes(query)
    );
  }
  
  return filtered;
});

// Computed: Mevcut üyeleri filtrele (search için)
const filteredCurrentMembers = computed(() => {
  if (!searchQuery.value.trim()) {
    return currentMembers.value;
  }
  
  const query = searchQuery.value.toLowerCase().trim();
  return currentMembers.value.filter(user => 
    user.username.toLowerCase().includes(query) ||
    user.email.toLowerCase().includes(query) ||
    `${user.firstName} ${user.lastName}`.toLowerCase().includes(query)
  );
});

// Load all users
const loadUsers = async () => {
  loading.value = true;
  errorMessage.value = '';
  
  try {
    // Tüm aktif kullanıcıları çek (sayfalama olmadan, hepsini al)
    await userStore.fetchUsers({
      page: 1,
      pageSize: 1000, // Büyük sayı ile tüm kullanıcıları al
      isActive: true
    });
    
    allUsers.value = [...userStore.users];
  } catch (error: any) {
    errorMessage.value = error.message || 'Kullanıcılar yüklenirken bir hata oluştu';
    console.error('Error loading users:', error);
  } finally {
    loading.value = false;
  }
};

// Watch modal açılışı
watch(() => props.isOpen, (newVal) => {
  if (newVal) {
    loadUsers();
    selectedUserIds.value = [];
    searchQuery.value = '';
    errorMessage.value = '';
    successMessage.value = '';
  }
});

// Add users to group (batch)
const addUsersToGroup = async () => {
  if (selectedUserIds.value.length === 0) {
    errorMessage.value = 'Lütfen en az bir kullanıcı seçin';
    return;
  }
  
  addingUsers.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  
  try {
    const promises = selectedUserIds.value.map(userId => 
      groupStore.addUserToGroup(props.groupId, userId)
    );
    
    await Promise.all(promises);
    
    successMessage.value = `${selectedUserIds.value.length} kullanıcı gruba başarıyla eklendi`;
    selectedUserIds.value = [];
    
    // Kullanıcı listesini yenile
    await loadUsers();
    
    // Parent'a updated event gönder
    emit('updated');
    
    // Başarı mesajını 3 saniye sonra temizle
    setTimeout(() => {
      successMessage.value = '';
    }, 3000);
  } catch (error: any) {
    errorMessage.value = error.message || 'Kullanıcılar gruba eklenirken bir hata oluştu';
    console.error('Error adding users to group:', error);
  } finally {
    addingUsers.value = false;
  }
};

// Remove user from group
const removeUserFromGroup = async (userId: string) => {
  removingUserId.value = userId;
  errorMessage.value = '';
  successMessage.value = '';
  
  try {
    await groupStore.removeUserFromGroup(props.groupId, userId);
    
    successMessage.value = 'Kullanıcı gruptan başarıyla çıkarıldı';
    
    // Kullanıcı listesini yenile
    await loadUsers();
    
    // Parent'a updated event gönder
    emit('updated');
    
    // Başarı mesajını 3 saniye sonra temizle
    setTimeout(() => {
      successMessage.value = '';
    }, 3000);
  } catch (error: any) {
    errorMessage.value = error.message || 'Kullanıcı gruptan çıkarılırken bir hata oluştu';
    console.error('Error removing user from group:', error);
  } finally {
    removingUserId.value = null;
  }
};

// Toggle user selection
const toggleUserSelection = (userId: string) => {
  const index = selectedUserIds.value.indexOf(userId);
  if (index > -1) {
    selectedUserIds.value.splice(index, 1);
  } else {
    selectedUserIds.value.push(userId);
  }
};

// Select all available users
const selectAll = () => {
  selectedUserIds.value = availableUsers.value.map(u => u.id);
};

// Deselect all
const deselectAll = () => {
  selectedUserIds.value = [];
};

// Close modal
const closeModal = () => {
  emit('close');
};
</script>

<template>
  <v-dialog :model-value="isOpen" max-width="1000px" persistent scrollable>
    <v-card>
      <v-card-title class="pa-4 bg-primary text-white d-flex justify-space-between align-center">
        <div>
          <span class="text-h6">Üye Yönetimi</span>
          <div class="text-caption mt-1">{{ groupName }}</div>
        </div>
        <v-btn icon variant="text" color="white" @click="closeModal" size="small">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>

      <v-card-text class="pa-0">
        <!-- Messages -->
        <v-alert
          v-if="errorMessage"
          type="error"
          variant="tonal"
          density="compact"
          class="ma-4 mb-0"
          closable
          @click:close="errorMessage = ''"
        >
          {{ errorMessage }}
        </v-alert>
        
        <v-alert
          v-if="successMessage"
          type="success"
          variant="tonal"
          density="compact"
          class="ma-4 mb-0"
        >
          {{ successMessage }}
        </v-alert>

        <v-divider />

        <!-- Loading State -->
        <div v-if="loading" class="text-center py-12">
          <v-progress-circular indeterminate color="primary" size="48" />
          <p class="text-subtitle-1 mt-4 text-medium-emphasis">Kullanıcılar yükleniyor...</p>
        </div>

        <!-- Content -->
        <div v-else class="pa-4">
          <!-- Search -->
          <v-text-field
            v-model="searchQuery"
            prepend-inner-icon="mdi-magnify"
            label="Kullanıcı Ara (Kullanıcı adı, Email, Ad Soyad)"
            variant="outlined"
            density="compact"
            clearable
            class="mb-4"
          />

          <v-row>
            <!-- Current Members -->
            <v-col cols="12" md="6">
              <v-card variant="outlined" class="h-100">
                <v-card-title class="bg-secondary text-white pa-3">
                  <div class="d-flex justify-space-between align-center w-100">
                    <span class="text-subtitle-1 font-weight-medium">
                      Mevcut Üyeler ({{ filteredCurrentMembers.length }})
                    </span>
                  </div>
                </v-card-title>
                
                <v-card-text class="pa-0">
                  <div v-if="filteredCurrentMembers.length === 0" class="text-center py-8 text-medium-emphasis">
                    <UserMinusIcon size="48" class="mb-2" />
                    <p class="text-subtitle-2">Üye bulunamadı</p>
                  </div>
                  
                  <v-list density="compact" v-else>
                    <v-list-item
                      v-for="member in filteredCurrentMembers"
                      :key="member.id"
                      :title="`${member.firstName} ${member.lastName}`"
                      :subtitle="`${member.username} • ${member.email}`"
                    >
                      <template v-slot:append>
                        <v-btn
                          icon
                          variant="text"
                          color="error"
                          size="small"
                          :loading="removingUserId === member.id"
                          @click="removeUserFromGroup(member.id)"
                        >
                          <UserMinusIcon size="20" />
                        </v-btn>
                      </template>
                    </v-list-item>
                  </v-list>
                </v-card-text>
              </v-card>
            </v-col>

            <!-- Available Users to Add -->
            <v-col cols="12" md="6">
              <v-card variant="outlined" class="h-100 d-flex flex-column">
                <v-card-title class="bg-info text-white pa-3">
                  <div class="d-flex justify-space-between align-center w-100">
                    <span class="text-subtitle-1 font-weight-medium">
                      Kullanıcı Ekle ({{ availableUsers.length }})
                    </span>
                  </div>
                </v-card-title>
                
                <!-- Action Bar: Butonları üste taşıdık -->
                <div v-if="availableUsers.length > 0" class="pa-3 bg-grey-lighten-5 border-b">
                  <div class="d-flex justify-space-between align-center mb-2">
                    <div class="d-flex ga-1">
                      <v-btn
                        size="small"
                        variant="outlined"
                        color="info"
                        @click="selectAll"
                        :disabled="availableUsers.length === 0"
                      >
                        Tümünü Seç
                      </v-btn>
                      <v-btn
                        size="small"
                        variant="outlined"
                        color="info"
                        @click="deselectAll"
                        :disabled="selectedUserIds.length === 0"
                      >
                        Seçimi Temizle
                      </v-btn>
                    </div>
                    <v-chip
                      v-if="selectedUserIds.length > 0"
                      color="primary"
                      size="small"
                      variant="flat"
                    >
                      {{ selectedUserIds.length }} seçili
                    </v-chip>
                  </div>
                  <v-btn
                    v-if="selectedUserIds.length > 0"
                    color="primary"
                    variant="flat"
                    size="small"
                    :loading="addingUsers"
                    @click="addUsersToGroup"
                    block
                  >
                    <UserPlusIcon class="mr-2" size="18" />
                    Seçilen {{ selectedUserIds.length }} Kullanıcıyı Ekle
                  </v-btn>
                </div>
                
                <v-card-text class="pa-0 flex-grow-1" style="overflow-y: auto;">
                  <div v-if="availableUsers.length === 0" class="text-center py-8 text-medium-emphasis">
                    <UserPlusIcon size="48" class="mb-2" />
                    <p class="text-subtitle-2">Eklenecek kullanıcı bulunamadı</p>
                    <p class="text-caption mt-2">Tüm kullanıcılar zaten bu grupta olabilir</p>
                  </div>
                  
                  <v-list density="compact" v-else>
                    <v-list-item
                      v-for="user in availableUsers"
                      :key="user.id"
                      :title="`${user.firstName} ${user.lastName}`"
                      :subtitle="`${user.username} • ${user.email}`"
                      @click="toggleUserSelection(user.id)"
                      :class="{ 'bg-primary-lighten-5': selectedUserIds.includes(user.id) }"
                    >
                      <template v-slot:prepend>
                        <v-checkbox
                          :model-value="selectedUserIds.includes(user.id)"
                          @click.stop="toggleUserSelection(user.id)"
                          hide-details
                          density="compact"
                        />
                      </template>
                    </v-list-item>
                  </v-list>
                </v-card-text>
              </v-card>
            </v-col>
          </v-row>
        </div>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn
          color="primary"
          variant="flat"
          @click="closeModal"
        >
          Kapat
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.bg-primary-lighten-5 {
  background-color: rgba(var(--v-theme-primary), 0.05);
}

.border-b {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>

