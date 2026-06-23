// AES-256-GCM blob crypto via the browser SubtleCrypto API. The WASM runtime
// does not support System.Security.Cryptography.AesGcm, so the Blazor client
// calls these instead. Wire format matches the Go server and the managed
// BlobCrypto: base64( nonce(12) || ciphertext || tag(16) ). SubtleCrypto's
// AES-GCM output is ciphertext||tag, identical to Go's gcm.Seal, so the layout
// lines up byte for byte.

const NONCE_SIZE = 12;

function hexToBytes(hex) {
  if (hex.length !== 64) {
    throw new Error(`AES-256 key must be 64 hex chars, got ${hex.length}`);
  }
  const out = new Uint8Array(32);
  for (let i = 0; i < 32; i++) {
    out[i] = parseInt(hex.substr(i * 2, 2), 16);
  }
  return out;
}

function b64ToBytes(b64) {
  const bin = atob(b64);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) {
    out[i] = bin.charCodeAt(i);
  }
  return out;
}

function bytesToB64(bytes) {
  let bin = "";
  for (let i = 0; i < bytes.length; i++) {
    bin += String.fromCharCode(bytes[i]);
  }
  return btoa(bin);
}

async function importKey(hexKey) {
  return crypto.subtle.importKey(
    "raw",
    hexToBytes(hexKey),
    { name: "AES-GCM" },
    false,
    ["encrypt", "decrypt"],
  );
}

// plaintextB64: base64 of the raw plaintext bytes (Blazor passes a byte[] as base64).
// returns base64( nonce || ciphertext || tag ).
export async function encrypt(hexKey, plaintextB64) {
  const key = await importKey(hexKey);
  const nonce = crypto.getRandomValues(new Uint8Array(NONCE_SIZE));
  const plaintext = b64ToBytes(plaintextB64);
  const sealed = new Uint8Array(
    await crypto.subtle.encrypt({ name: "AES-GCM", iv: nonce, tagLength: 128 }, key, plaintext),
  );
  const out = new Uint8Array(NONCE_SIZE + sealed.length);
  out.set(nonce, 0);
  out.set(sealed, NONCE_SIZE);
  return bytesToB64(out);
}

// b64: base64( nonce || ciphertext || tag ). returns base64 of the plaintext bytes.
export async function decrypt(hexKey, b64) {
  const key = await importKey(hexKey);
  const data = b64ToBytes(b64);
  if (data.length < NONCE_SIZE + 16) {
    throw new Error("blob too short");
  }
  const nonce = data.slice(0, NONCE_SIZE);
  const sealed = data.slice(NONCE_SIZE); // ciphertext || tag
  const plaintext = new Uint8Array(
    await crypto.subtle.decrypt({ name: "AES-GCM", iv: nonce, tagLength: 128 }, key, sealed),
  );
  return bytesToB64(plaintext);
}
