window.downloadFile = (fileName, base64Data) => {
    const link = document.createElement("a");
    link.download = fileName;
    link.href = `data:text/csv;base64,${base64Data}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

Chart.defaults.font.family = "Poppins, system-ui, sans-serif";

window.renderChart = (canvasId, config) => {
    const canvas = document.getElementById(canvasId);
    const existingChart = Chart.getChart(canvasId);

    if (existingChart) {
        existingChart.destroy();
    }

    new Chart(canvas.getContext("2d"), config);
};
