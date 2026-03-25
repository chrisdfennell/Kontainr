// Auto-scroll log viewer to bottom
window.kontainr = {
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    },
    focusElement: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.focus();
    }
};
