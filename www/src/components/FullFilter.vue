<template>
  <div class="max-h-60 overflow-y-auto">
    <div
      class="relative flex-grow max-h-6/10"
      v-for="(_, index) in filterArray"
      :key="index"
    >
      <!-- Separator -->
      <div v-if="index != 0" class="text-gray-400 mt-0_5rem">-- AND --</div>

      <div class="filter-container focus-within:z-10">
        <!-- Top level filter options -->
        <filter-select
          v-if="true"
          :internal-id="getIdHeader() + 'top-' + index"
          :option-list="getTopLevelFilterOptions()"
          level="top"
          :index="index"
          :model-value="modVals.top[index]"
          @change-filter-value="changeFilterValue"
          @handle-filter-change="handleFilterChange"
        ></filter-select>
        <!-- Operator options -->
        <filter-select
          v-if="filterLevelIf(index, null, 'operator')"
          :internal-id="getIdHeader() + 'op-' + index"
          :option-list="getFilterOpOptions(modVals.top[index])"
          level="operator"
          :index="index"
          :model-value="modVals.operator[index]"
          @change-filter-value="changeFilterValue"
          @handle-filter-change="handleFilterChange"
        ></filter-select>
        <!-- Value options (Base) -->
        <filter-select
          v-if="filterLevelIf(index, null, 'value', 'base')"
          :internal-id="getIdHeader() + 'value-' + index"
          :option-list="getFilterValueOptions(modVals.top[index])"
          level="value"
          :index="index"
          :model-value="modVals.value[index]"
          @change-filter-value="changeFilterValue"
          @handle-filter-change="handleFilterChange"
        ></filter-select>
        <!-- Target Selectors - opens a custom modal -->
        <filter-modal-input
          v-if="filterLevelIf(index, null, 'value', 'target')"
          :index="index"
          :model-value="modVals.dValue[index]"
          :internal-id="'filter-value-' + index + (isLifetime ? '-lifetime' : '')"
          @open-modal="openTargetFilterMenu"
        ></filter-modal-input>
        <!-- Drop Selectors - opens a custom modal -->
        <filter-modal-input
          v-if="filterLevelIf(index, null, 'value', 'drops')"
          :index="index"
          :model-value="modVals.dValue[index]"
          :internal-id="'filter-value-' + index + (isLifetime ? '-lifetime' : '')"
          @open-modal="openDropFilterMenu"
        ></filter-modal-input>
        <!-- Value options - Launch/Return Date Selectors - Min: 20 January 2021 - Max: Today-->
        <filter-date-input
          v-if="filterLevelIf(index, null, 'value', 'date')"
          :index="index"
          :model-value="modVals.value[index]"
          :internal-id="getIdHeader() + 'value-' + index"
          @change-filter-value="changeFilterValue"
          @handle-filter-change="handleFilterChange"
        ></filter-date-input>
        <!-- Or button -->
        <button
          v-if="filterLevelIf(index, null, 'or')"
          :disabled="isOrDisabled(index)"
          title="Add alternate filter condition"
          :id="getIdHeader() + 'add-or-' + index"
          type="button"
          v-on:click="
            (e) => {
              e.preventDefault();
              $emit('addOr', index);
            }
          "
          class="mr-1rem flex items-center h-6 w-6 justify-center text-sml border text-yellow-700 border-yellow-700 bg-darkest rounded-md bg-transparent py-2 px-4 mt-0_5rem filter-button"
        >
          OR
        </button>
        <!-- Clear button -->
        <filter-clear-button
          v-if="filterLevelIf(index, null, 'clear')"
          :index="index"
          :internal-id="getIdHeader() + 'clear-' + index"
          @remove-and-shift="removeAndShift"
        ></filter-clear-button>
        <!-- Show a warning div if the filter is incomplete -->
        <span v-if="isFilterIncomplete(index, null)" class="filter-incomplete"
          >(!) Incomplete, will not apply</span
        >
        <span v-else-if="isDateFilterBadFormat(index, null)" class="filter-warning"
          >(!) Date should be between {{ getDateRange() }}</span
        >
      </div>
      <div
        class="ml-2rem filter-container focus-within:z-10"
        v-for="(_, orIndex) in generateOrFiltersConditionsArr(index)"
        :key="orIndex"
      >
        <!-- Separator -->
        <div class="text-gray-400 mt-0_5rem mr-1rem">
          <span class="text-gray text-lg">⮡ </span> OR
        </div>

        <!-- Top level filter options -->
        <filter-select
          v-if="true"
          :internal-id="getIdHeader() + 'top-' + index + '-' + orIndex"
          :option-list="getTopLevelFilterOptions()"
          level="top"
          :index="index"
          :or-index="orIndex"
          :model-value="modVals.orTop[index][orIndex]"
          @change-filter-value="changeFilterValue"
          @handle-filter-change="handleOrFilterChange"
        ></filter-select>

        <!-- Operator options -->
        <filter-select
          v-if="filterLevelIf(index, orIndex, 'operator')"
          :internal-id="getIdHeader() + 'op-' + index + '-' + orIndex"
          :option-list="getFilterOpOptions(modVals.orTop[index][orIndex])"
          level="operator"
          :index="index"
          :or-index="orIndex"
          :model-value="modVals.orOperator[index][orIndex]"
          @change-filter-value="changeFilterValue"
          @handle-filter-change="handleOrFilterChange"
        ></filter-select>

        <!-- Value options -->
        <filter-select
          v-if="filterLevelIf(index, orIndex, 'value', 'base')"
          :internal-id="getIdHeader() + 'value-' + index + '-' + orIndex"
          :option-list="getFilterValueOptions(modVals.orTop[index][orIndex])"
          level="value"
          :index="index"
          :or-index="orIndex"
          :model-value="modVals.orValue[index][orIndex]"
          @change-filter-value="changeFilterValue"
          @handle-filter-change="handleOrFilterChange"
        ></filter-select>

        <!-- Target Selectors - opens a custom modal -->
        <filter-modal-input
          v-if="filterLevelIf(index, orIndex, 'value', 'target')"
          :index="index"
          :or-index="orIndex"
          :model-value="modVals.orDValue[index][orIndex]"
          :internal-id="'filter-value-' + index + '-' + orIndex + (isLifetime ? '-lifetime' : '')"
          @open-modal="openTargetFilterMenu"
        ></filter-modal-input>

        <!-- Drop Selectors - opens a custom modal -->
        <filter-modal-input
          v-if="filterLevelIf(index, orIndex, 'value', 'drops')"
          :index="index"
          :or-index="orIndex"
          :model-value="modVals.orDValue[index][orIndex]"
          :internal-id="'filter-value-' + index + '-' + orIndex + (isLifetime ? '-lifetime' : '')"
          @open-modal="openDropFilterMenu"
        ></filter-modal-input>

        <!-- Value options - Launch/Return Date Selectors - Min: 20 January 2021 - Max: Today-->
        <filter-date-input
          v-if="filterLevelIf(index, orIndex, 'value', 'date')"
          :index="index"
          :or-index="orIndex"
          :model-value="modVals.orValue[index][orIndex]"
          :internal-id="getIdHeader() + 'value-' + index + '-' + orIndex"
          @change-filter-value="changeFilterValue"
          @handle-filter-change="handleOrFilterChange"
        ></filter-date-input>

        <!-- Clear button-->
        <filter-clear-button
          :index="index"
          :or-index="orIndex"
          :internal-id="getIdHeader() + 'clear-' + index + '-' + orIndex"
          @remove-and-shift="removeOrAndShift"
        ></filter-clear-button>

        <!-- Show a warning if the filter is incomplete -->
        <span v-if="isFilterIncomplete(index, orIndex)" class="filter-incomplete"
          >(!) Incomplete, will not apply</span
        >
        <span v-else-if="isDateFilterBadFormat(index, orIndex)" class="filter-warning"
          >(!) Date should be between {{ getDateRange() }}</span
        >
      </div>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent, PropType } from "vue";
import FilterClearButton from "./FilterClearButton.vue";
import FilterSelect from "./FilterSelect.vue";
import FilterModalInput from "./FilterModalInput.vue";
import FilterDateInput from "./FilterDateInput.vue";

interface FilterOption {
  value: string | number;
  text: string;
  styleClass?: string;
  imagePath?: string;
  rarity?: number;
  rarityGif?: string;
}

interface ModVals {
  top: (string | null)[];
  operator: (string | null)[];
  value: (string | null)[];
  dValue: (string | null)[];
  orCount: (number | null)[];
  orTop: (string | null)[][];
  orOperator: (string | null)[][];
  orValue: (string | null)[][];
  orDValue: (string | null)[][];
  [key: string]: unknown;
}

export default defineComponent({
  components: {
    FilterClearButton,
    FilterSelect,
    FilterModalInput,
    FilterDateInput,
  },
  emits: [
    "changeFilterValue",
    "handleFilterChange",
    "handleOrFilterChange",
    "removeAndShift",
    "removeOrAndShift",
    "addOr",
    "openDropFilterMenu",
    "openTargetFilterMenu",
  ],
  props: {
    filterArray: {
      type: Array as PropType<unknown[]>,
      default: () => [],
    },
    modVals: {
      type: Object as PropType<ModVals>,
      required: true,
    },
    isLifetime: Boolean,
    getFilterValueOptions: {
      type: Function as PropType<(topLevel: string | null) => FilterOption[]>,
      default: null,
    },
  },
  methods: {
    artifactDisplayText(artifact: { displayName: string; level: number | string }) {
      const { displayName, level } = artifact;
      const nameLower = displayName.toLowerCase();
      const levelOffset =
        nameLower.includes("stone") && !nameLower.includes("fragment") ? 2 : 1;
      if (level === "%") return displayName;
      return `${displayName} (T${Number(level) + levelOffset})`;
    },
    dropPath(drop: { protoName: string; level: number }) {
      const addendum = drop.protoName.includes("_STONE") ? 1 : 0;
      const fixedName = drop.protoName
        .replaceAll("_FRAGMENT", "")
        .replaceAll("ORNATE_GUSSET", "GUSSET")
        .replaceAll("VIAL_MARTIAN_DUST", "VIAL_OF_MARTIAN_DUST");
      return `artifacts/${fixedName}/${fixedName}_${drop.level + 1 + addendum}.png`;
    },
    dropRarityPath(drop: { rarity: number }) {
      switch (drop.rarity) {
        case 0:
          return "";
        case 1:
          return "images/rare.gif";
        case 2:
          return "images/epic.gif";
        case 3:
          return "images/legendary.gif";
        default:
          return "";
      }
    },
    removeAndShift(index: number) {
      this.$emit("removeAndShift", index);
    },
    removeOrAndShift(index: number, orIndex: number) {
      this.$emit("removeOrAndShift", index, orIndex);
    },
    handleFilterChange(event: Event) {
      this.$emit("handleFilterChange", event);
    },
    handleOrFilterChange(event: Event) {
      this.$emit("handleOrFilterChange", event);
    },
    changeFilterValue(
      index: number,
      orIndex: number | null,
      level: string,
      value: string,
    ) {
      this.$emit("changeFilterValue", index, orIndex, level, value);
    },
    addOr(index: number) {
      this.$emit("addOr", index);
    },
    openDropFilterMenu(index: number, orIndex: number | null) {
      this.$emit("openDropFilterMenu", index, orIndex);
    },
    openTargetFilterMenu(index: number, orIndex: number | null) {
      this.$emit("openTargetFilterMenu", index, orIndex);
    },
    generateOrFiltersConditionsArr(index: number) {
      const count = this.modVals.orCount[index];
      if (!count) return [];
      return new Array(count);
    },
    getIdHeader() {
      return this.isLifetime ? "lifetime-filter-" : "filter-";
    },
    resolveFilterRefs(index: number, orIndex: number | null) {
      if (orIndex != null) {
        return {
          top: this.modVals.orTop[index][orIndex],
          op: this.modVals.orOperator[index][orIndex],
          val: this.modVals.orValue[index][orIndex],
        };
      }
      return {
        top: this.modVals.top[index],
        op: this.modVals.operator[index],
        val: this.modVals.value[index],
      };
    },
    isFilterIncomplete(index: number, orIndex: number | null) {
      const { top, op, val } = this.resolveFilterRefs(index, orIndex);
      return (
        top != null &&
        (op == null ||
          val == null ||
          ((top === "returnDT" || top === "launchDT") && val === ""))
      );
    },
    isDateFilterBadFormat(index: number, orIndex: number | null) {
      const { top, op, val } = this.resolveFilterRefs(index, orIndex);
      if (top !== "launchDT" && top !== "returnDT") return false;
      // Operator not being set is not a bad format
      if (op == null || op === "") return false;
      // Valid date range is 2021-01-20 to tomorrow
      const minDate = new Date("2021-01-20");
      const maxDate = new Date();
      maxDate.setDate(maxDate.getDate() + 1);
      const dateValue = new Date(val as string);
      return dateValue < minDate || dateValue > maxDate;
    },
    getDateRange() {
      const minDate = new Date("2021-01-20");
      const maxDate = new Date();
      maxDate.setDate(maxDate.getDate() + 1);
      return minDate.toLocaleDateString() + " and " + maxDate.toLocaleDateString();
    },
    getTopLevelFilterOptions(): FilterOption[] {
      const commonOptions: FilterOption[] = [
        { text: "Ship", value: "ship" },
        { text: "Farm", value: "farm" },
        { text: "Duration", value: "duration" },
        { text: "Level", value: "level" },
        { text: "Target", value: "target" },
        { text: "Double Cap", value: "dubcap" },
        { text: "Bugged Cap", value: "buggedcap" },
      ];
      if (this.isLifetime)
        return commonOptions.concat({ text: "Drop Date", value: "returnDT" });
      return commonOptions.concat(
        { text: "Drops/Loot", value: "drops" },
        { text: "Launch Date", value: "launchDT" },
        { text: "Return Date", value: "returnDT" },
      );
    },
    getFilterOpOptions(topLevel: string | null): FilterOption[] {
      if (topLevel == null) return [];
      switch (topLevel) {
        case "ship":
        case "duration":
        case "level":
          return [
            { text: "is", value: "=" },
            { text: "is not", value: "!=" },
            { text: "greater than", value: ">" },
            { text: "less than", value: "<" },
          ];
        case "farm":
        case "target":
          return [
            { text: "is", value: "=" },
            { text: "is not", value: "!=" },
          ];
        case "drops":
          return [
            { text: "contains", value: "c" },
            { text: "does not contain", value: "dnc" },
          ];
        case "launchDT":
        case "returnDT":
          return [
            { text: "on", value: "d=" },
            { text: "before", value: "<" },
            { text: "after", value: ">" },
          ];
        case "dubcap":
        case "buggedcap":
          return [{ text: "is", value: "=" }];
        default:
          return [];
      }
    },
    filterValueIf(top: string | null, op: string | null, vtype?: string): boolean {
      if (top == null || op == null) return false;
      switch (vtype) {
        case "base":
          return this.isBaseFilter(top);
        case "drops":
          return top === "drops";
        case "target":
          return top === "target";
        case "date":
          return top === "launchDT" || top === "returnDT";
        default:
          return true;
      }
    },
    filterLevelIf(
      index: number,
      orIndex: number | null,
      level: string,
      vtype?: string,
    ): boolean {
      const { top, op, val } = this.resolveFilterRefs(index, orIndex);
      switch (level) {
        case "top":
          return true;
        case "operator":
          return top != null;
        case "value":
          return this.filterValueIf(top, op, vtype);
        case "or":
          return (
            orIndex == null &&
            top != null &&
            op != null &&
            val != null &&
            ((top !== "returnDT" && top !== "launchDT") || val !== "")
          );
        case "clear":
          return orIndex != null || top != null;
        default:
          return false;
      }
    },
    isBaseFilter(filterOp: string) {
      return !["launchDT", "returnDT", "drops", "target"].includes(filterOp);
    },
    isOrDisabled(index: number) {
      const orCount = this.modVals.orCount[index];
      if (orCount == null || orCount == 0) return false;
      const last = orCount - 1;
      return (
        this.modVals.orTop[index][last] == null ||
        this.modVals.orOperator[index][last] == null ||
        this.modVals.orValue[index][last] == null
      );
    },
  },
});
</script>
