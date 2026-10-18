// Spelar omgångar via hubben (gynnar flerkortsutspel) och jämför serverns gurka/vinnare/poäng och resultatrutan med summaregeln. Arg: [spelare] [rundor]
const {launch,open,ready,BASE:B,SHOTS}=require('./lib');
const N=+process.argv[2]||2,ROUNDS=+process.argv[3]||8,sleep=ms=>new Promise(r=>setTimeout(r,ms)),SH=SHOTS+'/';
(async()=>{const br=await launch();const P=[],errs=[];
const c0=await br.createBrowserContext(),q=await c0.newPage();await open(q,B+'/');await ready(q);const ROOM=await q.evaluate(()=>hub.invoke('NewRoom'));await c0.close();
for(let i=0;i<N;i++){const p=await(await br.createBrowserContext()).newPage();await p.setViewport({width:[360,844,1440,390][i],height:[640,390,900,844][i]});p.on('pageerror',e=>errs.push(e.message));
 await open(p,`${B}/?bord=${ROOM}`);await p.waitForSelector('#nick');await p.type('#nick','S'+i);await p.click('#lobby .join button');await sleep(300);await p.evaluate(()=>hub.on('err',m=>(window.__e||=[]).push(m)));P.push(p)}
await P[0].evaluate(()=>hub.invoke('Start'));let done=0,multi=0;
for(let k=0;k<4000&&done<ROUNDS;k++){let acted=false;
 for(const p of P){const a=await p.evaluate(async()=>{const s=S;if(s.phase==='over')return'over';
  if(s.phase==='zlatan'&&s.zlMe){await hub.invoke('Zlatan',false);return 1}if(!myTurn())return 0;
  if(s.phase==='exchange'){await hub.invoke('Exchange',[]);return 1}if(s.phase==='respond'){await(s.free?hub.invoke('Swap',[],false):hub.invoke('Respond',false));return 1}
  if(s.phase==='play'){const L=legalSets();let m;if(lead()){const h=s.hand,c={};h.forEach(x=>c[x.r]=(c[x.r]||0)+1);const t=+Object.keys(c).sort((a,b)=>c[b]-c[a])[0];
   m=h.every(x=>x.r===t)?h.map((x,i)=>i):[h.map((x,i)=>[x.r,i]).filter(x=>x[0]!==t).sort((a,b)=>a[0]-b[0])[0][1]]}else m=L[Math.floor(Math.random()*L.length)];await hub.invoke('PlayCards',m);return 1}return 0});
  if(a===1)acted=true}
 await sleep(acted?60:120);
 if(await P[0].evaluate(()=>S.phase==='over')){await sleep(1800);
  for(const [i,p] of P.entries()){const r=await p.evaluate(()=>{const s=S,m=s.trick.map((q,i)=>({i,seat:q.seat,v:q.cards.reduce((a,c)=>a+c.r,0)})),lo=Math.min(...m.map(x=>x.v)),hi=Math.max(...m.map(x=>x.v));
   const exp=m.filter(x=>x.v===lo).length>1?{g:-1,w:-1,p:0}:{g:m.filter(x=>x.v===hi).at(-1).seat,w:m.find(x=>x.v===lo).seat,p:hi*5};
   const R=document.getElementById('result');return{exp,got:{g:s.gurka,w:s.winner,p:s.points},cnt:s.count,open:!R.hidden,txt:R.innerText.replace(/\s+/g,' ').slice(0,170),sums:!!R.querySelector('.sums')}});
   if(i===0){if(r.cnt>1)multi++;console.log(`#${done+1} kort=${r.cnt} server=${JSON.stringify(r.got)} väntat=${JSON.stringify(r.exp)} | ${r.txt}`)}
   if(JSON.stringify(r.exp)!==JSON.stringify(r.got))errs.push(`#${done+1} P${i}: avvikelse ${JSON.stringify(r)}`);
   if(!r.open)errs.push(`#${done+1} P${i}: resultatrutan inte öppen`);if(r.sums!==(r.cnt>1))errs.push(`#${done+1} P${i}: summor visas=${r.sums} men kort=${r.cnt}`);
   if(r.cnt>1&&i===0)await p.screenshot({path:SH+'sum-real-'+(done+1)+'.png'})}
  done++;if(done<ROUNDS){await P[0].evaluate(()=>hub.invoke('Start'));await sleep(500)}}}
for(const [i,p] of P.entries())(await p.evaluate(()=>window.__e||[])).forEach(m=>errs.push(`P${i} server-err: ${m}`));
console.log('rundor',done,'med flera kort:',multi);console.log(errs.length?errs.join('\n'):'Inga fel.');await br.close()})().catch(e=>{console.error('KRASCH',e);process.exit(1)});
