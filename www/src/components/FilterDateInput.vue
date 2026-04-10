<template>
    <input
        type="date" :id="internalId" :value="modelValue"
        class="filter-select border-gray-300 text-gray-400"
        v-on:change="handleDateChange" required>
</template>

<script lang="ts">
    import { defineComponent, PropType } from 'vue';

    export default defineComponent({
        emits: ['changeFilterValue', 'handleFilterChange'],
        props: {
            internalId: String,
            index: Number,
            orIndex: Number,
            modelValue: {
                type: String as PropType<string | null>,
                default: null,
            },
        },
        methods: {
            handleDateChange(event: Event){
                this.$emit('changeFilterValue', this.index, this.orIndex, 'value', (event.target as HTMLInputElement).value);
                this.$emit('handleFilterChange', event)
            },
        },
    });
</script>
