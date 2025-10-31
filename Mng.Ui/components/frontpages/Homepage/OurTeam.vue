<script setup lang="ts">
import { Features, OurLeaders } from '@/_mockApis/front-pages/PagesData';
</script>

<template>
    <div class="space-p-96 front-dark bg-textPrimary">
        <v-container class="max-width-1218">
            <v-row class="pb-8">
                <v-col cols="12" lg="7">
                    <h2 class="display-2 font-weight-semibold text-white">Meet our team</h2>
                </v-col>
                <v-col cols="12" lg="5">
                    <p class="text-16 font-medium text-white opacity-50 text-md-end">
                        Our robust analytics offer rich insights into the <br />
                        information buyers want, informing where teams
                    </p>
                </v-col>
            </v-row>
            <v-row >
                <v-col cols="12" lg="3" sm="6" v-for="card in OurLeaders" :key="card.img">
                    <div class="position-relative rounded-16 team">
                        <img :src="card.img" alt="leader" class="w-100" />
                        <div class="bg-surface position-absolute py-4 px-3 text-center rounded-md intro">
                            <h5 class="text-h5 textPrimary">{{ card.name }}</h5>
                            <p class="text-14 text-textSecondary opacity-80">{{ card.position }}</p>
                        </div>
                    </div>
                </v-col>
            </v-row>
        </v-container>
    </div>
</template>
