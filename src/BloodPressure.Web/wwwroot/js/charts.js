window.bpCharts = window.bpCharts || {};
window.bpCharts._instances = window.bpCharts._instances || {};

function normalizeNumber(value) {
    if (value === null || value === undefined) {
        return null;
    }

    if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
}

function toRgba(color, alpha) {
    if (!color || typeof color !== 'string') {
        return `rgba(0, 0, 0, ${alpha})`;
    }

    const hex = color.trim();
    if (hex.startsWith('#')) {
        const raw = hex.slice(1);
        const full = raw.length === 3
            ? raw.split('').map(ch => ch + ch).join('')
            : raw;

        if (full.length === 6) {
            const r = parseInt(full.slice(0, 2), 16);
            const g = parseInt(full.slice(2, 4), 16);
            const b = parseInt(full.slice(4, 6), 16);
            return `rgba(${r}, ${g}, ${b}, ${alpha})`;
        }
    }

    return color;
}

function buildTrendDataset(dataset, labelsLength) {
    if (!dataset || !Array.isArray(dataset.data)) {
        return null;
    }

    if (dataset.isTrendLine === true) {
        return null;
    }

    const points = [];
    for (let i = 0; i < dataset.data.length; i++) {
        const y = normalizeNumber(dataset.data[i]);
        if (y !== null) {
            points.push({ x: i, y });
        }
    }

    if (points.length < 2) {
        return null;
    }

    const n = points.length;
    const sumX = points.reduce((acc, p) => acc + p.x, 0);
    const sumY = points.reduce((acc, p) => acc + p.y, 0);
    const sumXY = points.reduce((acc, p) => acc + (p.x * p.y), 0);
    const sumX2 = points.reduce((acc, p) => acc + (p.x * p.x), 0);
    const denominator = (n * sumX2) - (sumX * sumX);

    if (denominator === 0) {
        return null;
    }

    const slope = ((n * sumXY) - (sumX * sumY)) / denominator;
    const intercept = (sumY - (slope * sumX)) / n;

    const trendData = Array.from({ length: labelsLength }, (_, x) => Number((intercept + slope * x).toFixed(2)));
    const baseColor = dataset.borderColor || dataset.backgroundColor || '#666666';

    return {
        label: `${dataset.label || 'Serie'} trend`,
        data: trendData,
        borderColor: toRgba(baseColor, 0.85),
        backgroundColor: toRgba(baseColor, 0.2),
        borderDash: [6, 6],
        borderWidth: 2,
        pointRadius: 0,
        pointHoverRadius: 0,
        tension: 0.2,
        fill: false,
        isTrendLine: true
    };
}

window.bpCharts.renderLineChart = (canvasId, labels, datasets, title) => {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    const existing = window.bpCharts._instances[canvasId];
    if (existing) {
        existing.destroy();
    }

    const sourceDatasets = Array.isArray(datasets) ? datasets : [];
    const withTrend = [];
    for (const dataset of sourceDatasets) {
        withTrend.push(dataset);
        const trend = buildTrendDataset(dataset, Array.isArray(labels) ? labels.length : 0);
        if (trend) {
            withTrend.push(trend);
        }
    }

    window.bpCharts._instances[canvasId] = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: withTrend
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: 'top' },
                title: { display: !!title, text: title }
            }
        }
    });
};
