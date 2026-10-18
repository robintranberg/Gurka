// Renderar View-fixturerna i fixtures/ i flera storlekar och skriver ut msg/actions/status.
const {launch,open,ready,BASE,SHOTS:OUT}=require('./lib'),fs=require('fs'),D=process.env.FIXTURES||__dirname+'/fixtures';
(async()=>{const br=await launch();
const p=await br.newPage(),errs=[];p.on('pageerror',e=>errs.push(e.message));
await open(p, BASE+'/');await ready(p);await p.evaluate(()=>hub.off('state'));
for(const f of fs.readdirSync(D).filter(x=>x.endsWith('.json')))for(const [w,h] of [[360,640],[844,390],[1440,900]]){
 await p.setViewport({width:w,height:h});const st=JSON.parse(fs.readFileSync(D+'/'+f,'utf8'));
 const r=await p.evaluate(async st=>{st.v=Date.now();S=st;render(null);await new Promise(r=>setTimeout(r,500));const t=id=>document.getElementById(id).textContent.replace(/\s+/g,' ').trim();
  return{msg:t('msg'),act:t('actions'),status:t('status'),btn:[...document.querySelectorAll('#actions button')].map(b=>b.dataset.a+(b.disabled?'(av)':'')).join(','),hs:document.documentElement.scrollWidth>innerWidth,hand:document.querySelectorAll('#hand .card').length}},st);
 console.log(`${f} ${w}x${h}\n  status: ${r.status}\n  msg: ${r.msg}\n  act: ${r.act}\n  knappar: ${r.btn} hand=${r.hand} hscroll=${r.hs}`);
 await p.screenshot({path:`${OUT}/fix-${f.replace('.json','')}-${w}x${h}.png`})}
console.log(errs.length?'FEL: '+errs.join('|'):'Inga sidfel.');await br.close()})().catch(e=>{console.error("KRASCH",e);process.exit(1)});
