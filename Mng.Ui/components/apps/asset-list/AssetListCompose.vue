<script setup lang="ts">
import { ref, shallowRef } from 'vue';
import assetItem from './assetItem';

import { useAssetStore } from "@/stores/apps/asset";

const assetStore = useAssetStore();



const assetMenu = shallowRef(assetItem);


</script>

<template>
  <div>
    <perfect-scrollbar class="scrollnavbar">
        <v-list class="py-5 px-4 bg-muted" density="compact">
            <!---Menu Loop -->
            <template v-for="(item, i) in assetMenu">
                <!---Item Sub Header -->
                <AppsAssetListListNavigationItemNavGroup :item="item" v-if="item.header" :key="item.title" />
                <!---If Has Child -->
                <AppsAssetListListNavigationItemNavCollapse class="leftPadding" :item="item" :level="1" v-else-if="item.children" />
                <!---Single Item-->
                <AppsAssetListListNavigationItemNavItem :item="item" v-else class="leftPadding" />
                <!---End Single Item-->
            </template>
        </v-list>

        <v-list class="py-5 px-4 bg-muted" density="compact">
            <!---Menu Loop -->
            <template v-for="(item, i) in assetStore.assets">
                {{item.asset_name}}
            </template>
        </v-list>        
    </perfect-scrollbar>
  </div>
</template>
