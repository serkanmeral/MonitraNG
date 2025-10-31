<script setup lang="ts">
import { AboutFeatures } from '@/_mockApis/front-pages/PagesData';
import { Icon } from '@iconify/vue';
</script>
<template>
    <div class="space-p-96">
        <v-container class="max-width-1218">
            <h2 class="display-1 textPrimary font-weight-semibold text-center pb-12">The hassle-free setup process</h2>
            <v-row>
                <v-col cols="12" lg="3" sm="6" v-for="item in AboutFeatures" :key="item.icon">
                    <v-sheet :class="'pa-6 rounded-md bg-' + item.bgcolor" >
                        <div class="round-48 rounded-md d-flex justify-center align-center bg-surface elevation-4">
                            <Icon :icon="item.icon" height="22" :class="'text-' + item.color" />
                        </div>
                        <h4 class="text-22 textPrimary font-weight-semibold py-5">{{ item.title }}</h4>
                        <p class="text-textPrimary opacity-80 text-15">{{ item.subtitle }}</p>
                    </v-sheet>
                </v-col>
            </v-row>
        </v-container>
    </div>
</template>
