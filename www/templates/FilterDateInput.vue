<template>
    <input
        type="date" min="2021-01-20" :max="new Date().toISOString().split('T')[0]" :id="internalId"
        :value="modelValue" class="filter-select border-gray-300 text-gray-400"
        v-on:change="handleDateChange" required>
</template>

<script>
    export default {
        emits: ['changeFilterValue'],
        props: {
            internalId: String,
            index: Number,
            orIndex: Number,
            modelValue: String,
        },
        methods: {
            handleDateChange(event){
                this.$emit('changeFilterValue', this.index, this.orIndex, 'value', event.target.value);
                this.$emit('handleFilterChange', event)
            },
        }
    }
</script>