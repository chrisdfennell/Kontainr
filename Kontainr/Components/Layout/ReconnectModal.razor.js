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
    if (event.detail.state === "show") {
        reconnectModal.showModal();
        // Start auto-retrying immediately
        autoRetryCount = 0;
        scheduleAutoRetry(1000);
    } else if (event.detail.state === "hide") {
        clearAutoRetry();
        reconnectModal.close();
    } else if (event.detail.state === "failed") {
        // Keep retrying automatically
        scheduleAutoRetry(2000);
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (event.detail.state === "rejected") {
        location.reload();
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
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    autoRetryCount++;

    try {
        const successful = await Blazor.reconnect();
        if (!successful) {
            // Server is up but circuit is gone (container was restarted) — just reload
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                location.reload();
            } else {
                reconnectModal.close();
            }
        }
    } catch (err) {
        // Server unreachable — retry with backoff (1s, 2s, 3s... max 5s)
        const delay = Math.min(autoRetryCount * 1000, 5000);
        scheduleAutoRetry(delay);
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}
