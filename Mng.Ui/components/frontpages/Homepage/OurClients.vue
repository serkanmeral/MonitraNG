<script setup lang="ts">
import { Features } from '@/_mockApis/front-pages/PagesData';
import { Icon } from '@iconify/vue';
</script>

<template>
    <div class="pt-96 pb-14 bg-surface">
        <v-container class="max-width-1218">
            <v-row>
                <v-col cols="12" lg="5">
                    <h2 class="display-1 textPrimary font-weight-semibold">Over 45,000 clients and counting.</h2>
                    <p class="text-textPrimary opacity-80 py-6 text-17 lh-32 pe-lg-4">
                        Pellentesque varius semper odio non pretium. Nullam sagittis neque orci, eu elementum enim.
                    </p>
                    <RouterLink
                        to=""
                        target="_blank"
                        class="text-15 text-hover-primary font-weight-semibold underline-link-6 d-flex justify-start"
                        >Request a Callback</RouterLink
                    >
                </v-col>
                <v-col cols="12" lg="7" class="pt-lg-0 pt-8">
                    <v-row>
                        <v-col cols="12" sm="6" v-for="item in Features" :key="item.icon">
                            <v-sheet class="px-lg-6 pb-6">
                                <div :class="'round-48 rounded-md d-flex justify-center align-center bg-' + item.bgcolor">
                                    <Icon :icon="item.icon" height="22" :class="'text-' + item.color" />
                                </div>
                                <h4 class="text-22 textPrimary font-weight-semibold py-5">{{ item.title }}</h4>
                                <p class="text-textPrimary opacity-80 text-15">{{ item.subtitle }}</p>
                            </v-sheet>
                        </v-col>
                    </v-row>
                </v-col>
            </v-row>
        </v-container>
    </div>
</template>
