// El sistema ya no usa service worker. Este archivo sólo limpia el que haya quedado instalado en
// navegadores que abrieron versiones anteriores: borra las cachés, se desinstala y recarga la página.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil((async () => {
    const claves = await caches.keys();
    await Promise.all(claves.map(k => caches.delete(k)));
    await self.registration.unregister();
    const clientes = await self.clients.matchAll({ type: 'window' });
    clientes.forEach(c => c.navigate(c.url));
})()));
// Sin evento 'fetch': todas las solicitudes van directo a la red.
