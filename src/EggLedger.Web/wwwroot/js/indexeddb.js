const DB_NAME = "eggledger";
const DB_VERSION = 1;

const MISSION_INDEX_COLS = [
  "ship",
  "duration_type",
  "level",
  "is_dub_cap",
  "is_bugged_cap",
  "target",
  "start_timestamp",
  "return_timestamp",
  "mission_type",
];

let _db = null;

function open() {
  if (_db) {
    return Promise.resolve(_db);
  }
  return new Promise((res, rej) => {
    const req = globalThis.indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;

      db.createObjectStore("backup", { keyPath: "player_id" });

      const mission = db.createObjectStore("mission", { keyPath: ["player_id", "mission_id"] });
      mission.createIndex("player_id", "player_id");
      for (const col of MISSION_INDEX_COLS) {
        mission.createIndex(`player_${col}`, ["player_id", col]);
      }

      const drops = db.createObjectStore("artifact_drops", { keyPath: "id", autoIncrement: true });
      drops.createIndex("mission_player_dropindex", ["mission_id", "player_id", "drop_index"], { unique: true });
      drops.createIndex("player_rarity", ["player_id", "rarity"]);
      drops.createIndex("player_spec", ["player_id", "spec_type"]);

      db.createObjectStore("settings", { keyPath: "key" });

      const reports = db.createObjectStore("reports", { keyPath: "id" });
      reports.createIndex("account_id", "account_id");

      const reportGroups = db.createObjectStore("report_groups", { keyPath: "id" });
      reportGroups.createIndex("account_id", "account_id");
    };
    req.onsuccess = () => {
      _db = req.result;
      res(_db);
    };
    req.onerror = () => rej(req.error);
    req.onblocked = () => rej(new Error("indexeddb open blocked"));
  });
}

function tx(store, mode) {
  return _db.transaction(store, mode).objectStore(store);
}

function wrap(req) {
  return new Promise((res, rej) => {
    req.onsuccess = () => res(req.result);
    req.onerror = () => rej(req.error);
  });
}

export async function put(store, value) {
  await open();
  return wrap(tx(store, "readwrite").put(value));
}

export async function putMany(store, values) {
  await open();
  const objectStore = tx(store, "readwrite");
  // All puts enqueue synchronously on one transaction; do not await between them.
  await Promise.all(values.map((value) => wrap(objectStore.put(value))));
  return values.length;
}

export async function get(store, key) {
  await open();
  return wrap(tx(store, "readonly").get(key));
}

export async function getAll(store) {
  await open();
  return wrap(tx(store, "readonly").getAll());
}

export async function getAllByIndex(store, index, value) {
  await open();
  return wrap(tx(store, "readonly").index(index).getAll(value));
}

export async function del(store, key) {
  await open();
  return wrap(tx(store, "readwrite").delete(key));
}

export async function clear(store) {
  await open();
  return wrap(tx(store, "readwrite").clear());
}

export async function count(store) {
  await open();
  return wrap(tx(store, "readonly").count());
}
