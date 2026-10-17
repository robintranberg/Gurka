// Versionshandslag: /build.js fångas upp och besvaras med olika värden. Räknar sidladdningar och kollar #verBanner. Arg: [url till server före handslaget]
const { launch, open, BASE, SHOTS } = require('./lib');
const sleep = ms => new Promise(r => setTimeout(r, ms)), OLD = '000000000000';
const errs = [];
async function run(br, name, base, { build, noSS } = {}, want) {
  const ctx = await br.createBrowserContext(), p = await ctx.newPage(); await p.setViewport({ width: 390, height: 844 });
  let loads = 0, bjs = 0; const pe = [];
  p.on('load', () => loads++); p.on('pageerror', e => pe.push(e.message));
  if (noSS) await p.evaluateOnNewDocument(() => { const t = () => { throw new Error('blockerad') }; Storage.prototype.getItem = t; Storage.prototype.setItem = t });
  if (build) {
    await p.setBypassServiceWorker(true); // annars går omladdningens build.js via service workern förbi interceptionen
    await p.setRequestInterception(true);
    p.on('request', r => {
      if (!r.url().includes('/build.js')) return r.continue();
      const b = build(++bjs);
      return b == null ? r.continue() : b === 404 ? r.respond({ status: 404, body: '' }) : r.respond({ status: 200, contentType: 'text/javascript', body: `window.GURKA_BUILD="${b}";` });
    });
  }
  await open(p, base + '/'); await sleep(3500);
  const r = await p.evaluate(() => { const b = document.getElementById('verBanner'); return { banner: b && !b.hidden, text: b ? b.textContent : '', conn: typeof hub !== 'undefined' && hub.state } });
  console.log(`${name.padEnd(34)} laddningar=${loads} banner=${r.banner} ${r.conn}${pe.length ? ' SIDFEL: ' + pe.join('|') : ''}`);
  if (loads !== want.loads) errs.push(`${name}: ${loads} laddningar, väntade ${want.loads}`);
  if (r.banner !== want.banner) errs.push(`${name}: banner=${r.banner}, väntade ${want.banner}`);
  if (r.conn !== 'Connected') errs.push(`${name}: hubben är ${r.conn}`);
  if (pe.length) errs.push(`${name}: sidfel ${pe.join('|')}`);
  await p.screenshot({ path: `${SHOTS}/ver-${name.split(' ')[0]}.png` }); await ctx.close();
}
(async () => {
  const br = await launch();
  await run(br, 'A lika', BASE, {}, { loads: 1, banner: false });
  await run(br, 'B gammal sida (fel bara 1:a)', BASE, { build: n => n === 1 ? OLD : null }, { loads: 2, banner: false });
  await run(br, 'C gammal process (alltid fel)', BASE, { build: () => OLD }, { loads: 2, banner: true });
  await run(br, 'D build.js 404', BASE, { build: () => 404 }, { loads: 2, banner: false }); // 404 bevisar inget: en omladdning, sedan ingen banner
  await run(br, 'E sessionStorage kastar + fel', BASE, { build: () => OLD, noSS: true }, { loads: 1, banner: true });
  await run(br, 'G sessionStorage kastar + 404', BASE, { build: () => 404, noSS: true }, { loads: 1, banner: false });
  if (process.argv[2]) await run(br, 'F server före handslaget', process.argv[2], {}, { loads: 1, banner: true }); // Hello saknar build
  console.log(errs.length ? errs.join('\n') : 'Inga fel.');
  await br.close();
  process.exit(errs.length ? 1 : 0);
})().catch(e => { console.error('KRASCH', e); process.exit(1) });
