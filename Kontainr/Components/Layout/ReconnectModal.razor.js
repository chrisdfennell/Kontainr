// Set up event handlers
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", () => location.reload());

let autoRetryTimer = null;
let autoRetryCount = 0;

function handleReconnectStateChanged(event) {
    const state = event.detail.state;
    if (state === "show") {
        reconnectModal.showModal();
        autoRetryCount = 0;
        scheduleAutoRetry(1000);
    } else if (state === "hide") {
        clearAutoRetry();
        reconnectModal.close();
    } else if (state === "failed" || state === "resume-failed") {
        // Connection lost or resume failed — just reload the page
        location.reload();
    } else if (state === "rejected") {
        location.reload();
    } else if (state === "paused") {
        // Server paused the session — try to resume, if that fails reload
        tryResume();
    }
}

function scheduleAutoRetry(delayMs) {
    clearAutoRetry();
    autoRetryTimer = setTimeout(() => retry(), delayMs);
}

function clearAutoRetry() {
    if (autoRetryTimer) {
        clearTimeout(autoRetryTimer);
        autoRetryTimer = null;
    }
}

async function retry() {
    clearAutoRetry();
    autoRetryCount++;

    try {
        const successful = await Blazor.reconnect();
        if (!successful) {
            // Server is up but circuit is gone (container restarted) — reload
            location.reload();
        }
    } catch (err) {
        // Server unreachable — retry with backoff (1s, 2s, 3s... max 5s)
        const delay = Math.min(autoRetryCount * 1000, 5000);
        scheduleAutoRetry(delay);
    }
}

async function tryResume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            location.reload();
        } else {
            reconnectModal.close();
        }
    } catch {
        location.reload();
    }
}
