<template>
    <div class="max-h-60 overflow-y-auto">
        <div class="relative flex-grow max-h-6/10" v-for="(_, index) in filterArray" :key="index">
            <!-- Separator -->
            <div v-if="index != 0" class="text-gray-400 mt-0_5rem">-- AND --</div>

            <div class="filter-container focus-within:z-10">
                <!-- Top level filter options -->
                <filter-select v-if="true" :internal-id="getIdHeader() + 'top-' + index"
                    :option-list="getTopLevelFilterOptions()" level="top" :index="index" :model-value="modVals.top[index]"
                    @change-filter-value="changeFilterValue" @handle-filter-change="handleFilterChange"></filter-select>
                <!-- Operator options -->
                <filter-select v-if="filterLevelIf(index, null, 'operator')" :internal-id="getIdHeader() + 'op-' + index"
                    :option-list="getFilterOpOptions(modVals.top[index])" level="operator" :index="index"
                    :model-value="modVals.operator[index]" @change-filter-value="changeFilterValue"
                    @handle-filter-change="handleFilterChange"></filter-select>
                <!-- Value options (Base) -->
                <filter-select v-if="filterLevelIf(index, null, 'value', 'base')"
                    :internal-id="getIdHeader() + 'value-' + index" :option-list="getFilterValueOptions(modVals.top[index])"
                    level="value" :index="index" :model-value="modVals.value[index]"
                    @change-filter-value="changeFilterValue" @handle-filter-change="handleFilterChange"></filter-select>
                <!-- Target Selectors - opens a custom modal -->
                <filter-modal-input v-if="filterLevelIf(index, null, 'value', 'target')" :index="index"
                    :model-value="modVals.dValue[index]" :internal-id="getIdHeader() + 'value-' + index"
                    @open-modal="openTargetFilterMenu"></filter-modal-input>
                <!-- Drop Selectors - opens a custom modal -->
                <filter-modal-input
                    v-if="filterLevelIf(index, null, 'value', 'drops')"
                    :index="index" :model-value="modVals.dValue[index]" :internal-id="getIdHeader() + 'value-' + index" 
                    @open-modal="openDropFilterMenu"
                ></filter-modal-input>
                <!-- Value options - Launch/Return Date Selectors - Min: 20 January 2021 - Max: Today-->
                <filter-date-input
                    v-if="filterLevelIf(index, null, 'value', 'date')"
                    :index="index" :model-value="modVals.value[index]" :internal-id="getIdHeader() + 'value-' + index" 
                    @change-filter-value="changeFilterValue" @handle-filter-change="handleFilterChange"
                ></filter-date-input>
                <!-- Or button -->
                <button
                    v-if="filterLevelIf(index, null, 'or')" :disabled="isOrDisabled(index)" title="Add alternate filter condition" 
                    :id="getIdHeader() + 'add-or-' + index" type="button" v-on:click="e => { e.preventDefault(); $emit('addOr', index)}"
                    class="mr-1rem flex items-center h-6 w-6 justify-center text-sml border text-yellow-700 border-yellow-700 bg-darkest rounded-md bg-transparent py-2 px-4 mt-0_5rem filter-button"
                >OR</button>
                <!-- Clear button -->
                <filter-clear-button v-if="filterLevelIf(index, null, 'clear')" :index="index" :internal-id="getIdHeader() + 'clear-' + index"
                    @remove-and-shift="removeAndShift"></filter-clear-button>
                <!-- Show a warning div if the filter is incomplete -->
                <span v-if="isFilterIncomplete(index, null)" class="filter-incomplete">(!) Incomplete, will not apply</span>
            </div>
            <div v-if="modVals.orCount[index] != null && modVals.orCount[index] > 0"
                class="ml-2rem filter-container focus-within:z-10"
                v-for="(orFilter, orIndex) in generateOrFiltersConditionsArr(index)">
                <!-- Separator -->
                <div class="text-gray-400 mt-0_5rem mr-1rem"><span class="text-gray text-lg">⮡ </span> OR</div>

                <!-- Top level filter options -->
                <filter-select v-if="true" :internal-id="getIdHeader() + 'top-' + index + '-' + orIndex"
                    :option-list="getTopLevelFilterOptions()" level="top" :index="index" :or-index="orIndex"
                    :model-value="modVals.orTop[index][orIndex]" @change-filter-value="changeFilterValue"
                    @handle-filter-change="handleOrFilterChange"></filter-select>

                <!-- Operator options -->
                <filter-select v-if="filterLevelIf(index, orIndex, 'operator')"
                    :internal-id="getIdHeader() + 'op-' + index + '-' + orIndex"
                    :option-list="getFilterOpOptions(modVals.orTop[index][orIndex])" level="operator" :index="index"
                    :or-index="orIndex" :model-value="modVals.orOperator[index][orIndex]"
                    @change-filter-value="changeFilterValue" @handle-filter-change="handleOrFilterChange"></filter-select>

                <!-- Value options -->
                <filter-select v-if="filterLevelIf(index, orIndex, 'value', 'base')"
                    :internal-id="getIdHeader() + 'value-' + index + '-' + orIndex"
                    :option-list="getFilterValueOptions(modVals.orTop[index][orIndex])" level="value" :index="index"
                    :or-index="orIndex" :model-value="modVals.orValue[index][orIndex]"
                    @change-filter-value="changeFilterValue" @handle-filter-change="handleOrFilterChange"></filter-select>

                <!-- Target Selectors - opens a custom modal -->
                <filter-modal-input v-if="filterLevelIf(index, orIndex, 'value', 'target')" :index="index"
                    :or-index="orIndex" :model-value="modVals.orDValue[index][orIndex]"
                    :internal-id="getIdHeader() + 'value-' + index + '-' + orIndex"
                    @open-modal="openTargetFilterMenu"></filter-modal-input>

                <!-- Drop Selectors - opens a custom modal -->
                <filter-modal-input v-if="!isLifetime && filterLevelIf(index, orIndex, 'value', 'drops')" :index="index"
                    :or-index="orIndex" :model-value="modVals.orDValue[index][orIndex]"
                    :internal-id="getIdHeader() + 'value-' + index + '-' + orIndex"
                    @open-modal="openDropFilterMenu"></filter-modal-input>

                <!-- Value options - Launch/Return Date Selectors - Min: 20 January 2021 - Max: Today-->
                <filter-date-input v-if="!isLifetime && filterLevelIf(index, orIndex, 'value', 'date')" :index="index"
                    :or-index="orIndex" :model-value="modVals.orValue[index][orIndex]"
                    :internal-id="getIdHeader() + 'value-' + index + '-' + orIndex" @change-filter-value="changeFilterValue"
                    @handle-filter-change="handleOrFilterChange"></filter-date-input>

                <!-- Clear button-->
                <filter-clear-button :index="index" :or-index="orIndex" :internal-id="getIdHeader() + 'clear-' + index + '-' + orIndex"
                    @remove-and-shift="removeOrAndShift"></filter-clear-button>

                <!-- Show a warning if the filter is incomplete -->
                <span v-if="isFilterIncomplete(index, orIndex)" class="filter-incomplete">(!) Incomplete, will not apply</span>
            </div>
        </div>
    </div>
</template>

<script>
    import FilterClearButton from './FilterClearButton.vue';
    import FilterSelect from './FilterSelect.vue';
    import FilterModalInput from './FilterModalInput.vue';
    import FilterDateInput from './FilterDateInput.vue';

    export default {
        components: {
            FilterClearButton,
            FilterSelect,
            FilterModalInput,
            FilterDateInput,
        },
        emits: [
            'changeFilterValue',
            'handleFilterChange',
            'handleOrFilterChange',
            'removeAndShift',
            'removeOrAndShift',
            'addOr',
            'openDropFilterMenu',
            'openTargetFilterMenu'
        ],
        props: {
            filterArray: Array,
            modVals: Object,
            isLifetime: Boolean,
            getFilterValueOptions: Function
        },
        methods: {
            artifactDisplayText(artifact) {
              const displayName = artifact.displayName;
              const level = artifact.level;
              let displayText = displayName;
              if(level != '%') displayText += " (T" + (level + ((displayName.toLowerCase().indexOf('stone') > -1 && displayName.toLowerCase().indexOf('fragment') == -1) ? 2 : 1)) + ")";
              return displayText;
            },
            dropPath(drop) {
              const addendum = (drop.protoName.indexOf('_STONE') > -1 ? 1 : 0);
              const fixedName = drop.protoName.replaceAll("_FRAGMENT", "").replaceAll("ORNATE_GUSSET", "GUSSET").replaceAll("VIAL_MARTIAN_DUST", "VIAL_OF_MARTIAN_DUST");
              return "artifacts/" + fixedName + "/" + fixedName + "_" + (drop.level + 1 + addendum) + ".png";
            },
            dropRarityPath(drop) {
              switch(drop.rarity){
                case 0: return "";
                case 1: return "images/rare.gif";
                case 2: return "images/epic.gif";
                case 3: return "images/legendary.gif";
                default: return "";
              }
            },
            getFilterValueOptions(topLevel) {
              switch(topLevel){
                case 'ship': return Array.from({ length: 11 }, (_, index) => ({
                    text: Array.of(
                        'Chicken One', 'Chicken Nine', 'Chicken Heavy',
                        'BCR', 'Quintillion Chicken', 'Cornish-Hen Corvette',
                        'Galeggtica', 'Defihent', 'Voyegger', 'Henerprise', 'Atreggies Henliner'
                      )[index],
                    value: index,
                  }));
                case 'duration': return Array.from({ length: 4 }, (_, index) => ({
                    text: Array.of('Short', 'Standard', 'Extended', 'Tutorial')[index],
                    value: index,
                    styleClass: 'text-duration-' + index
                  }));
                case 'level': return Array.from({ length: 9 }, (_, index) => ({
                    text: index + '★',
                    value: index
                  }));
                case 'target': return possibleTargets.value.map(target => ({
                    text: target.displayName,
                    value: target.id,
                    imagePath: target.imageString
                  }));
                case 'drops': {
                  const filteredConfigs = artifactConfigs.value.filter(a => a.baseQuality <= maxQuality.value).map(artifact => ({
                    text: this.artifactDisplayText(artifact),
                    value: artifact.name + "_" + artifact.level + "_" + artifact.rarity + "_" + artifact.baseQuality,
                    rarity: artifact.rarity,
                    imagePath: this.dropPath(artifact),
                    rarityGif: this.dropRarityPath(artifact),
                  }))
                  filteredConfigs.unshift({text: 'Any Legendary', value: '%_%_3_%', rarity: 3, styleClass: 'text-legendary', imagePath: 'icon_help.webp'});
                  filteredConfigs.unshift({text: 'Any Epic', value: '%_%_2_%', rarity: 2, styleClass: 'text-epic', imagePath: 'icon_help.webp'});
                  filteredConfigs.unshift({text: 'Any Rare', value: '%_%_1_%', rarity: 1, styleClass: 'text-rare', imagePath: 'icon_help.webp'});
                  return filteredConfigs;
                }
                case 'buggedcap':
                case 'dubcap': return [{ text: 'True', value: 'true' },{ text: 'False', value: 'false' }];
                default: return [];
              }
            },
            removeAndShift(index) {
                this.$emit('removeAndShift', index);
            },
            removeOrAndShift(index, orIndex) {
                this.$emit('removeOrAndShift', index, orIndex);
            },
            handleFilterChange(event) {
                this.$emit('handleFilterChange', event);
            },
            handleOrFilterChange(event) {
                this.$emit('handleOrFilterChange', event);
            },
            changeFilterValue(index, orIndex, level, value) {
                this.$emit('changeFilterValue', index, orIndex, level, value);
            },
            addOr(index) {
                this.$emit('addOr', index);
            },
            openDropFilterMenu(index, orIndex) {
                this.$emit('openDropFilterMenu', index, orIndex);
            },
            openTargetFilterMenu(index, orIndex) {
                this.$emit('openTargetFilterMenu', index, orIndex);
            },
            generateOrFiltersConditionsArr(index) {
                return new Array(this.modVals.orCount[index]);
            },
            getIdHeader(){
                return this.isLifetime ? 'lifetime-filter-' : 'filter-';
            },
            isFilterIncomplete(index, orIndex) {
                const isOr = orIndex != null;
                const topLevelRef = isOr ? this.modVals.orTop : this.modVals.top;
                const operatorRef = isOr ? this.modVals.orOperator : this.modVals.operator;
                const valueRef = isOr ? this.modVals.orValue : this.modVals.value;
                return (
                    isOr ? topLevelRef[index][orIndex] != null && (operatorRef[index][orIndex] == null || valueRef[index][orIndex] == null || ((topLevelRef[index][orIndex] == 'returnDT' || topLevelRef[index][orIndex] == 'launchDT') && valueRef[index][orIndex] == '')) :
                        topLevelRef[index] != null && (operatorRef[index] == null || valueRef[index] == null || ((topLevelRef[index] == 'returnDT' || topLevelRef[index] == 'launchDT') && valueRef[index] == ''))
                );
            },
            getTopLevelFilterOptions() {
                const commonOptions = [
                    { text: 'Ship', value: 'ship' },
                    { text: 'Duration', value: 'duration' },
                    { text: 'Level', value: 'level' },
                    { text: 'Target', value: 'target' },
                    { text: 'Double Cap', value: 'dubcap' },
                    { text: 'Bugged Cap', value: 'buggedcap'}
                ];
                if (this.isLifetime) return commonOptions.concat({ text: 'Drop Date', value: 'returnDT'});
                return commonOptions.concat({ text: 'Drops/Loot', value: 'drops' }, { text: 'Launch Date', value: 'launchDT' }, { text: 'Return Date', value: 'returnDT' });
            },
            getFilterOpOptions(topLevel) {
                switch (topLevel) {
                    case 'ship':
                    case 'duration':
                    case 'level': return [
                        { text: 'is', value: '=' },
                        { text: 'is not', value: '!=' },
                        { text: 'greater than', value: '>' },
                        { text: 'less than', value: '<' }
                    ];
                    case 'target': return [
                        { text: 'is', value: '=' },
                        { text: 'is not', value: '!=' }
                    ];
                    case 'drops': return [
                        { text: 'contains', value: 'c' },
                        { text: 'does not contain', value: 'dnc' }
                    ];
                    case 'launchDT':
                    case 'returnDT': return [
                        { text: 'on', value: 'd=' },
                        { text: 'before', value: '<' },
                        { text: 'after', value: '>' }
                    ];
                    case 'dubcap': 
                    case 'buggedcap': return [
                        { text: 'is', value: '=' },
                    ];
                }
            },
            filterLevelIf(index, orIndex, level, vtype) {
                const isOr = orIndex != null;
                const topLevelRef = isOr ? this.modVals.orTop : this.modVals.top;
                const operatorRef = isOr ? this.modVals.orOperator : this.modVals.operator;
                const valueRef = isOr ? this.modVals.orValue : this.modVals.value;

                switch (level) {
                    case 'top': return true;
                    case 'operator': return (isOr ? topLevelRef[index][orIndex] != null : topLevelRef[index] != null);
                    case 'value':
                        switch (vtype) {
                            case 'base':
                                return (
                                    isOr ? topLevelRef[index][orIndex] != null && operatorRef[index][orIndex] != null && this.isBaseFilter(topLevelRef[index][orIndex]) :
                                        topLevelRef[index] != null && operatorRef[index] != null && this.isBaseFilter(topLevelRef[index])
                                )
                            case 'drops':
                                return (
                                    isOr ? topLevelRef[index][orIndex] != null && operatorRef[index][orIndex] != null && topLevelRef[index][orIndex] == 'drops' :
                                        topLevelRef[index] != null && operatorRef[index] != null && topLevelRef[index] == 'drops'
                                )
                            case 'target':
                                return (
                                    isOr ? topLevelRef[index][orIndex] != null && operatorRef[index][orIndex] != null && topLevelRef[index][orIndex] == 'target' :
                                        topLevelRef[index] != null && operatorRef[index] != null && topLevelRef[index] == 'target'
                                )
                            case 'date':
                                return (
                                    isOr ? topLevelRef[index][orIndex] != null && operatorRef[index][orIndex] != null && (topLevelRef[index][orIndex] == 'launchDT' || topLevelRef[index][orIndex] == 'returnDT') :
                                        topLevelRef[index] != null && operatorRef[index] != null && (topLevelRef[index] == 'launchDT' || topLevelRef[index] == 'returnDT')
                                )
                            default: return true;
                        }
                    case 'or':
                        return (
                            topLevelRef[index] != null && operatorRef[index] != null && valueRef[index] != null && ((topLevelRef[index] != 'returnDT' && topLevelRef[index] != 'launchDT') || valueRef[index] != '')
                        )
                    case 'clear':
                        return isOr ? true : topLevelRef[index] != null;
                }
            },
            isBaseFilter(filterOp) {
                return(!['launchDT','returnDT','drops','target'].includes(filterOp))
            },
            isOrDisabled(index) {
                const topLevelRef = this.modVals.orTop;
                const operatorRef = this.modVals.orOperator;
                const valueRef = this.modVals.orValue;
                const orCountRef = this.modVals.orCount;
                return (
                    orCountRef[index] != null &&
                    orCountRef[index] != 0 &&
                    (
                        topLevelRef[index][orCountRef[index] - 1] == null ||
                        operatorRef[index][orCountRef[index] - 1] == null ||
                        valueRef[index][orCountRef[index] - 1] == null
                    )
                );
            }
        }
    }

</script>