// Native beforeunload confirm while a mission fetch is running, so a refresh/close mid-fetch
// isn't silently mistaken for the app being stuck - triggering it aborts the in-flight fetch.
(function () {
  if (window.__beforeUnloadWarningInit) return;
  window.__beforeUnloadWarningInit = true;

  let active = false;

  window.addEventListener("beforeunload", function (e) {
    if (!active) return;
    e.preventDefault();
    e.returnValue = "";
  });

  window.setFetchInProgressWarning = function (isActive) {
    active = isActive;
  };
})();
