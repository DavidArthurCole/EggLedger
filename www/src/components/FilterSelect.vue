<template>
    <select class="filter-select border-gray-300 text-gray-400"
        v-on:change="handleFilterChange"
        :id="internalId"
        :value="modelValue"
    >
        <option
            v-for="option in optionList"
            :value="option.value"
            :key="String(option.value)"
            :class="'filter-select-option ' + (level == 'value' ? (option.styleClass ?? '') : '')"
        >
            {{ option.text }}
        </option>
    </select>
</template>

<script lang="ts">
    import { defineComponent, PropType } from 'vue';

    interface FilterOption {
        value: string | number;
        text: string;
        styleClass?: string;
    }

    export default defineComponent({
        emits: ['changeFilterValue', 'handleFilterChange'],
        props: {
            internalId: String,
            optionList: {
                type: Array as PropType<FilterOption[]>,
                default: () => [],
            },
            level: String,
            index: Number,
            orIndex: Number,
            modelValue: {
                type: String as PropType<string | null>,
                default: null,
            },
        },
        methods: {
            handleFilterChange(event: Event){
                this.$emit('changeFilterValue', this.index, this.orIndex, this.level, (event.target as HTMLSelectElement).value);
                this.$emit('handleFilterChange', event);
            },
        },
    });
</script>
