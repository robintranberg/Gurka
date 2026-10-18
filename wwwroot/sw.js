// Service worker: gör Gurka installerbar och visar skalet även utan nät.
// Nätet först så att en uppdaterad klient alltid vinner – cachen är bara reserv.
const C='gurka-v2',SHELL=['/','/app.css','/app.js','/lib/signalr.min.js','/icon.svg','/manifest.webmanifest','/fonts/montserrat.woff2','/fonts/roboto.woff2'];
self.addEventListener('install',e=>{e.waitUntil(caches.open(C).then(c=>c.addAll(SHELL)).then(()=>self.skipWaiting()))});
self.addEventListener('activate',e=>{e.waitUntil(caches.keys().then(ks=>Promise.all(ks.filter(k=>k!==C).map(k=>caches.delete(k)))).then(()=>self.clients.claim()))});
self.addEventListener('fetch',e=>{
 const u=new URL(e.request.url);
 if(e.request.method!=='GET'||u.origin!==location.origin||u.pathname.startsWith('/hub'))return; // SignalR går aldrig via cachen
 e.respondWith(fetch(e.request).then(r=>{if(r.ok){const k=r.clone();caches.open(C).then(c=>c.put(e.request,k))}return r})
  .catch(()=>caches.match(e.request,{ignoreSearch:true}).then(r=>r||caches.match('/'))));
});
