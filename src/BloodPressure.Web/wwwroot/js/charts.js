window.bpCharts = window.bpCharts || {};
window.bpCharts._instances = window.bpCharts._instances || {};

window.bpCharts.renderLineChart = (canvasId, labels, datasets, title) => {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    const existing = window.bpCharts._instances[canvasId];
    if (existing) {
        existing.destroy();
    }

    window.bpCharts._instances[canvasId] = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            plugins: {
                legend: { position: 'top' },
                title: { display: !!title, text: title }
            }
        }
    });
};
