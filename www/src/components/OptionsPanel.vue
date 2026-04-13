<template>
  <div v-if="hasAnyFilterableData">
    <div>
      <span class="mr-0_5rem section-heading">Options</span>
      <button
        id="toggleOptionsButtonVM"
        class="text-base toggle-link"
        type="button"
        @click="hideOptions = !hideOptions"
      >
        {{ hideOptions ? 'Show' : 'Hide' }}
      </button>
    </div>
    <div v-if="!hideOptions">
      <span class="opt-span">
        <label for="viewByDateCB" class="ext-opt-label">Separate missions by day</label>
        <input id="viewByDateCB" type="checkbox" v-model="localViewByDate" class="ext-opt-check" />
      </span>
      <span class="opt-span">
        <label for="viewMissionTimesCB" class="ext-opt-label">Show launch times</label>
        <input id="viewMissionTimesCB" type="checkbox" v-model="localViewMissionTimes" class="ext-opt-check" />
      </span>
      <span class="opt-span">
        <label for="recolorDCCB" class="ext-opt-label">Re-color dubcaps</label>
        <input id="recolorDCCB" type="checkbox" v-model="localRecolorDC" class="ext-opt-check" />
      </span>
      <span class="opt-span">
        <label for="recolorBCCB" class="ext-opt-label">Re-color bugged-caps</label>
        <input id="recolorBCCB" type="checkbox" v-model="localRecolorBC" class="ext-opt-check" />
      </span>
      <span class="opt-span">
        <label for="showExpectedDropsPerShip" class="ext-opt-label">Show "Expected Drops Per Ship"</label>
        <input
          id="showExpectedDropsPerShip"
          type="checkbox"
          :disabled="!mennoDataLoaded"
          v-model="localShowExpectedDropsPerShip"
          class="ext-opt-check"
        />
      </span>
      <div>
        <span class="section-heading">Multi-View Method</span><br />
        <span class="opt-span">
          <label for="multiViewOffCB" class="ext-opt-label">Off</label>
          <input id="multiViewOffCB" type="radio" v-model="localMultiViewMode" value="off" class="ext-opt-check" />
        </span>
        <span class="opt-span">
          <label for="multiViewRowCB" class="ext-opt-label">Row/Date Select</label>
          <input id="multiViewRowCB" type="radio" v-model="localMultiViewMode" value="row" class="ext-opt-check" />
        </span>
        <span class="opt-span">
          <label for="multiViewFreeCB" class="ext-opt-label">Free Select</label>
          <input id="multiViewFreeCB" type="radio" v-model="localMultiViewMode" value="free" class="ext-opt-check" />
        </span>
      </div>
      <div>
        <span class="section-heading">Drops Sort Method</span><br />
        <span class="opt-span">
          <label for="viewMissionSortDefault" class="ext-opt-label">Default</label>
          <input
            id="viewMissionSortDefault"
            type="radio"
            v-model="localViewMissionSortMethod"
            value="default"
            class="ext-opt-check"
          />
        </span>
        <span class="opt-span">
          <label for="viewMissionSortIV" class="ext-opt-label">Inventory Visualizer</label>
          <input
            id="viewMissionSortIV"
            type="radio"
            v-model="localViewMissionSortMethod"
            value="iv"
            class="ext-opt-check"
          />
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  hasAnyFilterableData: boolean
  mennoDataLoaded: boolean
  viewByDate: boolean
  viewMissionTimes: boolean
  recolorDC: boolean
  recolorBC: boolean
  showExpectedDropsPerShip: boolean
  multiViewMode: 'off' | 'row' | 'free'
  viewMissionSortMethod: 'default' | 'iv'
}>()

const emit = defineEmits<{
  'update:viewByDate': [value: boolean]
  'update:viewMissionTimes': [value: boolean]
  'update:recolorDC': [value: boolean]
  'update:recolorBC': [value: boolean]
  'update:showExpectedDropsPerShip': [value: boolean]
  'update:multiViewMode': [value: 'off' | 'row' | 'free']
  'update:viewMissionSortMethod': [value: 'default' | 'iv']
}>()

const hideOptions = ref(false)

const localViewByDate = computed({
  get: () => props.viewByDate,
  set: (val) => emit('update:viewByDate', val),
})

const localViewMissionTimes = computed({
  get: () => props.viewMissionTimes,
  set: (val) => emit('update:viewMissionTimes', val),
})

const localRecolorDC = computed({
  get: () => props.recolorDC,
  set: (val) => emit('update:recolorDC', val),
})

const localRecolorBC = computed({
  get: () => props.recolorBC,
  set: (val) => emit('update:recolorBC', val),
})

const localShowExpectedDropsPerShip = computed({
  get: () => props.showExpectedDropsPerShip,
  set: (val) => emit('update:showExpectedDropsPerShip', val),
})

const localMultiViewMode = computed({
  get: () => props.multiViewMode,
  set: (val) => emit('update:multiViewMode', val),
})

const localViewMissionSortMethod = computed({
  get: () => props.viewMissionSortMethod,
  set: (val) => emit('update:viewMissionSortMethod', val),
})
</script>
