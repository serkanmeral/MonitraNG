<script setup>
import Icon from "~/components/shared/Icon.vue";
const props = defineProps({ item: Object, level: Number });
</script>

<template>
  <!-- ---------------------------------------------- -->
  <!---Item Childern -->
  <!-- ---------------------------------------------- -->
  <v-list-group no-action>
    <!-- ---------------------------------------------- -->
    <!---Dropdown  -->
    <!-- ---------------------------------------------- -->
    <template v-slot:activator="{ props }">
      <v-list-item
        v-bind="props"
        :value="item.title"
        rounded
        class="mb-1"
       
      >
        <!---Icon  -->
        <template v-slot:prepend>
          <Icon :item="item.icon" :level="level" />
        </template>
        <!---Title  -->
        <v-list-item-title
          class="mr-auto"
        >{{ $t(item.title) }}</v-list-item-title>
        <!---If Caption-->
        <v-list-item-subtitle
          v-if="item.subCaption"
          class="text-caption mt-n1 hide-menu"
        >
          {{ item.subCaption }}
        </v-list-item-subtitle>

        <!---If any chip or label-->
        <template v-slot:append v-if="item.chip">
            <v-chip
                :color="item.chipColor"
                :class="'sidebarchip hide-menu bg-' + item.chipBgColor"
                :size="item.chipIcon ? 'small' : 'small'"
                :variant="item.chipVariant"
                :prepend-icon="item.chipIcon"
            >
                {{ item.chip }}
            </v-chip>
        </template>        


      </v-list-item>
    </template>
    <!-- ---------------------------------------------- -->
    <!---Sub Item-->
    <!-- ---------------------------------------------- -->
    <template
      v-for="(subitem, i) in item.children"
      :key="i"
      v-if="item.children"
    >
      <AppsAssetListListNavigationItemNavCollapse :item="subitem" v-if="subitem.children" :level="level + 1" />
      <AppsAssetListListNavigationItemNavItem :item="subitem" :level="level + 1" v-else></AppsAssetListListNavigationItemNavItem>
    </template>
  </v-list-group>

  <!-- ---------------------------------------------- -->
  <!---End Item Sub Header -->
  <!-- ---------------------------------------------- -->
</template>
