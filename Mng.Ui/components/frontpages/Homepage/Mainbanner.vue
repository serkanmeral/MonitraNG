<script setup lang="ts">
import { ref } from 'vue';
import icon1 from '/images/front-pages/technology/nuxt.svg';
import icon2 from '/images/front-pages/technology/Vuetify.svg';
import icon3 from '/images/front-pages/technology/Vite.svg';
import icon4 from '/images/front-pages/technology/Typescript.svg';
import icon5 from '/images/front-pages/technology/Pinia.svg';
import icon6 from '/images/front-pages/technology/Apexchart.svg';

const technology = ref([
    {
        img: icon1,
        name: 'Nuxt.js'
    },
    {
        img: icon2,
        name: 'Vuetify'
    },
    {
        img: icon3,
        name: 'Vite'
    },
    {
        img: icon4,
        name: 'Typescript'
    },
    {
        img: icon5,
        name: 'Pinia'
    },
    {
        img: icon6,
        name: 'ApexChart'
    }
]);
</script>
<template>
    <div class="overflow-hidden bg-background">
        <v-container class="max-width-1218 pt-md-10 pt-6 pb-xl-0 pb-10">
            <v-row class="d-flex justify-center align-center">
                <v-col cols="12" lg="6" class="text-align-start">
                    <h1 class="text-56 textPrimary font-weight-medium">
                        <b class="font-weight-semibold">A Feature-Packed Dashboard</b> Built For Developers' Needs.
                    </h1>
                    <div class="d-sm-flex align-center text-center justify-md-start justify-center pt-5 gap-3">
                        <div class="d-flex flex-row-reverse justify-sm-start justify-center ml-2">
                            <v-avatar class="ml-n2 avtar-border" size="40">
                                <img src="/images/profile/user-3.jpg" height="40" alt="usericon" />
                            </v-avatar>
                            <v-avatar class="ml-n2 avtar-border" size="40">
                                <img src="/images/profile/user-2.jpg" height="40" alt="usericon" />
                            </v-avatar>
                            <v-avatar class="ml-n2 avtar-border" size="40">
                                <img src="/images/profile/user-1.jpg" height="40" alt="usericon" />
                            </v-avatar>
                        </div>
                        <div class="d-sm-flex text-muted">
                            <h6 class="text-17 font-weight-medium mt-sm-0 mt-2">52,589+</h6>
                            <h6 class="text-17 font-weight-medium ml-2">developers & agencies using our templates</h6>
                        </div>
                    </div>
                    <div class="d-flex flex-wrap  ga-sm-6 ga-3 pt-9 pb-7 justify-md-start justify-center align-center technology">
                        <div v-for="(item, i) in technology" :key="i">
                            <div class="d-flex justify-center align-center round-56 rounded-md bg-surface elevation-1">
                                <img :src="item.img" alt="technology" height="30" width="30" />
                            </div>
                            <v-tooltip activator="parent" location="bottom">{{ item.name }}</v-tooltip>
                        </div>
                    </div>
                    <div class="d-flex justify-md-start justify-center">
                        <v-btn color="primary"  class="px-8 transform-none m-btn-full" size="large"  href="/auth/login" target="_blank"
                            >Log in</v-btn
                        >
                    </div>
                </v-col>
                <v-col cols="12" lg="6">
                    <div class="main-banner d-lg-block d-none">
                        <img src="/images/front-pages/background/main-banner.png" alt="banner-right-image" class="rtlImg" />
                    </div>
                </v-col>
            </v-row>
        </v-container>
    </div>
</template>
