// 1 upp end-to-end via gränssnittet. Arg: N scenario. Scenario: starter=take|pass, svar på frågor (y/n i ordning), val för övriga ägare.
const { launch, open, BASE, SHOTS: OUT } = require('./lib');
const N = +process.argv[2] || 2, SC = process.argv[3] || 'take';
const SCN = {
  take:    { st: 'take', ans: [], own: ['up-pass', 'all7', 'keep'] },            // startspelaren tar; nästa kör 1 upp och tackar nej, alla nej
  other:   { st: 'pass', ans: ['n', 'y'], own: ['all7', 'keep', 'keep'] },        // startspelaren nej, 2:a tillfrågade tar (4p) / 1:a (2p: y)
  allno:   { st: 'pass', ans: ['n', 'n', 'n'], own: ['keep', 'up-take', 'keep'] },
  other2:  { st: 'pass', ans: ['y'], own: ['keep'] },                              // 2p: den andra tar och har då förbrukat sin tur
  keep2:   { st: 'take', ans: [], own: ['up-pass'], ans2: ['n'] },
}[SC];
if (!SCN) { console.error('Okänt scenario: ' + SC + ' (take|other|allno|other2|keep2)'); process.exit(2) }
const SIZES = [[360, 640], [844, 390], [1440, 900], [390, 844]];
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const br = await launch();
  const errs = [], pages = [], log = [];
  const c0 = await br.createBrowserContext(), q = await c0.newPage(); await open(q, BASE + '/');
  await q.waitForFunction(() => typeof S !== 'undefined' && S); const ROOM = await q.evaluate(() => hub.invoke('NewRoom')); await c0.close();
  for (let i = 0; i < N; i++) {
    const ctx = await br.createBrowserContext(), p = await ctx.newPage(); const [w, h] = SIZES[i % 4];
    await p.setViewport({ width: w, height: h });
    p.on('console', m => { if (m.type() === 'error') errs.push(`P${i} console: ${m.text()}`) });
    p.on('pageerror', e => errs.push(`P${i} pageerror: ${e.message}`));
    await open(p, `${BASE}/?bord=${ROOM}`);
    await p.waitForSelector('#nick'); await p.type('#nick', ['Anna', 'Bo', 'Cecilia', 'Dag'][i]); await p.click('#lobby .join button'); await sleep(250);
    await p.evaluate(() => hub.on('err', m => (window.__errs ||= []).push(m)));
    pages.push(p);
  }
  await pages[0].waitForSelector('#lobby [data-a=start]'); await pages[0].click('#lobby [data-a=start]'); await sleep(500);
  const seen = new Set(); let ans = [...SCN.ans], own = [...SCN.own], shots = new Set(), steps = 0, spentSeen = false;
  const st = p => p.evaluate(() => ({ ph: S.phase, you: S.you, my: myTurn(), zl: S.zlMe, up: S.up, deck: S.deck, hand: S.hand, n: S.seats.map(x => x && x.n),
    act: document.getElementById('actions').innerText.replace(/\s+/g, ' '), msg: document.getElementById('msg')?.innerText.replace(/\s+/g, ' '), status: document.getElementById('status').innerText,
    sp: [...document.querySelectorAll('.plate.sp')].map(e => +e.dataset.s), ud: [...document.querySelectorAll('.av.ud')].length, upc: !!document.querySelector('#msg .card.up'), pub: [...S.reveal, ...(S.tie || [])] }));
  const shot = async (p, i, tag) => { const k = tag + i; if (shots.has(k)) return; shots.add(k); await p.screenshot({ path: `${OUT}/e-${SC}-${N}p-P${i}-${tag}.png` }) };
  const ck = c => (+c.r) + '-' + (+c.s);
  while (steps++ < 300) {
    const S_ = await Promise.all(pages.map(st));
    if (S_[0].ph === 'play') break;
    // uppvända kort är offentliga
    S_.forEach(s => { if (s.up?.card) seen.add(ck(s.up.card)); (s.pub || []).forEach(c => seen.add(ck(c))) });
    // läckage: ingen annan spelares handkort (utom offentliga upp-kort) får finnas i någon annans tillstånd eller bord/chatt
    for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) if (i !== j) for (const c of S_[i].hand || []) {
      const k = ck(c); if (seen.has(k)) continue;
      const leak = await pages[j].evaluate(k => { const [r, s] = k.split('-').map(Number), js = JSON.stringify({ ...S, hand: null });
        return js.includes(`"r":${r},"s":${s}`) || !!document.querySelector(`#felt [data-k="${k}"],#actions [data-k="${k}"]`) || document.getElementById('log').textContent.includes(cn({ r, s })) }, k);
      if (leak) errs.push(`LÄCKA: P${i}:s kort ${k} syns för P${j} (fas ${S_[0].ph} ${JSON.stringify(S_[0].up)})`);
    }
    let acted = false;
    for (const [i, p] of pages.entries()) {
      const s = S_[i];
      if (s.ph === 'oneup' && s.up) {
        if (s.up.card && !s.upc) errs.push(`P${i} ser inte upp-kortet i ${s.up.stage}`);
        if (s.sp.length) spentSeen = true;
        await shot(p, i, s.up.stage + (s.my ? '-me' : ''));
        if (s.up.mark >= 0 && s.up.owner !== s.you) errs.push(`P${i} får mark trots att den inte är ägare`);
      }
      if (s.ph === 'zlatan' && s.zl) { await p.click('[data-a=zn]'); acted = true; log.push(`P${i} Zlatan nej`); break }
      if (!s.my) continue;
      if (s.ph === 'exchange') {
        await shot(p, i, 'exchange');
        await p.click('#hand .card:nth-child(2)'); const lbl = await p.$eval('[data-a=x]', b => b.textContent);
        if (lbl !== '1 upp') errs.push(`P${i} exchange-knapp med 1 markerat: "${lbl}"`);
        await p.click('[data-a=x]'); log.push(`P${i} exchange 1 upp`); acted = true; break
      }
      if (s.ph !== 'oneup') { errs.push(`P${i} oväntad fas ${s.ph}: ${s.act}`); continue }
      const u = s.up;
      if (u.stage === 'decide') {
        const first = u.owner === (await p.evaluate(() => S.starter));
        const take = first ? SCN.st === 'take' : SCN._next === 'take';
        await p.click(take ? '[data-a=ut]' : '[data-a=un]'); log.push(`P${i} decide ${take ? 'tar' : 'nej'} (${s.n.join('/')})`);
        if (!take) SCN._pass = i; acted = true; break
      }
      if (u.stage === 'offer') {
        const a = (ans.shift() || 'n');
        if (a === 'y') {
          await p.click('[data-a=uj]'); await sleep(150);
          const dis = await p.$eval('[data-a=uy]', b => b.disabled); if (!dis) errs.push(`P${i} släng-knappen aktiv utan markering`);
          await p.click('#hand .card:nth-child(1)'); await sleep(100); await shot(p, i, 'offer-yes');
          const before = s.hand.map(ck); await p.click('[data-a=uy]'); log.push(`P${i} offer ja`);
        } else { await p.click('[data-a=un]'); log.push(`P${i} offer nej`) }
        acted = true; break
      }
      if (u.stage === 'pick') {
        const o = own.shift() || 'keep';
        if (o === 'all7' && s.deck < 7) { await p.click('[data-a=rk]'); log.push(`P${i} pick behåll (leken räckte inte)`) }
        else if (o === 'all7') { await p.click('[data-a=ra]'); log.push(`P${i} pick alla 7`) }
        else if (o === 'keep') { await p.click('[data-a=rk]'); log.push(`P${i} pick behåll`) }
        else {
          const dis = await p.$eval('[data-a=up]', b => b.disabled); if (!dis) errs.push(`P${i} 1 upp-knappen aktiv utan markering`);
          await p.click('#hand .card:nth-child(3)'); await sleep(100); SCN._next = o === 'up-take' ? 'take' : 'pass';
          if (SCN.ans2) ans = [...SCN.ans2];
          await p.click('[data-a=up]'); log.push(`P${i} pick 1 upp (${SCN._next})`)
        }
        acted = true; break
      }
    }
    await sleep(acted ? 450 : 200);
  }
  const fin = await Promise.all(pages.map(st));
  for (const [i, p] of pages.entries()) { (await p.evaluate(() => window.__errs || [])).forEach(m => errs.push(`P${i} server-err: ${m}`));
    const o = await p.evaluate(() => [document.documentElement.scrollWidth, innerWidth]); if (o[0] > o[1]) errs.push(`P${i} horisontell scroll ${o}`) }
  const chat = await pages[0].evaluate(() => [...document.querySelectorAll('#log div')].map(d => d.textContent).join('|'));
  console.log(`== ${SC} ${N}p bord ${ROOM}`); console.log(log.join('\n'));
  console.log('slutfas', fin[0].ph, 'kort:', fin[0].n.join('/'), 'förbrukad-markering sedd:', spentSeen);
  console.log('chatt:', chat.split('|').filter(l => l.startsWith('Bordet')).join(' | '));
  console.log(errs.length ? errs.join('\n') : 'Inga fel.');
  await br.close();
})().catch(e => { console.error('KRASCH', e); process.exit(1) });
