<script setup lang="ts">
import { ref } from "vue";
import { useDisplay } from "vuetify";
const { xs, lgAndUp } = useDisplay();

const sDrawer = ref(false); // Sidebar drawer for mobile
const eDrawer = ref(false); // Asset details drawer
const selectedAsset = ref(null); // To store the selected asset details

// Method to select an asset and open the asset details drawer
const openAssetDetails = (asset:any) => {
  selectedAsset.value = asset; // Store the selected asset
  eDrawer.value = true; // Open the asset details drawer
};

</script>

<template>
  <div class="d-flex mainbox">
    <div class="compose pa-6 " v-if="lgAndUp">
      <slot name="assetCompose"></slot>
    </div>

    <div class="asset-details pa-6  d-md-block d-none">
      <slot name="assetDetail"></slot>
    </div>
  </div>

</template>

<style lang="scss">
.compose {
  max-width: 300px;
  width: 100%;
}
.asset-list {
  max-width: 340px;
  width: 100%;
}
.asset-details {
  width: 100%;
}

@media screen and (max-width: 991px) {
  .asset-list {
    max-width: 100%;
  }
}
@media screen and (min-width: 991px) {
  .compose,.asset-list{
    border-inline-end-width: thin !important;
    border-inline-end-style: solid !important;
    border-inline-end-color: rgba(var(--v-border-color), var(--v-border-opacity)) !important;
  }
}
</style>
