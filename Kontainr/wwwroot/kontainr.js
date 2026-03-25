window.kontainr = {
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    },
    focusElement: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.focus();
    },
    drawSparkline: function (canvasId, data, color, maxVal) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !data || data.length === 0) return;
        const ctx = canvas.getContext('2d');
        const w = canvas.width = canvas.offsetWidth * (window.devicePixelRatio || 1);
        const h = canvas.height = canvas.offsetHeight * (window.devicePixelRatio || 1);
        ctx.clearRect(0, 0, w, h);
        const max = maxVal > 0 ? maxVal : Math.max(...data, 1);
        const step = w / Math.max(data.length - 1, 1);
        ctx.beginPath();
        ctx.moveTo(0, h);
        for (let i = 0; i < data.length; i++) {
            const x = i * step;
            const y = h - (data[i] / max) * h * 0.9;
            ctx.lineTo(x, y);
        }
        ctx.lineTo((data.length - 1) * step, h);
        ctx.closePath();
        ctx.fillStyle = color + '20';
        ctx.fill();
        ctx.beginPath();
        for (let i = 0; i < data.length; i++) {
            const x = i * step;
            const y = h - (data[i] / max) * h * 0.9;
            if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
        }
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.5 * (window.devicePixelRatio || 1);
        ctx.stroke();
    },
    setTheme: function (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        document.body.className = theme === 'light' ? 'k-light' : '';
    },
    downloadText: function (filename, text) {
        const blob = new Blob([text], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    // ── xterm.js interactive terminal ──────────────────────────
    _terminals: {},

    initTerminal: function (elementId, dotnetRef) {
        if (typeof Terminal === 'undefined') {
            console.error('xterm.js not loaded');
            return;
        }
        const term = new Terminal({
            cursorBlink: true,
            fontSize: 13,
            fontFamily: "'Cascadia Code', 'Fira Code', 'JetBrains Mono', monospace",
            theme: {
                background: '#010409',
                foreground: '#c9d1d9',
                cursor: '#58a6ff',
                selectionBackground: '#264f78',
            },
            convertEol: true
        });
        const fitAddon = new FitAddon.FitAddon();
        term.loadAddon(fitAddon);
        term.open(document.getElementById(elementId));
        fitAddon.fit();

        // Buffer for collecting typed characters
        let inputBuffer = '';

        term.onData(function (data) {
            // Send each keystroke to .NET
            dotnetRef.invokeMethodAsync('OnTerminalInput', data);
        });

        window.addEventListener('resize', function () { fitAddon.fit(); });

        this._terminals[elementId] = { term, fitAddon };
    },

    writeTerminal: function (elementId, text) {
        const t = this._terminals[elementId];
        if (t) t.term.write(text);
    },

    fitTerminal: function (elementId) {
        const t = this._terminals[elementId];
        if (t) t.fitAddon.fit();
    },

    disposeTerminal: function (elementId) {
        const t = this._terminals[elementId];
        if (t) {
            t.term.dispose();
            delete this._terminals[elementId];
        }
    },

    getTerminalSize: function (elementId) {
        const t = this._terminals[elementId];
        if (t) return { cols: t.term.cols, rows: t.term.rows };
        return { cols: 80, rows: 24 };
    }
};
