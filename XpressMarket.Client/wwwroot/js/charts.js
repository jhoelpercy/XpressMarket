let graficoLinea = null;
let graficoBarras = null;

window.renderizarGraficoVentas = (labels, datos) => {
    const ctx = document.getElementById('graficoVentas');
    if (!ctx) return;
    if (graficoLinea) graficoLinea.destroy();

    graficoLinea = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Ventas (Bs)',
                data: datos,
                borderColor: '#1F3D2B',
                backgroundColor: 'rgba(31, 61, 43, 0.1)',
                fill: true,
                tension: 0.3
            }]
        },
        options: { responsive: true, plugins: { legend: { display: false } } }
    });
};

window.renderizarGraficoProductos = (labels, datos) => {
    const ctx = document.getElementById('graficoProductos');
    if (!ctx) return;
    if (graficoBarras) graficoBarras.destroy();

    graficoBarras = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Unidades vendidas',
                data: datos,
                backgroundColor: '#D9A544'
            }]
        },
        options: { responsive: true, plugins: { legend: { display: false } } }
    });
};