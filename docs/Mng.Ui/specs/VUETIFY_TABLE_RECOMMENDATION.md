# Vuetify v-data-table Kullanım Önerisi

## ✅ Kesinlikle Önerilir!

**Vuetify 3.7.1** içinde `v-data-table` component'i kullanıcı/grup yönetimi için **mükemmel** bir seçimdir.

---

## 🎯 Neden v-data-table?

### 1. **Built-in Özellikler (Hazır Gelen)**

#### ✅ Pagination
- Otomatik pagination desteği
- `items-per-page` ile sayfa başına kayıt sayısı
- `v-model:page` ile sayfa kontrolü
- Custom pagination footer desteği

**Örnek:**
```vue
<v-data-table
  :items-per-page="10"
  :headers="headers"
  :items="users"
  v-model:page="currentPage"
/>
```

#### ✅ Sorting (Sıralama)
- Header'lara tıklayarak otomatik sıralama
- `sortable: true/false` ile kolon bazlı kontrol
- Çoklu sıralama desteği
- `sort-by` prop ile varsayılan sıralama

**Örnek:**
```vue
const headers = [
  { title: 'Kullanıcı Adı', key: 'username', sortable: true },
  { title: 'Email', key: 'email', sortable: true },
  { title: 'Oluşturulma', key: 'createdAt', sortable: true },
]
```

#### ✅ Filtering (Filtreleme)
- Built-in search desteği
- `:search` prop ile arama
- Custom filter fonksiyonları
- Kolon bazlı filtreleme

**Örnek:**
```vue
<v-text-field v-model="search" label="Ara" />
<v-data-table :search="search" :items="users" />
```

#### ✅ Selection (Seçim)
- `show-select` ile checkbox seçimi
- `v-model` ile seçili satırları yönetme
- Toplu işlemler için ideal

**Örnek:**
```vue
<v-data-table
  v-model="selectedUsers"
  show-select
  :items="users"
/>
```

#### ✅ Slots (Özelleştirme)
- `item.{key}` slot'ları ile kolon özelleştirme
- `header.{key}` slot'ları ile header özelleştirme
- `item.data-table-select` ile checkbox özelleştirme
- `item.actions` ile action butonları

**Örnek:**
```vue
<v-data-table :items="users">
  <template v-slot:item.status="{ value }">
    <v-chip :color="value === 'active' ? 'success' : 'error'">
      {{ value }}
    </v-chip>
  </template>
  <template v-slot:item.actions="{ item }">
    <v-btn @click="editUser(item)">Düzenle</v-btn>
    <v-btn @click="deleteUser(item)">Sil</v-btn>
  </template>
</v-data-table>
```

#### ✅ Loading States
- `loading` prop ile loading göstergesi
- `loading-text` ile özelleştirilebilir mesaj

**Örnek:**
```vue
<v-data-table
  :loading="isLoading"
  loading-text="Yükleniyor..."
  :items="users"
/>
```

#### ✅ Density (Yoğunluk)
- `density="compact"` - Sıkışık görünüm
- `density="comfortable"` - Rahat görünüm
- `density="default"` - Varsayılan

#### ✅ Expandable Rows
- `show-expand` ile genişletilebilir satırlar
- Detay görüntüleme için ideal

---

## 📊 v-table vs v-data-table Karşılaştırması

| Özellik | v-table | v-data-table |
|---------|---------|--------------|
| **Pagination** | ❌ Manuel | ✅ Built-in |
| **Sorting** | ❌ Manuel | ✅ Built-in |
| **Filtering** | ❌ Manuel | ✅ Built-in |
| **Selection** | ❌ Manuel | ✅ Built-in |
| **Loading** | ❌ Manuel | ✅ Built-in |
| **Slots** | ✅ Var | ✅ Var |
| **Özelleştirme** | ✅ Yüksek | ✅ Yüksek |
| **Kod Miktarı** | ⚠️ Çok | ✅ Az |
| **Performans** | ✅ İyi | ✅ İyi |

**Sonuç:** `v-data-table` çok daha az kod ile çok daha fazla özellik sunar!

---

## 💡 Kullanıcı/Grup Yönetimi için Örnek

### Kullanıcı Listesi Sayfası

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useUserStore } from '@/stores/apps/user';
import { fetchFromMngKeeper } from '@/services/apiService';

const userStore = useUserStore();
const search = ref('');
const selectedUsers = ref([]);
const loading = ref(false);
const page = ref(1);
const itemsPerPage = ref(10);

const headers = [
  { title: 'Kullanıcı Adı', key: 'username', sortable: true },
  { title: 'Email', key: 'email', sortable: true },
  { title: 'Ad Soyad', key: 'fullName', sortable: true },
  { title: 'Durum', key: 'isActive', sortable: false },
  { title: 'Gruplar', key: 'groups', sortable: false },
  { title: 'Oluşturulma', key: 'createdAt', sortable: true },
  { title: 'İşlemler', key: 'actions', sortable: false, align: 'end' },
];

onMounted(async () => {
  loading.value = true;
  try {
    await userStore.fetchUsers();
  } finally {
    loading.value = false;
  }
});

const editUser = (user: any) => {
  navigateTo(`/apps/users/edit/${user.id}`);
};

const deleteUser = async (user: any) => {
  if (confirm(`Kullanıcıyı silmek istediğinizden emin misiniz?`)) {
    await userStore.deleteUser(user.id);
  }
};
</script>

<template>
  <BaseBreadcrumb title="Kullanıcı Yönetimi" />
  
  <v-card elevation="10">
    <v-card-item>
      <div class="d-flex justify-space-between align-center mb-4">
        <v-text-field
          v-model="search"
          prepend-inner-icon="mdi-magnify"
          label="Kullanıcı Ara"
          variant="outlined"
          density="compact"
          hide-details
          style="max-width: 300px;"
        />
        <v-btn color="primary" to="/apps/users/create">
          <v-icon class="mr-2">mdi-account-plus</v-icon>
          Yeni Kullanıcı
        </v-btn>
      </div>

      <v-data-table
        v-model="selectedUsers"
        :headers="headers"
        :items="userStore.users"
        :search="search"
        :loading="loading"
        :items-per-page="itemsPerPage"
        v-model:page="page"
        show-select
        item-value="id"
        class="border rounded-md"
      >
        <!-- Status Column -->
        <template v-slot:item.isActive="{ value }">
          <v-chip :color="value ? 'success' : 'error'" size="small">
            {{ value ? 'Aktif' : 'Pasif' }}
          </v-chip>
        </template>

        <!-- Groups Column -->
        <template v-slot:item.groups="{ item }">
          <div class="d-flex ga-1">
            <v-chip
              v-for="group in item.groups"
              :key="group"
              size="small"
              color="primary"
              variant="outlined"
            >
              {{ group }}
            </v-chip>
          </div>
        </template>

        <!-- Created At Column -->
        <template v-slot:item.createdAt="{ value }">
          {{ new Date(value).toLocaleDateString('tr-TR') }}
        </template>

        <!-- Actions Column -->
        <template v-slot:item.actions="{ item }">
          <div class="d-flex ga-2">
            <v-btn
              icon
              size="small"
              variant="text"
              @click="editUser(item)"
            >
              <v-icon>mdi-pencil</v-icon>
              <v-tooltip activator="parent">Düzenle</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteUser(item)"
            >
              <v-icon>mdi-delete</v-icon>
              <v-tooltip activator="parent">Sil</v-tooltip>
            </v-btn>
          </div>
        </template>
      </v-data-table>
    </v-card-item>
  </v-card>
</template>
```

---

## 🚀 Avantajlar

### 1. **Hızlı Geliştirme**
- Pagination, sorting, filtering hazır
- Minimal kod ile maksimum özellik
- Template'te zaten örnekler var

### 2. **Tutarlı UI**
- Vuetify design system ile uyumlu
- Material Design standartları
- Responsive design

### 3. **Performans**
- Virtual scrolling desteği (büyük veri setleri için)
- Efficient rendering
- Lazy loading desteği

### 4. **Özelleştirilebilirlik**
- Slots ile tam kontrol
- CSS override ile styling
- Props ile davranış kontrolü

### 5. **TypeScript Desteği**
- Type-safe headers
- Type-safe items
- IDE autocomplete

---

## ⚠️ Dikkat Edilmesi Gerekenler

### 1. **Büyük Veri Setleri**
- 1000+ kayıt için server-side pagination önerilir
- `items-per-page` ile client-side pagination yapılabilir
- Virtual scrolling aktif edilebilir

### 2. **API Entegrasyonu**
- `v-data-table` client-side işlemler yapar
- Server-side pagination için `server-items-length` kullanılmalı
- `@update:options` event'i ile server'a istek gönderilmeli

**Server-side Pagination Örneği:**
```vue
<script setup>
const options = ref({
  page: 1,
  itemsPerPage: 10,
  sortBy: [{ key: 'username', order: 'asc' }],
});

const fetchUsers = async () => {
  loading.value = true;
  try {
    const params = {
      page: options.value.page,
      limit: options.value.itemsPerPage,
      sort: options.value.sortBy[0]?.key,
      order: options.value.sortBy[0]?.order,
    };
    const response = await fetchFromMngKeeper('/api/user', 'GET', null, params);
    users.value = response.data;
    totalItems.value = response.total;
  } finally {
    loading.value = false;
  }
};

watch(options, fetchUsers, { deep: true });
</script>

<template>
  <v-data-table
    v-model:options="options"
    :headers="headers"
    :items="users"
    :loading="loading"
    :server-items-length="totalItems"
    :items-per-page="options.itemsPerPage"
  />
</template>
```

### 3. **Custom Filtering**
- Basit arama için `:search` prop yeterli
- Kompleks filtreleme için custom filter function gerekli
- Server-side filtering önerilir (büyük veri setleri için)

---

## 📋 Önerilen Yapı

### Kullanıcı Listesi için

**Component Yapısı:**
```vue
<template>
  <BaseBreadcrumb />
  <v-card>
    <v-card-item>
      <!-- Search & Actions -->
      <div class="d-flex justify-space-between mb-4">
        <v-text-field v-model="search" />
        <v-btn to="/apps/users/create">Yeni Kullanıcı</v-btn>
      </div>

      <!-- Data Table -->
      <v-data-table
        :headers="headers"
        :items="users"
        :search="search"
        :loading="loading"
        :items-per-page="itemsPerPage"
        show-select
      >
        <!-- Custom Slots -->
        <template v-slot:item.status="{ value }">
          <v-chip>{{ value }}</v-chip>
        </template>
        <template v-slot:item.actions="{ item }">
          <v-btn @click="edit(item)">Edit</v-btn>
        </template>
      </v-data-table>
    </v-card-item>
  </v-card>
</template>
```

### Store Yapısı

```typescript
// stores/apps/user.ts
export const useUserStore = defineStore('user', {
  state: () => ({
    users: [],
    loading: false,
    error: null,
  }),
  actions: {
    async fetchUsers(params?: { page?: number; limit?: number; search?: string }) {
      this.loading = true;
      try {
        const queryParams = new URLSearchParams();
        if (params?.page) queryParams.append('page', params.page.toString());
        if (params?.limit) queryParams.append('limit', params.limit.toString());
        if (params?.search) queryParams.append('search', params.search);
        
        const response = await fetchFromMngKeeper(
          `/api/user?${queryParams.toString()}`,
          'GET'
        );
        this.users = response.data || response;
      } catch (error) {
        this.error = error;
      } finally {
        this.loading = false;
      }
    },
  },
});
```

---

## ✅ Sonuç

**Kesinlikle `v-data-table` kullanılmalı!**

**Nedenler:**
1. ✅ Built-in pagination, sorting, filtering
2. ✅ Minimal kod, maksimum özellik
3. ✅ Template'te zaten örnekler var
4. ✅ Vuetify design system ile uyumlu
5. ✅ TypeScript desteği
6. ✅ Özelleştirilebilir (slots)
7. ✅ Performanslı
8. ✅ Responsive

**Önerilen Yaklaşım:**
- Küçük veri setleri (< 100 kayıt): Client-side pagination
- Büyük veri setleri (> 100 kayıt): Server-side pagination
- `v-data-table` + Store pattern + API entegrasyonu

**Template'teki Örnekler:**
- `pages/datatables/Basic.vue` - Temel kullanım
- `pages/datatables/Pagination.vue` - Pagination
- `pages/datatables/Filtering.vue` - Filtreleme
- `pages/datatables/CrudTable.vue` - CRUD işlemleri
- `pages/datatables/Selectable.vue` - Seçim

Bu örnekler kullanıcı/grup yönetimi için mükemmel referans!

