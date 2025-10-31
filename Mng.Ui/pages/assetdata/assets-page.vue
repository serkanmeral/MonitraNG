
<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useEmailStore } from "@/stores/apps/email"; 
import { useAssetStore } from "@/stores/apps/asset";
import BaseBreadcrumb from "@/components/shared/BaseBreadcrumb.vue";



import { useDisplay } from "vuetify";
import AppAssetListCard from "~/components/apps/asset-list/AppAssetListCard.vue";
import AssetListCompose from "@/components/apps/asset-list/AssetListCompose.vue";
import AssetDetail from "~/components/apps/asset-list/AssetDetail.vue";

const emailStore = useEmailStore();
const assetStore = useAssetStore();

const page = ref({ title: "Asset List" });
const breadcrumbs = ref([
  { text: "Dashboard", disabled: false, href: "#" },
  { text: "Asset List", disabled: true, href: "#" },
]);

const selectedFilter = ref<string>("inbox"); // Set default to inbox

// Fetch emails when the component mounts
onMounted(async () => {
  await assetStore.fetchAssets();

  if (assetStore.assets.length > 0)
  {
    
    assetStore.selectAsset(assetStore.assets)
  }

});


const assetChange = (filterName: string) => {
  selectedFilter.value = filterName;
};

const isMobileDrawerOpen = ref(false);
const isMobileAssetCategory = ref(false);
const { xs, lgAndUp } = useDisplay();
</script>

<template>
  <BaseBreadcrumb
    :title="page.title"
    :breadcrumbs="breadcrumbs"
  ></BaseBreadcrumb>

  <v-card elevation="10">
    <AppAssetListCard>
      <template v-slot:assetCompose>
        <AssetListCompose  />
      </template>

      <template v-slot:assetDetail>
        <AssetDetail class="d-md-block d-none" />
      </template>

      <template v-slot:mobileLeftContent>
        <AssetListCompose />
      </template>
    </AppAssetListCard>
  </v-card>
   <!-- Drawer for Asset Detail mobile view  -->
  <v-navigation-drawer
    v-model="isMobileDrawerOpen"
    location="right"
    temporary
    width="350"
  >
    <v-card-text class="pa-6">
      <AssetDetail />
    </v-card-text>
  </v-navigation-drawer>

  <!-- Drawer for Asset Detail mobile view -->
  <v-navigation-drawer
    v-if="!lgAndUp"
    v-model="isMobileAssetCategory"
    location="left"
    temporary
    width="350"
  >
    <v-card-text class="pa-6">
      <AssetListCompose  />
    </v-card-text>
  </v-navigation-drawer>
</template>
