// Spelar hela omgångar med N klienter via gränssnittet (klick på kort och knappar) och loggar fel.
const { launch, open, BASE, SHOTS } = require('./lib');
const N = +process.argv[2] || 2, ROUNDS = +process.argv[3] || 1;
let ROOM = process.env.ROOM || '';
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const br = await launch({ args: ['--autoplay-policy=no-user-gesture-required'] });
  const pages = [], errs = [];
  if (!ROOM) { const c = await br.createBrowserContext(), q = await c.newPage(); await open(q, BASE + '/'); await q.waitForFunction(() => typeof S !== 'undefined' && S); ROOM = await q.evaluate(() => hub.invoke('NewRoom')); await c.close(); console.log('Bord', ROOM) }
  for (let i = 0; i < N; i++) {
    const ctx = await br.createBrowserContext(), p = await ctx.newPage();
    await p.setViewport({ width: [390, 844, 1440, 768][i % 4], height: [844, 390, 900, 1024][i % 4] });
    p.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') errs.push(`P${i} console ${m.type()}: ${m.text()}`) });
    p.on('pageerror', e => errs.push(`P${i} pageerror: ${e.message}`));
    await open(p, `${BASE}/?bord=${ROOM}`);
    await p.waitForFunction(() => window.S || (typeof S !== 'undefined' && S), { timeout: 8000 });
    await p.waitForSelector('#nick',{timeout:5000}).catch(async()=>{console.log('ingen #nick',await p.evaluate(()=>JSON.stringify({ph:S.phase,you:S.you,room:S.room,seats:S.seats,lobby:document.getElementById('lobby').innerText,toast:document.getElementById('toast').innerText})))});
    await p.type('#nick', 'Spelare' + (i + 1)); await p.click('#lobby .join button');
    await sleep(200); pages.push(p);
  }
  await pages[0].waitForSelector('#lobby [data-a=start]'); await pages[0].click('#lobby [data-a=start]');
  // fånga felmeddelanden från servern
  for (const [i, p] of pages.entries()) await p.evaluate(i => { hub.on('err', m => (window.__errs ||= []).push(m)) }, i);
  let rounds = 0, steps = 0, phases = new Set();
  while (rounds < ROUNDS && steps < 2000) {
    steps++; let acted = false;
    for (const [i, p] of pages.entries()) {
      const st = await p.evaluate(() => ({ ph: S.phase, my: myTurn(), zl: S.zlMe, free: S.free, exn: S.exn, deck: S.deck, up: S.up }));
      phases.add(st.ph);
      if (st.ph === 'over') continue;
      const clickN = async (n) => { const cs = await p.$$('#hand .card'); for (let k = 0; k < n; k++) await cs[k].click() };
      if (st.ph === 'zlatan' && st.zl) { await p.click(Math.random() < .5 ? '[data-a=zy]:not(:disabled)' : '[data-a=zn]').catch(() => p.click('[data-a=zn]')); acted = true }
      else if (!st.my) continue;
      else if (st.ph === 'exchange') { await clickN(Math.floor(Math.random() * 4)); await p.click('[data-a=x]'); acted = true }
      else if (st.ph === 'respond' && st.free) {
        const r = Math.random();
        if (r < .5 && st.deck >= st.exn) { await clickN(st.exn); await p.click('[data-a=w]') }
        else if (r < .7 && await p.$('[data-a=wa]')) await p.click('[data-a=wa]');
        else await p.click('[data-a=wk]'); acted = true
      }
      else if (st.ph === 'oneup') { // 1 upp: ta/tacka nej, ja/nej på frågan, och på egen tur 1 upp, alla 7 eller behåll
        const r = Math.random(), u = st.up;
        if (u.stage === 'decide') await p.click(r < .5 ? '[data-a=ut]' : '[data-a=un]');
        else if (u.stage === 'offer') { if (r < .3) { await p.click('[data-a=uj]'); await clickN(1); await p.click('[data-a=uy]') } else await p.click('[data-a=un]') }
        else if (st.deck === 0) await p.click(r < .5 && await p.$('[data-a=ud]') ? '[data-a=ud]' : '[data-a=rk]');
        else if (r < .5) { await clickN(1); await p.click('[data-a=up]') }
        else await p.click(r < .7 && st.deck >= 7 ? '[data-a=ra]' : '[data-a=rk]');
        acted = true
      }
      else if (st.ph === 'respond') { await p.click(st.deck >= 7 && Math.random() < .3 ? '[data-a=ra]' : '[data-a=rk]'); acted = true }
      else if (st.ph === 'play') {
        // välj en giltig kombination via gränssnittets egna spegling och klicka korten
        const pick = await p.evaluate(() => { const L = legalSets(); const m = L[Math.floor(Math.random() * L.length)]; return m });
        if (pick && Math.random() < .85) {
          for (const ix of pick) await p.click(`#hand .card[data-i="${ix}"]`);
          const ok = await p.evaluate(() => { const b = document.querySelector('[data-a=play]'); return b && !b.disabled });
          if (ok) await p.click('[data-a=play]'); else { errs.push(`P${i} play-knappen inaktiv för giltigt val ${pick} ` + JSON.stringify(await p.evaluate(()=>({sel:[...sel],lead:lead(),count:S.count,hand:S.hand,top:topRs(),act:document.getElementById("actions").innerText})))); await p.click('[data-a=sak]') }
        } else await p.click('[data-a=sak]').catch(async () => { for (const ix of pick) await p.click(`#hand .card[data-i="${ix}"]`); await p.click('[data-a=play]') });
        acted = true
      }
      await sleep(120);
    }
    const ph = await pages[0].evaluate(() => S.phase);
    if (ph === 'over') {
      rounds++; await sleep(1500);
      const res = await pages[0].evaluate(() => ({ res: !document.getElementById('result').hidden, txt: document.getElementById('result').innerText.replace(/\s+/g, ' ') }));
      console.log('Omgång', rounds, 'klar. Resultatruta:', res.res, res.txt.slice(0, 120));
      if (res.txt.includes('Oavgjort')) { const d = await pages[0].evaluate(() => ({ w: S.winner, g: S.gurka, p: S.points, sc: S.seats.filter(x => x).map(x => x.score), last: S.trick.map(q => q.cards.map(c => c.r)), msg: document.getElementById('msg').innerText, conf: document.querySelectorAll('.cf').length })); console.log('  OAVGJORT', JSON.stringify(d), 'poäng före', JSON.stringify(globalThis.prevSc)); await pages[0].screenshot({ path: SHOTS + '/draw-real.png' }) }
      globalThis.prevSc = await pages[0].evaluate(() => S.seats.filter(x => x).map(x => x.score));
      if (rounds < ROUNDS) { await pages[0].click('#result [data-a=start]'); await sleep(400) }
    }
    if (!acted) await sleep(150);
  }
  for (const [i, p] of pages.entries()) { const e = await p.evaluate(() => window.__errs || []); e.forEach(m => errs.push(`P${i} server-err: ${m}`)) }
  // horisontell scroll?
  for (const [i, p] of pages.entries()) { const o = await p.evaluate(() => [document.documentElement.scrollWidth, innerWidth]); if (o[0] > o[1]) errs.push(`P${i} horisontell overflow ${o}`) }
  console.log('Faser:', [...phases].join(','), 'steg:', steps);
  console.log(errs.length ? errs.join('\n') : 'Inga fel.');
  await br.close();
})().catch(e => { console.error('KRASCH', e); process.exit(1) });
