window.descargarArchivo = (nombreArchivo, base64) => {
    const enlace = document.createElement('a');
    enlace.href = 'data:application/pdf;base64,' + base64;
    enlace.download = nombreArchivo;
    document.body.appendChild(enlace);
    enlace.click();
    document.body.removeChild(enlace);
};