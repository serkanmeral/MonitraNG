<script setup lang="ts">
import { ref } from 'vue';
import { Packages } from '@/_mockApis/front-pages/PagesData';
import { CircleCheckIcon, CircleXIcon } from 'vue-tabler-icons';
</script>

<template>
    <div class="pt-96 bg-surface">
        <v-container class="max-width-1218 package py-0" id="pricing">
            <div class="pb-12 text-center">
               
                    <h2 class="display-1 font-weight-semibold textPrimary">Fair pricing for everyone.</h2>
                    <p class="text-textPrimary opacity-80 pt-4 text-17 lh-32">Our robust analytics offer rich insights into the information buyers want, informing where teams.</p>
               
            </div>
            <v-row class="d-flex justify-center">
                <v-col cols="12" lg="3" sm="6" v-for="card in Packages" :key="card.caption">
                    <v-card elevation="0" class="rounded-16 pa-6 border">
                        <div class="pb-4">
                            <div class="d-flex ga-2 align-center">
                                <h4 class="text-20 font-weight-semibold textPrimary mb-4" v-text="card.caption"></h4>
                                <v-chip v-if="card.tagtext" size="small" class="mt-sm-n4 font-weight-bold" rounded="md" color="primary"
                                    >Popular</v-chip
                                >
                            </div>
                            <p class="text-14 text-textPrimary opacity-80 ">{{ card.subtext }}</p>
                        </div>


                        <div class="d-flex align-center mt-3">
                            <h2 class="display-2 text-primary font-weight-semibold">${{ card.price }}</h2>
                            <span class="text-textPrimary opacity-80 mt-4 ms-2 text-16">/{{ card.period }}</span>
                        </div>
                        <v-list class="mb-0 pl-0 bg-transparent pt-5">
                            <v-list-item class="pa-0" v-for="desc in card.list" :key="desc.listtitle">
                                <v-list-item-title
                                    v-if="desc.status"
                                    :class="{ 'opacity-80': desc.disable }"
                                    class="text-14 d-flex align-center font-weight-medium"
                                >
                                    <CircleXIcon v-if="desc.icon" stroke-width="1.5" size="20" class="mr-2 text-error " />
                                    <span v-html="desc.listtitle"></span>
                                </v-list-item-title>
                                <v-list-item-title
                                    v-else
                                    :class="{ 'opacity-50': desc.disable }"
                                    class="text-14 d-flex align-center font-weight-medium"
                                >
                                    <CircleCheckIcon v-if="desc.icon" stroke-width="1.5" size="20" class="mr-2 text-primary" />
                                    <span v-html="desc.listtitle"></span>
                                </v-list-item-title>
                            </v-list-item>
                        </v-list>
                        <v-btn color="primary" size="large" class="mt-4 font-weight-medium"  href="card.url" target="_blank" flat block>{{
                            card.buttontext
                        }}</v-btn>
                    </v-card>
                </v-col>
            </v-row>
        </v-container>
    </div>
</template>
