<script setup lang="ts">
import { ref, watch } from "vue";
import { useCustomizerStore } from "@/stores/customizer";
import { useEcomStore } from "@/stores/apps/eCommerce";
// Icon Imports
import {
  GridDotsIcon,
  LanguageIcon,
  SearchIcon,
  Menu2Icon,
  BellRingingIcon,
  ShoppingCartIcon,
} from "vue-tabler-icons";
const customizer = useCustomizerStore();
const showSearch = ref(false);
const drawer = ref(false);
const appsdrawer = ref(false);
const priority = ref(customizer.setHorizontalLayout ? 0 : 0);
function searchbox() {
  showSearch.value = !showSearch.value;
}
watch(priority, (newPriority) => {
  // yes, console.log() is a side effect
  priority.value = newPriority;
});
// count items
const store = useEcomStore();
const getCart = computed(() => {
  return store.cart;
});
</script>

<template>
  <v-app-bar
    elevation="5"
    :priority="priority"
    height="64"
    color="primary"
    id="top"
  >
    <!---LOGO RTL/LTR--->
    <v-locale-provider v-if="customizer.setRTLLayout" rtl>
      <div class="d-sm-flex d-none">
        <LcFullLogoRtlLogo />
      </div>
      <div class="pr-2 pt-2 d-sm-none d-flex">
        <LcFullLogoIcon />
      </div>
    </v-locale-provider>

    <v-locale-provider v-else>
      <div class="d-sm-flex d-none">
        <LcFullLogo />
      </div>
      <div class="pr-2 pt-2 d-sm-none d-flex">
        <LcFullLogoIcon />
      </div>
    </v-locale-provider>

    <v-btn
      class="hidden-md-and-down "
      icon
      color="primary"
      variant="text"
      @click.stop="customizer.SET_MINI_SIDEBAR(!customizer.mini_sidebar)"
    >
      <Menu2Icon size="25" />
    </v-btn>
    <v-btn
      class="hidden-lg-and-up"
      icon
      variant="text"
      @click.stop="customizer.SET_SIDEBAR_DRAWER"
      size="small"
    >
      <Menu2Icon size="25" />
    </v-btn>

    <!-- ---------------------------------------------- -->
    <!-- Search part -->
    <!-- ---------------------------------------------- -->

    <LcFullVerticalHeaderSearchbar />

    <v-spacer />
    <!-- ---------------------------------------------- -->
    <!---right part -->
    <!-- ---------------------------------------------- -->
    <!-- ---------------------------------------------- -->
    <!-- translate -->
    <!-- ---------------------------------------------- -->
    <div>
      <LcFullVerticalHeaderThemeToggler />
    </div>
    <LcFullVerticalHeaderLanguageDD />

    <!-- ---------------------------------------------- -->
    <!-- ShoppingCart -->
    <!-- ---------------------------------------------- -->
    <v-btn icon variant="text" color="primary" to="/apps/ecommerce/checkout">
      <v-badge color="error" :content="getCart?.length">
        <v-icon size="22">mdi-cart</v-icon>
      </v-badge>
    </v-btn>

    <!-- ------------------------------------------------>
    <!-- Notification -->
    <!-- ------------------------------------------------>
    <LcFullVerticalHeaderNotificationDD />

    <!-- ---------------------------------------------- -->
    <!-- User Profile -->
    <!-- ---------------------------------------------- -->
    <div class="ms-1">
      <LcFullVerticalHeaderProfileDD />
    </div>
  </v-app-bar>
</template>
