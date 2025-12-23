<script setup lang="ts">
import { ref, onMounted } from "vue";
import { Form } from "vee-validate";
import { useAuthStore } from "@/stores/auth";
import { fetchFromMngKeeper } from "@/services/apiService";

const router = useRouter();
const authStore = useAuthStore();

const password = ref("");
const username = ref("");
const domain = ref("");
const showDomain = ref(false);
const domains = ref<Array<{ id: string; name: string; displayName: string }>>([]);
const errorMessage = ref("");
const isLoading = ref(false);

const passwordRules = ref([
  (v: string) => !!v || "Şifre gereklidir",
  (v: string) => (v && v.length >= 3) || "Şifre en az 3 karakter olmalıdır",
]);

const usernameRules = ref([
  (v: string) => !!v || "Kullanıcı adı gereklidir",
]);

// Load domains on mount
onMounted(async () => {
  try {
    const response = await fetchFromMngKeeper("/api/domain", "GET");
    if (response.domains && Array.isArray(response.domains)) {
      domains.value = response.domains;
      // If only one domain, auto-select it
      if (domains.value.length === 1) {
        domain.value = domains.value[0].name;
        showDomain.value = false;
      } else if (domains.value.length > 1) {
        showDomain.value = true;
      }
    }
  } catch (error) {
    console.error("Domain listesi yüklenemedi:", error);
    // Continue without domain selection if it fails
  }
});

async function validate() {
  errorMessage.value = "";
  isLoading.value = true;

  try {
    // Parse domain from username if format is "domain@username"
    let selectedDomain = domain.value;
    let selectedUsername = username.value;

    if (username.value.includes("@")) {
      const parts = username.value.split("@");
      if (parts.length === 2) {
        selectedDomain = parts[0];
        selectedUsername = parts[1];
      }
    }

    await authStore.login(selectedUsername, password.value, selectedDomain || undefined);
    
    // Redirect to dashboard
    router.push({ path: "/dashboards/analytical" });
  } catch (error) {
    if (error instanceof Error) {
      errorMessage.value = error.message;
    } else {
      errorMessage.value = "Giriş başarısız. Lütfen tekrar deneyin.";
    }
  } finally {
    isLoading.value = false;
  }
}

</script>

<template>
  <Form @submit="validate" v-slot="{ errors, isSubmitting }" class="mt-5">
    <!-- Domain Selection (if multiple domains available) -->
    <div v-if="showDomain && domains.length > 0" class="mb-4">
      <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText"
        >Domain</v-label
      >
      <VSelect
        v-model="domain"
        :items="domains"
        item-title="displayName"
        item-value="name"
        variant="outlined"
        density="comfortable"
        hide-details="auto"
        placeholder="Domain seçiniz"
      >
        <template v-slot:item="{ props, item }">
          <v-list-item v-bind="props" :title="item.raw.displayName" :subtitle="item.raw.name">
          </v-list-item>
        </template>
      </VSelect>
      <div class="text-caption text-medium-emphasis mt-2">
        Veya kullanıcı adınızı "domain@kullaniciadi" formatında girebilirsiniz
      </div>
    </div>

    <!-- Username Field -->
    <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText"
      >Kullanıcı Adı</v-label
    >
    <VTextField
      v-model="username"
      :rules="usernameRules"
      class="mb-4"
      required
      hide-details="auto"
      placeholder="Kullanıcı adı veya domain@kullaniciadi"
      variant="outlined"
      density="comfortable"
    ></VTextField>

    <!-- Password Field -->
    <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText"
      >Şifre</v-label
    >
    <VTextField
      v-model="password"
      :rules="passwordRules"
      required
      hide-details="auto"
      type="password"
      variant="outlined"
      density="comfortable"
      placeholder="Şifrenizi giriniz"
    ></VTextField>

    <!-- Error Message -->
    <div v-if="errorMessage" class="mt-4">
      <v-alert type="error" variant="tonal" density="compact">
        {{ errorMessage }}
      </v-alert>
    </div>

    <!-- Submit Button -->
    <v-btn
      size="large"
      :loading="isLoading || isSubmitting"
      color="primary"
      :disabled="!username || !password"
      block
      type="submit"
      class="mt-6"
      flat
    >
      Giriş Yap
    </v-btn>
  </Form>
</template>
