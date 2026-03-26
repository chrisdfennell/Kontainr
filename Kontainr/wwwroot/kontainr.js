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
    },

    // ── Cytoscape.js network topology ─────────────────────────
    _cy: null,
    _cyDotnetRef: null,
    _cyResizeHandler: null,

    initTopology: function (elementId, elements, dotnetRef) {
        if (typeof cytoscape === 'undefined') {
            console.error('cytoscape.js not loaded');
            return;
        }
        this._cyDotnetRef = dotnetRef;
        this._cy = cytoscape({
            container: document.getElementById(elementId),
            elements: elements,
            style: [
                {
                    selector: 'node[nodeType="network"]',
                    style: {
                        'background-color': '#1c2333',
                        'background-opacity': 0.8,
                        'border-color': '#bc8cff',
                        'border-width': 2,
                        'label': 'data(label)',
                        'color': '#bc8cff',
                        'font-size': '13px',
                        'font-weight': 'bold',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'shape': 'roundrectangle',
                        'width': 'label',
                        'height': 'label',
                        'padding': '18px',
                        'text-wrap': 'wrap',
                        'text-max-width': '160px'
                    }
                },
                {
                    selector: 'node[nodeType="container"][status="running"]',
                    style: {
                        'background-color': '#0d2818',
                        'border-color': '#3fb950',
                        'border-width': 2,
                        'label': 'data(label)',
                        'color': '#e6edf3',
                        'font-size': '10px',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'shape': 'roundrectangle',
                        'width': 'label',
                        'height': 'label',
                        'padding': '14px',
                        'text-wrap': 'wrap',
                        'text-max-width': '150px'
                    }
                },
                {
                    selector: 'node[nodeType="container"][status!="running"]',
                    style: {
                        'background-color': '#2d1619',
                        'border-color': '#f85149',
                        'border-width': 2,
                        'border-style': 'dashed',
                        'label': 'data(label)',
                        'color': '#8b949e',
                        'font-size': '10px',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'shape': 'roundrectangle',
                        'width': 'label',
                        'height': 'label',
                        'padding': '14px',
                        'text-wrap': 'wrap',
                        'text-max-width': '150px',
                        'opacity': 0.7
                    }
                },
                {
                    selector: 'edge',
                    style: {
                        'width': 2,
                        'line-color': '#30363d',
                        'curve-style': 'bezier',
                        'target-arrow-shape': 'none',
                        'opacity': 0.6
                    }
                },
                {
                    selector: 'node:selected',
                    style: {
                        'border-color': '#58a6ff',
                        'border-width': 3
                    }
                }
            ],
            layout: { name: 'cose', animate: true, animationDuration: 500, padding: 50, nodeRepulsion: function () { return 8000; }, idealEdgeLength: function () { return 120; } },
            minZoom: 0.2,
            maxZoom: 4
        });

        // Click: navigate to container detail
        this._cy.on('tap', 'node[nodeType="container"]', function (evt) {
            var cid = evt.target.data('containerId');
            if (cid && dotnetRef) {
                dotnetRef.invokeMethodAsync('NavigateToContainer', cid);
            }
        });

        // Click: navigate to network (optional, for network nodes)
        this._cy.on('tap', 'node[nodeType="network"]', function (evt) {
            // No-op for now, could link to network detail in future
        });

        // Resize handler
        this._cyResizeHandler = function () {
            if (window.kontainr._cy) window.kontainr._cy.resize();
        };
        window.addEventListener('resize', this._cyResizeHandler);
    },

    updateTopology: function (elements) {
        if (!this._cy) return;
        this._cy.elements().remove();
        this._cy.add(elements);
        this._cy.layout({ name: 'cose', animate: true, animationDuration: 500, padding: 50, nodeRepulsion: function () { return 8000; }, idealEdgeLength: function () { return 120; } }).run();
    },

    setTopologyLayout: function (layoutName) {
        if (!this._cy) return;
        var opts = { name: layoutName, animate: true, animationDuration: 500, padding: 50 };
        if (layoutName === 'cose') {
            opts.nodeRepulsion = function () { return 8000; };
            opts.idealEdgeLength = function () { return 120; };
        }
        if (layoutName === 'breadthfirst') {
            opts.directed = true;
            opts.spacingFactor = 1.5;
        }
        this._cy.layout(opts).run();
    },

    resetTopologyView: function () {
        if (!this._cy) return;
        this._cy.fit(null, 50);
    },

    disposeTopology: function () {
        if (this._cyResizeHandler) {
            window.removeEventListener('resize', this._cyResizeHandler);
            this._cyResizeHandler = null;
        }
        if (this._cy) {
            this._cy.destroy();
            this._cy = null;
            this._cyDotnetRef = null;
        }
    }
};
