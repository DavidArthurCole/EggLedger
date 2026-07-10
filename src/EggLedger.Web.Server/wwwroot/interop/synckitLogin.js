// Loads SyncKit's embedded popup login widget script on demand and drives it. On success,
// reloads the page so the freshly-minted cookie takes effect. On any failure (popup blocked/
// closed, script load, redeem failure), falls back to the existing full-page redirect login.

let scriptPromise = null;

function loadScript(identityHostUrl) {
  if (window.SyncKitAuth) return Promise.resolve();
  if (scriptPromise) return scriptPromise;
  scriptPromise = new Promise((resolve, reject) => {
    const el = document.createElement("script");
    el.src = identityHostUrl.replace(/\/$/, "") + "/synckit-login.js";
    el.onload = () => resolve();
    el.onerror = () => reject(new Error("script_load_failed"));
    document.head.appendChild(el);
  });
  return scriptPromise;
}

export async function login(identityHostUrl, fallbackUrl) {
  try {
    await loadScript(identityHostUrl);
    const { code } = await window.SyncKitAuth.login(identityHostUrl);
    const resp = await fetch("/api/v1/auth/redeem-code", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ code }),
    });
    if (!resp.ok) throw new Error("redeem_failed");
    window.location.reload();
  } catch {
    window.location.href = fallbackUrl;
  }
}
