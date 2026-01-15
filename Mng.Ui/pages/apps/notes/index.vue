<script setup lang="ts">
import { computed } from 'vue';
// common components
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AppBaseCard from '@/components/shared/AppBaseCard.vue';
// component
import UserNotesListing from '@/components/apps/notes/UserNotesListing.vue';
import UserNotesContent from '@/components/apps/notes/UserNotesContent.vue';

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string) => {
  if (i18n && i18n.t) {
    return i18n.t(key);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key);
  }
  return key;
};

const page = computed(() => ({ 
  title: t('notes.pageTitle') || 'Notlarım' 
}));

const breadcrumbs = computed(() => [
  {
    text: t('notes.breadcrumb') || 'Notlarım',
    disabled: true,
    href: '#'
  }
]);
</script>

<template>
    <!-- ---------------------------------------------------- -->
    <!-- User Notes App -->
    <!-- ---------------------------------------------------- -->
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs"></BaseBreadcrumb>

    <v-card elevation="10" class="overflow-hidden">
        <AppBaseCard>
            <template v-slot:leftpart> 
                <UserNotesListing />
            </template>
            <template v-slot:rightpart>
                <UserNotesContent />
            </template>

            <template v-slot:mobileLeftContent>
                <UserNotesListing />
            </template>
        </AppBaseCard>
    </v-card>
</template>

<style scoped lang="scss">
@media (max-width: 1279px) {
    .v-card {
        position: unset;
    }
}
</style>