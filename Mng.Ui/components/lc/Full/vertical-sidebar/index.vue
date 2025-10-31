<script setup lang="ts">
import { ref, shallowRef } from 'vue';
import { useCustomizerStore } from '@/stores/customizer';
import sidebarItems from './sidebarItem';
const customizer = useCustomizerStore();
const sidebarMenu = shallowRef(sidebarItems);
</script>

<template>
    <v-navigation-drawer
        left
        v-model="customizer.Sidebar_drawer"
        elevation="0"
        rail-width="75"
        app
        class="leftSidebar"
        :rail="customizer.mini_sidebar"
        expand-on-hover width="270"
    >
        <!-- ---------------------------------------------- -->
        <!---Navigation -->
        <!-- ---------------------------------------------- -->
        <perfect-scrollbar class="scrollnavbar">
            <div class="profile">
                <div class="profile-pic profile-pic py-7 px-3">
                    <v-avatar size="45">
                        <img src="/images/profile/user2.jpg" width="50" alt="Julia" />
                    </v-avatar>
                </div>
                <div class="profile-name d-flex align-center px-3">
                    <h5 class="text-white font-weight-medium">Julia Roberts</h5>
                    <div class="ml-auto profile-logout">
                        <v-btn variant="text" icon rounded="md" color="white" to="/auth/login">
                            <PowerIcon size="22"/>
                            <v-tooltip activator="parent" location="top">Logout</v-tooltip>
                        </v-btn>
                    </div>
                </div>
            </div>
            <v-list class="py-5 px-4 bg-muted" density="compact">
                <!---Menu Loop -->
                <template v-for="(item, i) in sidebarMenu">
                    <!---Item Sub Header -->
                    <LcFullVerticalSidebarNavGroup :item="item" v-if="item.header" :key="item.title" />
                    <!---If Has Child -->
                    <LcFullVerticalSidebarNavCollapse class="leftPadding" :item="item" :level="0" v-else-if="item.children" />
                    <!---Single Item-->
                    <LcFullVerticalSidebarNavItem :item="item" v-else class="leftPadding" />
                    <!---End Single Item-->
                </template>
            </v-list>
        </perfect-scrollbar>
    </v-navigation-drawer>
</template>
