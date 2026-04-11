<template>
    <input
        type="text" v-on:click="e => {e.preventDefault(); e.stopPropagation(); $emit('openModal', index, (orIndex ?? null));}"
        class="filter-select w-auto border-gray-300 text-gray-400" placeholder="Click to select" readonly
        :id="internalId" :value="modelValue">
</template>

<script lang="ts">
    import { defineComponent, PropType } from 'vue';

    export default defineComponent({
        emits: ['openModal'],
        props: {
            index: Number,
            orIndex: Number,
            modelValue: {
                type: String as PropType<string | null>,
                default: null,
            },
            internalId: String,
        },
    });
</script>
