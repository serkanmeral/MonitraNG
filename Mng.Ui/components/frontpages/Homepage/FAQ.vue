<script setup lang="ts">
import { FAQData } from '@/_mockApis/front-pages/PagesData';
</script>
<template>
    <div class="space-p-96 lp-faq">
        <v-container class="max-width-800 py-0">
            <div class="text-center mb-8">
                <h3 class="display-2 font-weight-semibold textPrimary">Frequently asked questions</h3>
            </div>
            <v-expansion-panels elevation="0" class="mt-14 d-flex flex-column ga-4">
                <v-expansion-panel elevation="0" class="border" v-for="item in FAQData" :key="item.title">
                    <v-expansion-panel-title collapse-icon="mdi-minus" expand-icon="mdi-plus" class="text-18 font-weight-medium textPrimary py-5">{{
                        item.title
                    }}</v-expansion-panel-title>

                    <v-expansion-panel-text class="text-16 text-textPrimary opacity-80">
                        {{ item.subtitle }}
                    </v-expansion-panel-text>
                </v-expansion-panel>
            </v-expansion-panels>
            <div class="d-flex justify-center mt-14">
                <div class="pa-2  border border-dashed rounded-md font-weight-medium text-14  text-textPrimary opacity-80 font-weight-medium d-flex align-center">
                    Still have a question?
                    <a
                        
                        href="https://discord.com/invite/eMzE8F6Wqs"
                        target="_blank"
                        class="text-underline text-textPrimary opacity-80 hover-primary underline-btn mx-1"
                        >Ask on discord</a
                    >
                    or
                    <a
            
                         href="https://support.wrappixel.com/"
                        target="_blank"
                        class="text-textPrimary opacity-80 hover-primary mx-1 underline-btn"
                        >submit a ticket</a
                    >
                </div>
            </div>
        </v-container>
    </div>
</template>
