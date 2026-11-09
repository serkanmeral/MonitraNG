<script setup lang="ts">
export interface AfListCardLine {
  key: string;
  label: string;
  value: string;
}

export interface AfListCardModel {
  id: string;
  title: string;
  initials: string;
  imageUrl: string | null;
  lines: AfListCardLine[];
  corner: string | null;
  raw: Record<string, unknown>;
}

defineProps<{
  items: AfListCardModel[];
  canUpdate: boolean;
  canDelete: boolean;
  editLabel: string;
  deleteLabel: string;
}>();

const emit = defineEmits<{
  edit: [item: Record<string, unknown>];
  delete: [item: Record<string, unknown>];
}>();
</script>

<template>
  <v-row class="af-list-cards">
    <v-col
      v-for="card in items"
      :key="card.id"
      cols="12"
      sm="6"
      lg="4"
    >
      <v-card
        border
        rounded="lg"
        class="af-list-card h-100 d-flex flex-column"
        :class="{ 'af-list-card--clickable': canUpdate }"
        @click="canUpdate && emit('edit', card.raw)"
      >
        <v-card-item>
          <template #prepend>
            <v-avatar v-if="card.imageUrl" size="48" rounded="lg">
              <v-img :src="card.imageUrl" cover />
            </v-avatar>
            <v-avatar v-else color="primary" variant="tonal" size="48">
              <span class="text-subtitle-2 font-weight-medium">{{ card.initials }}</span>
            </v-avatar>
          </template>
          <v-card-title class="af-list-card-title text-wrap">{{ card.title }}</v-card-title>
          <template v-if="card.corner" #append>
            <v-chip size="small" variant="tonal" color="primary">{{ card.corner }}</v-chip>
          </template>
        </v-card-item>
        <v-card-text v-if="card.lines.length" class="pt-0 flex-grow-1">
          <div
            v-for="line in card.lines"
            :key="line.key"
            class="d-flex justify-space-between align-start ga-3 mb-1"
          >
            <span class="text-caption text-medium-emphasis">{{ line.label }}</span>
            <span class="text-body-2 text-end">{{ line.value }}</span>
          </div>
        </v-card-text>
        <v-card-actions v-if="canUpdate || canDelete" class="pt-0">
          <v-spacer />
          <v-btn
            v-if="canUpdate"
            icon
            size="small"
            variant="text"
            :title="editLabel"
            @click.stop="emit('edit', card.raw)"
          >
            <v-icon icon="mdi-pencil" size="18" />
          </v-btn>
          <v-btn
            v-if="canDelete"
            icon
            size="small"
            variant="text"
            color="error"
            :title="deleteLabel"
            @click.stop="emit('delete', card.raw)"
          >
            <v-icon icon="mdi-trash-can-outline" size="18" />
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-col>
  </v-row>
</template>

<style scoped>
.af-list-card--clickable {
  cursor: pointer;
}

.af-list-card-title {
  white-space: normal;
  line-height: 1.3;
}
</style>
