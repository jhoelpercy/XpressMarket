const graficosActivos = {};

window.renderizarGraficoLinea = (idCanvas, labels, datos, colorBorde, colorFondo, etiqueta) => {
    const ctx = document.getElementById(idCanvas);
    if (!ctx) return;
    if (graficosActivos[idCanvas]) graficosActivos[idCanvas].destroy();

    graficosActivos[idCanvas] = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: etiqueta,
                data: datos,
                borderColor: colorBorde,
                backgroundColor: colorFondo,
                fill: true,
                tension: 0.3
            }]
        },
        options: { responsive: true, plugins: { legend: { display: false } } }
    });
};

window.renderizarGraficoBarras = (idCanvas, labels, datos, color, etiqueta) => {
    const ctx = document.getElementById(idCanvas);
    if (!ctx) return;
    if (graficosActivos[idCanvas]) graficosActivos[idCanvas].destroy();

    graficosActivos[idCanvas] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: etiqueta,
                data: datos,
                backgroundColor: color
            }]
        },
        options: { responsive: true, plugins: { legend: { display: false } } }
    });
};