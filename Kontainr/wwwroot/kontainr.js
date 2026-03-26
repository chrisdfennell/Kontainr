window.kontainr = {
    initGlobalSearch: function () {
        // Use event delegation on document so it survives Blazor re-renders
        if (this._globalSearchInit) return;
        this._globalSearchInit = true;
        document.addEventListener('keydown', function (e) {
            if (e.key !== 'Enter') return;
            var input = document.getElementById('k-global-search-input');
            if (!input || document.activeElement !== input) return;
            var q = input.value.trim();
            if (q) {
                window.location.href = '/search?q=' + encodeURIComponent(q);
                input.value = '';
            }
        });
    },
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
        if (theme === 'light') {
            document.body.classList.add('k-light');
        } else {
            document.body.classList.remove('k-light');
        }
        // Toggle icon visibility
        var sun = document.querySelector('.k-theme-sun');
        var moon = document.querySelector('.k-theme-moon');
        if (sun) sun.style.display = theme === 'dark' ? '' : 'none';
        if (moon) moon.style.display = theme === 'light' ? '' : 'none';
    },
    _themeRef: null,
    registerThemeRef: function (ref) {
        this._themeRef = ref;
    },
    toggleTheme: function () {
        var current = document.documentElement.getAttribute('data-theme') || 'dark';
        var next = current === 'dark' ? 'light' : 'dark';
        this.setTheme(next);
        if (this._themeRef) {
            this._themeRef.invokeMethodAsync('OnThemeChanged', next);
        }
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

    // Color palette for networks — each network gets a unique color
    _netColors: ['#bc8cff', '#58a6ff', '#d29922', '#3fb950', '#f778ba', '#f0883e', '#79c0ff', '#56d4dd', '#db6d28', '#a5d6ff'],
    _netColorMap: {},
    _netColorIdx: 0,

    _getNetColor: function (netId) {
        if (!this._netColorMap[netId]) {
            this._netColorMap[netId] = this._netColors[this._netColorIdx % this._netColors.length];
            this._netColorIdx++;
        }
        return this._netColorMap[netId];
    },

    _coseLayout: { name: 'cose', animate: true, animationDuration: 600, padding: 60, nodeRepulsion: function () { return 12000; }, idealEdgeLength: function () { return 150; }, edgeElasticity: function () { return 80; }, gravity: 0.3, numIter: 800 },

    initTopology: function (elementId, elements, dotnetRef) {
        if (typeof cytoscape === 'undefined') {
            console.error('cytoscape.js not loaded');
            return;
        }
        // Reset color mapping
        this._netColorMap = {};
        this._netColorIdx = 0;

        // Assign colors to network nodes and their edges
        var self = this;
        elements.forEach(function (el) {
            if (el.data.nodeType === 'network') {
                el.data.color = self._getNetColor(el.data.id);
            }
            if (el.data.source && el.data.targetNetId) {
                el.data.color = self._getNetColor(el.data.targetNetId);
            }
        });

        this._cyDotnetRef = dotnetRef;
        this._cy = cytoscape({
            container: document.getElementById(elementId),
            elements: elements,
            style: [
                // ── Network nodes: bold diamond shapes ──
                {
                    selector: 'node[nodeType="network"]',
                    style: {
                        'background-color': '#161b22',
                        'background-opacity': 0.9,
                        'border-color': 'data(color)',
                        'border-width': 3,
                        'label': 'data(label)',
                        'color': 'data(color)',
                        'font-size': '14px',
                        'font-weight': 'bold',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'shape': 'diamond',
                        'width': 80,
                        'height': 80,
                        'text-wrap': 'wrap',
                        'text-max-width': '140px',
                        'text-margin-y': 0,
                        'overlay-opacity': 0,
                        'shadow-blur': 20,
                        'shadow-color': 'data(color)',
                        'shadow-opacity': 0.4,
                        'shadow-offset-x': 0,
                        'shadow-offset-y': 0
                    }
                },
                // ── Running container nodes ──
                {
                    selector: 'node[nodeType="container"][status="running"]',
                    style: {
                        'background-color': '#0d2818',
                        'border-color': '#3fb950',
                        'border-width': 2.5,
                        'label': 'data(label)',
                        'color': '#e6edf3',
                        'font-size': '11px',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'shape': 'roundrectangle',
                        'width': 'label',
                        'height': 'label',
                        'padding': '16px',
                        'text-wrap': 'wrap',
                        'text-max-width': '160px',
                        'overlay-opacity': 0,
                        'shadow-blur': 12,
                        'shadow-color': '#3fb950',
                        'shadow-opacity': 0.25,
                        'shadow-offset-x': 0,
                        'shadow-offset-y': 0
                    }
                },
                // ── Stopped container nodes ──
                {
                    selector: 'node[nodeType="container"][status!="running"]',
                    style: {
                        'background-color': '#1c1012',
                        'border-color': '#f85149',
                        'border-width': 2,
                        'border-style': 'dashed',
                        'label': 'data(label)',
                        'color': '#6e7681',
                        'font-size': '11px',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'shape': 'roundrectangle',
                        'width': 'label',
                        'height': 'label',
                        'padding': '16px',
                        'text-wrap': 'wrap',
                        'text-max-width': '160px',
                        'opacity': 0.55,
                        'overlay-opacity': 0
                    }
                },
                // ── Port badge nodes ──
                {
                    selector: 'node[nodeType="port"]',
                    style: {
                        'background-color': '#1a3a52',
                        'border-color': '#58a6ff',
                        'border-width': 1.5,
                        'label': 'data(label)',
                        'color': '#58a6ff',
                        'font-size': '9px',
                        'font-weight': 'bold',
                        'text-valign': 'center',
                        'text-halign': 'center',
                        'shape': 'roundrectangle',
                        'width': 'label',
                        'height': 'label',
                        'padding': '8px',
                        'overlay-opacity': 0,
                        'shadow-blur': 8,
                        'shadow-color': '#58a6ff',
                        'shadow-opacity': 0.2,
                        'shadow-offset-x': 0,
                        'shadow-offset-y': 0
                    }
                },
                // ── Network edges: colored per network ──
                {
                    selector: 'edge[edgeType="network"]',
                    style: {
                        'width': 2.5,
                        'line-color': 'data(color)',
                        'line-opacity': 0.5,
                        'curve-style': 'bezier',
                        'target-arrow-shape': 'none',
                        'label': 'data(label)',
                        'font-size': '9px',
                        'color': '#6e7681',
                        'text-rotation': 'autorotate',
                        'text-margin-y': -10,
                        'text-opacity': 0.8,
                        'text-background-color': '#0d1117',
                        'text-background-opacity': 0.8,
                        'text-background-padding': '3px',
                        'overlay-opacity': 0
                    }
                },
                // ── Port edges: subtle connector ──
                {
                    selector: 'edge[edgeType="port"]',
                    style: {
                        'width': 1.5,
                        'line-color': '#58a6ff',
                        'line-opacity': 0.3,
                        'line-style': 'dotted',
                        'curve-style': 'bezier',
                        'target-arrow-shape': 'none',
                        'overlay-opacity': 0
                    }
                },
                // ── Hover: highlight node ──
                {
                    selector: 'node.hover',
                    style: {
                        'border-width': 4,
                        'shadow-opacity': 0.7,
                        'z-index': 999
                    }
                },
                // ── Dim non-connected when hovering ──
                {
                    selector: 'node.dimmed',
                    style: {
                        'opacity': 0.15
                    }
                },
                {
                    selector: 'edge.dimmed',
                    style: {
                        'opacity': 0.08
                    }
                },
                {
                    selector: 'edge.highlighted',
                    style: {
                        'width': 4,
                        'line-opacity': 0.9,
                        'z-index': 999
                    }
                },
                // ── Selected ──
                {
                    selector: 'node:selected',
                    style: {
                        'border-color': '#58a6ff',
                        'border-width': 4,
                        'shadow-color': '#58a6ff',
                        'shadow-opacity': 0.6
                    }
                },
                // ── Cursor ──
                {
                    selector: 'node[nodeType="container"]',
                    style: {
                        'cursor': 'pointer'
                    }
                }
            ],
            layout: this._coseLayout,
            minZoom: 0.15,
            maxZoom: 4
        });

        // ── Hover interaction: highlight connections ──
        this._cy.on('mouseover', 'node', function (evt) {
            var node = evt.target;
            node.addClass('hover');
            var connected = node.neighborhood();
            // Also include port nodes and their edges for containers
            if (node.data('nodeType') === 'container' || node.data('nodeType') === 'network') {
                self._cy.elements().not(node).not(connected).addClass('dimmed');
                connected.edges().addClass('highlighted');
            }
        });
        this._cy.on('mouseout', 'node', function (evt) {
            evt.target.removeClass('hover');
            self._cy.elements().removeClass('dimmed highlighted');
        });

        // Click: navigate to container detail
        this._cy.on('tap', 'node[nodeType="container"]', function (evt) {
            var cid = evt.target.data('containerId');
            if (cid && dotnetRef) {
                dotnetRef.invokeMethodAsync('NavigateToContainer', cid);
            }
        });

        // Resize handler
        this._cyResizeHandler = function () {
            if (window.kontainr._cy) window.kontainr._cy.resize();
        };
        window.addEventListener('resize', this._cyResizeHandler);
    },

    updateTopology: function (elements) {
        if (!this._cy) return;
        // Reset color mapping
        this._netColorMap = {};
        this._netColorIdx = 0;
        var self = this;
        elements.forEach(function (el) {
            if (el.data.nodeType === 'network') {
                el.data.color = self._getNetColor(el.data.id);
            }
            if (el.data.source && el.data.targetNetId) {
                el.data.color = self._getNetColor(el.data.targetNetId);
            }
        });
        this._cy.elements().remove();
        this._cy.add(elements);
        this._cy.layout(this._coseLayout).run();
    },

    setTopologyLayout: function (layoutName) {
        if (!this._cy) return;
        var opts = { name: layoutName, animate: true, animationDuration: 600, padding: 60 };
        if (layoutName === 'cose') {
            opts.nodeRepulsion = function () { return 12000; };
            opts.idealEdgeLength = function () { return 150; };
            opts.edgeElasticity = function () { return 80; };
            opts.gravity = 0.3;
            opts.numIter = 800;
        }
        if (layoutName === 'breadthfirst') {
            opts.directed = true;
            opts.spacingFactor = 1.75;
        }
        if (layoutName === 'circle') {
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
