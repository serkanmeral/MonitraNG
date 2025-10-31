<script setup lang="ts">
import { ref } from 'vue';
import { Icon } from '@iconify/vue';
import { QA1, QA2, QA3, QA4 } from '@/_mockApis/front-pages/PagesData';
const tab = ref(null);
const panel = ref([0]);
</script>
<template>
    <div class="space-p-96 bg-background">
        <v-container class="max-width-1218">
            <v-row>
                <v-col cols="12" class="pb-0">
                    <v-tabs v-model="tab" grow align-tabs="center" class="feature-tabs" color="primary" >
                        <v-tab value="1" class="text-16 font-weight-semibold textPrimary" 
                            ><Icon icon="material-symbols:groups-outline-rounded" height="24" class="me-3" />Team Scheduling</v-tab
                        >
                        <v-tab value="2" class="text-16 font-weight-semibold textPrimary"
                            ><Icon icon="material-symbols:account-balance-outline" height="24" class="me-3" />Payments</v-tab
                        >
                        <v-tab value="3" class="text-16 font-weight-semibold textPrimary"
                            ><Icon icon="material-symbols-light:photo-frame-outline-sharp" height="24" class="me-3" />Embedding</v-tab
                        >
                        <v-tab value="4" class="text-16 font-weight-semibold textPrimary"
                            ><Icon icon="material-symbols:widgets-outline-rounded" height="24" class="me-3" />Workflows</v-tab
                        >
                    </v-tabs>
                </v-col>
            </v-row>
        </v-container>
        <v-container class="max-width-1218">
            <v-row class="px-3">
                <div class="flex align-center">
                    <v-tabs-window v-model="tab">
                        <v-window-item value="1">
                            <v-row class="flex align-center">
                                <v-col cols="12" lg="6" md="12">
                                    <img src="/images/front-pages/background/feature-image.jpg" alt="image" class="w-100 rounded-md" />
                                </v-col>
                                <v-col cols="12" lg="6" md="12">
                                    <div class="ps-sm-10">
                                        <h2 class="display-2 mb-6 font-weight-semibold">Team Scheduling</h2>
                                        <v-expansion-panels elevation="0" v-model="panel" class="feature-tabs-expansion">
                                            <v-expansion-panel elevation="0" v-for="item in QA1" :key="item.question">
                                                <v-expansion-panel-title
                                                    class="text-17 font-weight-semibold textPrimary"
                                                    collapse-icon="mdi-minus"
                                                    expand-icon="mdi-plus"
                                                    >{{ item.question }}</v-expansion-panel-title
                                                >

                                                <v-expansion-panel-text class="text-16 pa-0 text-textPrimary opacity-80">
                                                    {{ item.answer }}
                                                </v-expansion-panel-text>
                                                <v-divider></v-divider>
                                            </v-expansion-panel>
                                        </v-expansion-panels>
                                        <v-btn color="primary" class="h-43 px-5 mt-6" flat href="" target="_blank">Learn More</v-btn>
                                    </div>
                                </v-col>
                            </v-row>
                        </v-window-item>
                        <v-window-item value="2">
                            <v-row class="flex align-center">
                                <v-col cols="12" lg="6" md="12">
                                    <img src="/images/front-pages/background/feature-image.jpg" alt="image" class="w-100 rounded-md" />
                                </v-col>
                                <v-col cols="12" lg="6" md="12">
                                    <div class="ps-sm-10">
                                        <h2 class="display-2 mb-6 font-weight-semibold">Payments</h2>
                                        <v-expansion-panels elevation="0" v-model="panel" class="feature-tabs-expansion">
                                            <v-expansion-panel elevation="0" v-for="item in QA2" :key="item.question">
                                                <v-expansion-panel-title
                                                    class="text-17 font-weight-semibold textPrimary"
                                                    collapse-icon="mdi-minus"
                                                    expand-icon="mdi-plus"
                                                    >{{ item.question }}</v-expansion-panel-title
                                                >

                                                <v-expansion-panel-text class="text-16 pa-0 text-textPrimary opacity-80">
                                                    {{ item.answer }}
                                                </v-expansion-panel-text>
                                                <v-divider></v-divider>
                                            </v-expansion-panel>
                                        </v-expansion-panels>
                                        <v-btn color="primary" class="h-43 px-5 mt-6" flat href="" target="_blank">Learn More</v-btn>
                                    </div>
                                </v-col>
                            </v-row>
                        </v-window-item>
                        <v-window-item value="3">
                            <v-row class="flex align-center">
                                <v-col cols="12" lg="6" md="12">
                                    <img src="/images/front-pages/background/feature-image.jpg" alt="image" class="w-100 rounded-md" />
                                </v-col>
                                <v-col cols="12" lg="6" md="12">
                                    <div class="ps-sm-10">
                                        <h2 class="display-2 mb-6 font-weight-semibold">Embedding</h2>
                                        <v-expansion-panels elevation="0" v-model="panel" class="feature-tabs-expansion">
                                            <v-expansion-panel elevation="0" v-for="item in QA3" :key="item.question">
                                                <v-expansion-panel-title
                                                    class="text-17 font-weight-semibold textPrimary"
                                                    collapse-icon="mdi-minus"
                                                    expand-icon="mdi-plus"
                                                    >{{ item.question }}</v-expansion-panel-title
                                                >

                                                <v-expansion-panel-text class="text-16 pa-0 text-textPrimary opacity-80">
                                                    {{ item.answer }}
                                                </v-expansion-panel-text>
                                                <v-divider></v-divider>
                                            </v-expansion-panel>
                                        </v-expansion-panels>
                                        <v-btn color="primary" class="h-43 px-5 mt-6" flat href="" target="_blank">Learn More</v-btn>
                                    </div>
                                </v-col>
                            </v-row>
                        </v-window-item>
                        <v-window-item value="4">
                            <v-row class="flex align-center">
                                <v-col cols="12" lg="6" md="12">
                                    <img src="/images/front-pages/background/feature-image.jpg" alt="image" class="w-100 rounded-md" />
                                </v-col>
                                <v-col cols="12" lg="6" md="12">
                                    <div class="ps-sm-10">
                                        <h2 class="display-2 mb-6 font-weight-semibold">Workflows</h2>
                                        <v-expansion-panels elevation="0" v-model="panel" class="feature-tabs-expansion">
                                            <v-expansion-panel elevation="0" v-for="item in QA4" :key="item.question">
                                                <v-expansion-panel-title
                                                    class="text-17 font-weight-semibold textPrimary"
                                                    collapse-icon="mdi-minus"
                                                    expand-icon="mdi-plus"
                                                    >{{ item.question }}</v-expansion-panel-title
                                                >

                                                <v-expansion-panel-text class="text-16 pa-0 text-textPrimary opacity-80">
                                                    {{ item.answer }}
                                                </v-expansion-panel-text>
                                                <v-divider></v-divider>
                                            </v-expansion-panel>
                                        </v-expansion-panels>
                                        <v-btn color="primary" class="h-43 px-5 mt-6" flat href="" target="_blank">Learn More</v-btn>
                                    </div>
                                </v-col>
                            </v-row>
                        </v-window-item>
                    </v-tabs-window>
                </div>
            </v-row>
        </v-container>
    </div>
</template>
