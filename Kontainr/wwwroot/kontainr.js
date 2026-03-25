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

        // Fill area
        ctx.beginPath();
        ctx.moveTo(0, h);
        for (let i = 0; i < data.length; i++) {
            const x = i * step;
            const y = h - (data[i] / max) * h * 0.9;
            if (i === 0) ctx.lineTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.lineTo((data.length - 1) * step, h);
        ctx.closePath();
        ctx.fillStyle = color + '20';
        ctx.fill();

        // Line
        ctx.beginPath();
        for (let i = 0; i < data.length; i++) {
            const x = i * step;
            const y = h - (data[i] / max) * h * 0.9;
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.5 * (window.devicePixelRatio || 1);
        ctx.stroke();
    }
};
