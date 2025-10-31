<script setup lang="ts">
import { onBeforeMount, ref, watch } from 'vue';
import { useCustomizerStore } from '@/stores/customizer';
import LogoAuth from '@/components/lc/Full/logo/AuthLogo.vue';
import Navigations from './Navigation.vue';

/*import tabler icons*/
import { Menu2Icon } from 'vue-tabler-icons';
const appsdrawer = ref(false);
const customizer = useCustomizerStore();
const stickyHeader = ref(false);
//For on Scroll Effect on Header
onBeforeMount(() => {
    window.addEventListener('scroll', handleScroll);
});
function handleScroll() {
    if (window.pageYOffset) {
        stickyHeader.value = true;
    } else {
        stickyHeader.value = false;
    }
}
</script>
<template>
    <div>
        <div class="bg-background">
            <!-- -----------------------------------------------
                    Start Header
                    ----------------------------------------------- -->
            <v-app-bar height="80" class="front-lp-header position-relative " flat :class="stickyHeader ? 'sticky-header py-0' : 'py-md-4'">
                <v-container class="py-0 max-width-1218">
                    <v-toolbar class="d-flex align-center">
                        <!-- Logo -->
                       
                        <div >
                            <LogoAuth />
                        </div>

                        <!-- Desktop view Navigation -->
                        <div class="navigation mx-auto d-lg-flex d-none">
                            <Navigations />
                        </div>
                        <div class="d-flex ms-xl-0 ms-auto">
                            <v-btn class="me-lg-0 me-3 d-md-flex d-none text-white px-6 transform-none custom-hover-primary bg-primary "   size="large" flat href="/auth/login"  target="_blank"
                                ><span class="text-white">Log in</span></v-btn
                            >
                            <v-btn variant="text" class="hidden-lg-and-up" icon @click.stop="appsdrawer = !appsdrawer">
                                <Menu2Icon size="22" stroke-width="1.5" />
                            </v-btn>
                        </div>
                    </v-toolbar>
                </v-container>
            </v-app-bar>
            <!-- -----------------------------------------------
                    End Header
                    ----------------------------------------------- -->
        </div>

        <!----sidebar menu drawer start----->
        <v-navigation-drawer class="lp-drawer" v-model="appsdrawer" location="left" temporary>
            <Navigations />
        </v-navigation-drawer>
    </div>
</template>

<style lang="scss" scoped>
.v-container {
    max-width: 1200px !important;
}
</style>
