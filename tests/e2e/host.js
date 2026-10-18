// Startsida utan bord, skapa bord (värd), gå med via länk och kod, bara värden startar, värden lämnar, ny giv, timeout-vy.
const { launch, open, ready, BASE: B, SHOTS } = require('./lib');
const SH = SHOTS + '/h-';
const sleep = ms => new Promise(r => setTimeout(r, ms)), errs = [], ok = (c, m) => { if (!c) errs.push(m); console.log((c ? 'OK  ' : 'FEL ') + m) };
(async () => {
  const br = await launch();
  const mk = async (w, h, url) => { const p = await (await br.createBrowserContext()).newPage(); await p.setViewport({ width: w, height: h }); p.on('pageerror', e => errs.push('pageerror ' + e.message)); p.on('console', m => m.type() !== 'info' && console.log('  [' + w + '] ' + m.text()));
    await open(p, url); await ready(p); await sleep(600); await p.evaluate(() => { hub.on('err', m => { (window.__e ||= []).push(m); console.log('ERR ' + m) }); document.addEventListener('submit', e => console.log('SUBMIT ' + e.target.dataset.f), true) }); return p };
  const info = p => p.evaluate(() => ({ url: location.search, lobby: !document.getElementById('lobby').hidden, lt: document.getElementById('lobby').innerText.replace(/\s+/g, ' '), status: document.getElementById('status').textContent,
    start: !!document.querySelector('[data-a=start]'), act: document.getElementById('actions').innerText.replace(/\s+/g, ' '), crowns: (() => { const hs = [...document.querySelectorAll('.host')], hn = S && S.host >= 0 ? S.seats[S.host].name : null;
      return { n: hs.length, stray: hs.filter(e => { const pl = e.closest('.plate'); if (pl) return +pl.dataset.s !== S.host; const sl = e.closest('.slot'); return !sl || !hn || !sl.textContent.includes(hn) }).map(e => (e.closest('.plate,.slot') || e).textContent.replace(/\s+/g, ' ').trim()) } })(), S: typeof S !== 'undefined' && S ? { ph: S.phase, you: S.you, host: S.host } : null, hs: document.documentElement.scrollWidth > innerWidth }));
  // 1. startsida i tre storlekar
  const A = await mk(360, 640, B + '/');
  for (const [w, h] of [[360, 640], [844, 390], [1440, 900]]) { await A.setViewport({ width: w, height: h }); await sleep(300); const i = await info(A);
    ok(i.lobby && /Skapa bord/.test(i.lt) && /Bordskod/.test(await A.$eval('#lobby', e => e.innerHTML)) && !i.S && !i.hs, `${w}x${h} startsida: skapa + kod, inget bord, ingen hscroll`); await A.screenshot({ path: SH + `landing-${w}x${h}.png` }) }
  await A.setViewport({ width: 360, height: 640 }); await sleep(800); // sidopanelen glider ut (.28 s) när bredden går från bred till smal
  // 2. skapa bord
  await A.$eval('#nick', e => e.value = ''); await A.type('#nick', 'Anna'); console.log('mål:', await A.evaluate(() => { const b = document.querySelector('[data-f=create] button'), x = b.getBoundingClientRect(), t = document.elementFromPoint(x.left + x.width / 2, x.top + x.height / 2); return [Math.round(x.left), Math.round(x.top), innerWidth, innerHeight, t && t.outerHTML.slice(0, 60)] })); await A.click('[data-f=create] button'); await sleep(1200);
  console.log('A:', await A.evaluate(() => JSON.stringify({room, S:!!S, btn:document.querySelector('[data-f=create] button')?.disabled, nick:document.getElementById('nick')?.value, st:hub.state})));
  let a = await info(A); const code = (a.url.match(/bord=([A-Z0-9]+)/) || [])[1] || '';
  ok(code.length === 8, `kod i URL med 8 tecken: ${code}`); ok(a.S && a.S.you >= 0 && a.S.host === a.S.you, `skaparen sitter och är värd (${JSON.stringify(a.S)})`);
  ok(!a.start && /Väntar på fler/.test(a.lt), 'ensam värd: ingen startknapp, väntar på fler'); ok(a.crowns.n >= 1 && a.crowns.stray.length === 0, `kronan bara hos värden (${a.crowns.n} st, fel ställe: ${a.crowns.stray.join(' | ') || 'inga'})`);
  // 3. Bo via länk
  const Bo = await mk(844, 390, `${B}/?bord=${code}`); await Bo.type('#nick', 'Bo'); await Bo.click('#lobby .join button'); await sleep(900);
  a = await info(A); let b = await info(Bo);
  ok(a.start, 'värden ser Starta spelet med 2'); ok(!b.start && /Väntar på att Anna startar spelet/.test(b.lt), 'Bo ser ingen start och "Väntar på att Anna startar spelet"');
  await Bo.screenshot({ path: SH + 'lobby-bo-844x390.png' }); await A.screenshot({ path: SH + 'lobby-anna-360x640.png' });
  // 4. Cecilia via kod på startsidan
  const Ce = await mk(1440, 900, B + '/'); await Ce.type('[data-f=room] input', code.toLowerCase()); await Ce.click('[data-f=room] button'); await sleep(900);
  let c = await info(Ce); ok(c.url.includes(code) && c.S && c.S.ph === 'lobby' && c.S.you < 0, 'Cecilia gick med via kod (gemener) och ser lobbyn');
  await Ce.$eval('#nick', e => e.value = ''); await Ce.type('#nick', 'Cecilia'); await Ce.click('#lobby .join button'); await sleep(900); c = await info(Ce);
  ok(c.S.you >= 0 && !c.start, 'Cecilia sitter, ingen startknapp'); await Ce.screenshot({ path: SH + 'lobby-ce-1440x900.png' });
  // 5. värden lämnar i lobbyn
  const hostSeat = a.S.host; await A.evaluate(() => hub.invoke('Leave')); await sleep(900);
  b = await info(Bo); c = await info(Ce); const nh = b.S.host;
  ok(nh >= 0 && nh !== hostSeat, `värdskapet gick vidare (${hostSeat} -> ${nh})`);
  ok(b.crowns.n >= 1 && b.crowns.stray.length === 0 && c.crowns.stray.length === 0, `kronan flyttade med till den nya värden (fel ställe: ${[...b.crowns.stray, ...c.crowns.stray].join(' | ') || 'inga'})`);
  const H = b.S.you === nh ? Bo : Ce, O = H === Bo ? Ce : Bo, hn = H === Bo ? 'Bo' : 'Cecilia';
  ok((await info(H)).start && !(await info(O)).start && new RegExp(`Väntar på att ${hn} startar`).test((await info(O)).lt), `ny värd ${hn} ser start, den andra väntar på ${hn}`);
  await H.screenshot({ path: SH + 'newhost.png' });
  // 6. icke-värd försöker starta via hubben
  await O.evaluate(() => hub.invoke('Start')); await sleep(400); ok((await O.evaluate(() => window.__e || [])).some(m => /Bara värden/.test(m)), 'icke-värd Start ger "Bara värden kan starta spelet."');
  // 7. värden startar, spela till slut via hubben
  await H.evaluate(() => hub.invoke('Start')); await sleep(800);
  for (let k = 0; k < 600; k++) { let over = false;
    for (const p of [Bo, Ce]) { const r = await p.evaluate(async () => { const s = S; if (s.phase === 'over') return 'over'; if (s.phase === 'zlatan' && s.zlMe) { await hub.invoke('Zlatan', false); return 1 } if (!myTurn()) return 0;
      if (s.phase === 'exchange') await hub.invoke('Exchange', []); else if (s.phase === 'respond') await (s.free ? hub.invoke('Swap', [], false) : hub.invoke('Respond', false)); else if (s.phase === 'play') { const L = legalSets(); await hub.invoke('PlayCards', L[0]) } return 1 }); if (r === 'over') over = true }
    if (over) break; await sleep(60) }
  await sleep(1800); let h = await info(H), o = await info(O);
  ok(h.S.ph === 'over' && h.start, 'efter omgången: värden har Spela igen'); ok(!o.start && new RegExp(`Väntar på att ${hn} startar nästa giv`).test(o.act + ' ' + await O.$eval('#result', e => e.innerText)), 'den andra: ingen Spela igen, väntar på nästa giv');
  await O.screenshot({ path: SH + 'over-other.png' }); await H.screenshot({ path: SH + 'over-host.png' });
  // 8. okänd kod
  const X = await mk(390, 844, B + '/?bord=ZZZZZZZZ'); await sleep(500); const x = await info(X);
  ok(x.lobby && /Skapa bord/.test(x.lt) && !x.S && x.url === '', 'okänd kod: startsidan och URL utan kod');
  // 9. timeout-vyn (simulerad: anropar klientens hanterare för serverns felmeddelande)
  await X.evaluate(() => asleep('Frånkopplad efter 10 minuter utan aktivitet – ladda om sidan för att fortsätta.')); await sleep(4000);
  const z = await X.evaluate(() => ({ st: hub.state, btn: !!document.querySelector('#lobby [data-a=reload]'), t: document.getElementById('lobby').innerText.replace(/\s+/g, ' ') }));
  ok(z.st === 'Disconnected' && z.btn, `timeout: frånkopplad utan återanslutning, Ladda om-knapp (${z.st})`); await X.screenshot({ path: SH + 'idle-390x844.png' });
  // 10. Till startsidan från lobbyn
  await Ce.evaluate(() => A.home()); await sleep(800); const hm = await info(Ce); ok(hm.lobby && /Skapa bord/.test(hm.lt) && hm.url === '' && !hm.S, 'Till startsidan: landningsvyn');
  console.log(errs.length ? 'FEL:\n' + errs.join('\n') : 'Inga fel.'); await br.close(); process.exit(errs.length ? 1 : 0)
})().catch(e => { console.error('KRASCH', e); process.exit(1) });
