'use strict';
// Gurka-klienten. Servern (GameHub.cs) är facit för reglerna – här speglas de bara för att visa vad som går.
const $=i=>document.getElementById(i),esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const SU='♣♦♥♠',SN=['klöver','ruter','hjärter','spader'],RN=r=>r<11?String(r):['knekt','dam','kung','ess'][r-11];
const RK=r=>r<11?r:['Kn','D','K','E'][r-11],isRed=c=>c.s==1||c.s==2,key=c=>(+c.r)+'-'+(+c.s),cn=c=>SN[c.s]+' '+RN(c.r);
const RM=matchMedia('(prefers-reduced-motion: reduce)'),WIDE=matchMedia('(min-width:1000px) and (min-height:560px)');
const ls={get(k,d){try{const v=localStorage.getItem('gurka.'+k);return v==null?d:JSON.parse(v)}catch{return d}},set(k,v){try{localStorage.setItem('gurka.'+k,JSON.stringify(v))}catch{}}};
const ss={get(k){try{return sessionStorage.getItem('gurka.'+k)}catch{return null}},set(k,v){try{v==null?sessionStorage.removeItem('gurka.'+k):sessionStorage.setItem('gurka.'+k,v)}catch{}}};

// ---------- kort ----------
const ix=c=>`<span class="ix"><b>${RK(+c.r)}</b><i>${SU[c.s]||''}</i></span><span class="pip">${SU[c.s]||''}</span>`;
const card=(c,x='')=>`<div class="card ${isRed(c)?'red':''} ${x}" data-k="${key(c)}">${ix(c)}</div>`;
const mini=(c,sk)=>`<div class="mc ${isRed(c)?'red':''} ${sk?'sk':''}" title="${cn(c)}">${RK(+c.r)}<i>${SU[c.s]||''}</i></div>`;
// avgörande vid lika visat kort: extrakorten per runda, vinnarens sista markeras
const tbk=(t,w)=>t.length?`<div class="tb"><span class="sr">Avgörande:</span>${t.sort((a,b)=>a.round-b.round).map((c,k)=>`<div class="mc ${isRed(c)?'red':''} ${w&&k===t.length-1?'hi':''}" title="${esc(`Avgörande ${+c.round}: ${cn(c)}`)}">${RK(+c.r)}<i>${SU[c.s]||''}</i></div>`).join('')}</div>`:'';
const ini=n=>esc([...String(n||'?').trim()][0]||'?').toUpperCase();

// ---------- ljud (syntetiskt via Web Audio, inga filer) och haptik ----------
let AC=null,snd=ls.get('snd',true);
function au(){if(!snd)return null;try{AC=AC||new(window.AudioContext||window.webkitAudioContext)();if(AC.state==='suspended')AC.resume();return AC}catch{return null}}
function tone(f,t0,d,type='sine',g=.1,f2){const a=au();if(!a)return;const o=a.createOscillator(),v=a.createGain(),t=a.currentTime+t0;o.type=type;o.frequency.setValueAtTime(f,t);if(f2)o.frequency.exponentialRampToValueAtTime(f2,t+d);
 v.gain.setValueAtTime(.0001,t);v.gain.exponentialRampToValueAtTime(g,t+.012);v.gain.exponentialRampToValueAtTime(.0001,t+d);o.connect(v).connect(a.destination);o.start(t);o.stop(t+d+.05)}
function snap(t0=0,g=.55){const a=au();if(!a)return;const n=a.sampleRate*.07|0,b=a.createBuffer(1,n,a.sampleRate),d=b.getChannelData(0);for(let i=0;i<n;i++)d[i]=(Math.random()*2-1)*(1-i/n)**4;
 const s=a.createBufferSource(),f=a.createBiquadFilter(),v=a.createGain();s.buffer=b;f.type='bandpass';f.frequency.value=1600+Math.random()*900;f.Q.value=.9;v.gain.value=g;s.connect(f).connect(v).connect(a.destination);s.start(a.currentTime+t0)}
const SFX={card:()=>snap(),tick:()=>snap(0,.18),deal:()=>{for(let i=0;i<7;i++)snap(i*.075,.4)},collect:()=>{snap(0,.35);snap(.06,.3);snap(.12,.25)},
 turn:()=>{tone(660,0,.2,'sine',.07);tone(990,.11,.32,'sine',.06)},err:()=>tone(170,0,.2,'square',.035),chat:()=>tone(1180,0,.09,'sine',.035),
 win:()=>[523,659,784,1047,1319].forEach((f,i)=>tone(f,i*.1,.4,'triangle',.08)),gurka:()=>{tone(392,0,.32,'sawtooth',.045,370);tone(330,.32,.32,'sawtooth',.045,311);tone(262,.64,.7,'sawtooth',.045,196)},over:()=>[784,659,523].forEach((f,i)=>tone(f,i*.12,.3,'triangle',.06))};
const sfx=k=>{if(snd)try{SFX[k]()}catch{}},buzz=p=>{try{if(navigator.userActivation?.hasBeenActive!==false)navigator.vibrate?.(p)}catch{}}; // vibration kräver en tidigare användargest
addEventListener('pointerdown',()=>au(),{once:true,capture:true}); // ljud får bara starta efter en användargest

// ---------- små hjälpare ----------
const put=(el,h)=>{if(el._h!==h){el._h=h;el.innerHTML=h;return true}return false}; // rör bara DOM när något ändrats – fokus och inmatning överlever
function toast(m,k='err'){const t=$('toast');t.textContent=m;t.className='on '+k;clearTimeout(t._h);t._h=setTimeout(()=>t.className=k,k==='chat'?3200:2800)}
function say(m){const r=$('sr');r.textContent='';setTimeout(()=>r.textContent=m,30)} // skärmläsare
function setConn(k,l){const c=$('conn');c.className='conn '+k;c.setAttribute('aria-label',l);c.title=l;if(k!=='ok')$('status').textContent=l}

// ---------- anslutning ----------
const hub=new signalR.HubConnectionBuilder().withUrl('/hub').withAutomaticReconnect({nextRetryDelayInMilliseconds:c=>Math.min(500*2**c.previousRetryCount,8000)}).build();
let S=null,sel=new Set(),lv=0,resOpen=false,unread=0,fresh=new Set(),hist=[],until={};
let room=(new URLSearchParams(location.search).get('bord')||'').replace(/[^A-Za-z0-9]/g,'').toUpperCase().slice(0,8),tok=ss.get('tok'),wasSeated=false;
const call=(m,...a)=>hub.invoke(m,...a).catch(()=>{toast('Kunde inte nå servern');sfx('err')});
hub.on('state',s=>{if(s.v<=lv&&s.room===S?.room)return;lv=s.v;const p=S&&S.room===s.room?S:null;S=s;
 s.seats.forEach(q=>{if(q&&!q.on)until[q.seat]??=Date.now()+q.grace*1000;else if(q)delete until[q.seat]});render(p)}); // ignorera tillstånd som kommer i fel ordning
let lastErr={m:'',t:0};
hub.on('err',m=>{lastErr={m,t:Date.now()};if(/annan flik/.test(m)){TK.put(null,tok);tok=null;ss.set('tok',null);wasSeated=false}if(/utan aktivitet/.test(m))return asleep(m);
 if(/För många anrop/.test(m)){const e=lastErr;setTimeout(()=>{if(!e.retry){toast(m);sfx('err')}},150);return} // hello() försöker igen tyst; felet visas bara om det inte görs
 toast(m);sfx('err');buzz([30,40,30])});
// servern kopplar ner inaktiva åskådare – då ska vi inte återansluta (det ger bara en ny timeout), bara erbjuda omladdning
let slept=false;function asleep(m){slept=true;hub.stop().catch(()=>{});setConn('down','Frånkopplad');S=null;clearTable();const lb=$('lobby');lb.hidden=false;
 put(lb,`<div class="lb"><h2>Frånkopplad</h2><p>${esc(m)}</p><div class="row"><button class="pri" data-a="reload">Ladda om</button></div></div>`)}
hub.on('chat',(n,t)=>{const l=$('log'),sys=n==='Bordet';l.insertAdjacentHTML('beforeend',`<div class="${sys?'sys':''}"><b>${esc(n)}:</b> ${esc(t)}</div>`);while(l.childElementCount>200)l.firstChild.remove();l.scrollTop=l.scrollHeight;
 if(!sideShown()){unread++;badge();toast(`${n}: ${t}`,'chat')}if(!sys)sfx('chat')});
// platstoken: servern ger en ny vid varje Hello. Fliken minns sin egen (sessionStorage); localStorage minns tokens för platser
// vars flik inte längre lever, så att en stängd och återöppnad app får tillbaka sin plats – utan att stjäla platsen från en öppen flik.
// Varje plats-token har {t: tid, s: 'v' synlig flik som slår hjärtslag | 'h' dold sedan t | 'r' fliken stängd}.
const TK={get(){const m=ls.get('toks',{});return m&&typeof m==='object'?m:{}},
 put(t,old,st='v'){const m=TK.get(),n=Date.now();if(old)delete m[old];if(t)m[t]={t:n,s:st};for(const k in m)if(!(n-m[k]?.t<6e5))delete m[k];ls.set('toks',m)},
 orphan(){const n=Date.now(),age=x=>n-x.t; // dold < 20 s räknas som levande så att en ny flik för en ny spelare inte tar platsen
  return Object.entries(TK.get()).filter(([,x])=>x.s==='r'||x.s==='v'&&age(x)>6000||x.s==='h'&&age(x)>20000).sort((a,b)=>b[1].t-a[1].t)[0]?.[0]||null}};
const seated=()=>tok&&S&&S.you>=0;
setInterval(()=>{if(seated()&&document.visibilityState==='visible')TK.put(tok)},2000);
let gone=false; // pagehide kommer före visibilitychange när fliken stängs – 'r' får inte skrivas över med 'h'
addEventListener('pagehide',()=>{gone=true;if(seated())TK.put(tok,null,'r')});addEventListener('pageshow',()=>gone=false);
document.addEventListener('visibilitychange',()=>{if(seated()&&!gone)TK.put(tok,null,document.visibilityState==='hidden'?'h':'v')});
async function hello(code=room,tries=0){
 const t=tok||TK.orphan();let r=null;if(code!==room)$('log').innerHTML='';const t0=Date.now();try{r=await hub.invoke('Hello',t,code)}catch{}
 if(!r&&tries<5){await new Promise(z=>setTimeout(z,60)); // felet kan komma strax efter svaret
  if(lastErr.t>=t0&&/För många anrop/.test(lastErr.m)){lastErr.retry=true;await new Promise(z=>setTimeout(z,700+Math.random()*1500));return hello(code,tries+1)}} // flodskydd när många bakom samma NAT ansluter samtidigt
 if(!r){room=S?.room??'';history.replaceState(null,'',room?'?bord='+encodeURIComponent(room):location.pathname);if(!S)landing();return false} // servern har redan skickat felet, t.ex. okänd bordskod
 const old=tok;tok=r.token;ss.set('tok',tok);TK.put(r.seat>=0?tok:null,t!==tok?t:old);
 if(r.room!==room){room=r.room;history.replaceState(null,'',room?'?bord='+encodeURIComponent(room):location.pathname)}
 try{hist=JSON.parse(ss.get('hist.'+room)||'[]')}catch{hist=[]}
 if(r.seat>=0&&!wasSeated&&t)toast('Välkommen tillbaka – du har kvar din plats','ok');wasSeated=r.seat>=0;chkBuild(r.build);if(!room)landing();return true}
// versionshandslag: build.js speglar filerna på disken nu, Hello den build som processen startade med.
// Olika → ladda om en gång (spärrat i sessionStorage), kvarstår det kör servern gammal kod.
function chkBuild(b){const mine=window.GURKA_BUILD,bn=$('verBanner'),has=typeof mine==='string',old=()=>{bn.textContent='Servern kör en äldre version – starta om servern.';bn.hidden=false};
 if(typeof b!=='string')return old(); // servern är äldre än handslaget
 if(b===mine){bn.hidden=true;return}
 // build.js kom inte fram (nätfel, proxy): försök en gång till, men ge inget råd om omstart utan bevis
 const k=b+'/'+(has?mine:'-');let ok=false;try{if(sessionStorage.getItem('gurka-reload')!==k){sessionStorage.setItem('gurka-reload',k);ok=sessionStorage.getItem('gurka-reload')===k}}catch{}
 if(ok)location.reload();else if(has)old()}
async function online(){setConn('ok','Ansluten');lv=0;await hello()}
async function connect(){if(slept)return;setConn('wait','Ansluter…');try{await hub.start();await online()}catch{setConn('down','Ingen kontakt med servern – försöker igen…');setTimeout(connect,3000)}}
hub.onreconnecting(()=>setConn('wait','Återansluter…'));
hub.onreconnected(online);
hub.onclose(()=>{if(slept)return;setConn('down','Tappade anslutningen – försöker igen…');setTimeout(connect,2000)});
connect();
setInterval(()=>document.querySelectorAll('.gr').forEach(e=>{const u=until[e.dataset.s];if(u)e.textContent=Math.max(0,Math.ceil((u-Date.now())/1000))+' s'}),1000);

// ---------- regler som speglar servern ----------
const nm=i=>S.seats[i]?S.seats[i].name:'?',who=i=>i===S.you?'Du':nm(i);
const crown='<span class="host" title="Värd – startar spelet">👑<span class="sr"> värd</span></span>';
const waitHost=(s,nx)=>s.host>=0?`Väntar på att ${esc(nm(s.host))} startar ${nx?'nästa giv':'spelet'}.`:'Väntar på en värd…';
const draw=s=>s.phase==='over'&&(s.winner<0||s.gurka<0); // oavgjort: ingen vinner, poängen står still
// sista sticket: varje spelares summa av sina kort (med ett kort = kortets valör). Bara för att förklara – vinnare, gurka och poäng kommer från servern
const tot=q=>q.cards.reduce((a,c)=>a+(+c.r||0),0),many=s=>s.trick.some(q=>q.cards.length>1);
const tie=s=>{const m=s.trick.map(q=>({seat:q.seat,v:tot(q)})),lo=Math.min(...m.map(x=>x.v)),w=m.filter(x=>x.v===lo).map(x=>esc(who(x.seat)));
 return w.length>1&&lo<99?`${w.slice(0,-1).join(', ')} och ${w.at(-1)} delade ${many(s)?`den lägsta summan (${lo})`:`den lägsta valören (${RN(lo)})`}.`:'Flera delade det lägsta i sista sticket.'};
const sumsH=(s,g,w)=>`<div class="sums">${s.trick.map(q=>`<div class="s${+q.seat}${q.seat===g?' g':q.seat===w?' w':''}"><span>${esc(who(q.seat))}</span><b>${sumTxt(q)}</b></div>`).join('')}</div>`;
const sumTxt=q=>{const v=q.cards.map(c=>+c.r||0).sort((a,b)=>b-a);return v.length>1?`${v.join(' + ')} = ${tot(q)}`:String(tot(q))}; // t.ex. 14 + 9 + 3 = 26
const lead=()=>!S.trick.length||S.done,myTurn=()=>S.you>=0&&S.turn===S.you;
const topRs=()=>{const p=S.trick.find(p=>p.seat===S.top);return p?p.cards.map(c=>c.r).sort((a,b)=>a-b):[0]};
function legal(ix){ // -1 = ogiltigt, annars antal sakade kort (samma kontroll som PlayCards)
 const h=S.hand;if(!ix.length)return-1;const pr=ix.map(i=>h[i].r).sort((a,b)=>a-b);
 if(lead())return pr.every(r=>r===pr[0])?0:-1;
 if(ix.length!==S.count)return-1;const T=topRs(),B=a=>a.every((r,i)=>r>=T[i]);if(B(pr))return 0;
 const lo=h.map(c=>c.r).sort((a,b)=>a-b);
 for(let j=1;j<=S.count;j++)if(pr.slice(0,j).every((r,i)=>r===lo[i])&&B(pr.slice(j)))return j;return-1}
let LS=null,LSs=null;
function legalSets(){if(LSs===S)return LS;const n=S.hand.length,o=[];
 for(let m=1;m<1<<n;m++){const ix=[];for(let i=0;i<n;i++)if(m>>i&1)ix.push(i);if(ix.length<=4&&legal(ix)>=0)o.push(ix)}LSs=S;return LS=o}
const fits=x=>legalSets().some(m=>[...x].every(j=>m.includes(j)));
const picking=()=>S&&myTurn()&&(S.phase==='exchange'||S.phase==='play'||S.phase==='respond'&&S.free||S.phase==='oneup'&&(S.up?.stage==='pick'||S.up?.stage==='offer'&&upY));
const pickMax=()=>S.phase==='exchange'?Math.min(7,S.deck):S.phase==='oneup'?1:S.exn;
// 1 upp: up={owner,stage:pick|decide|offer,card,asking,used,mark}. upY = du har svarat ja och väljer kortet du slänger
let upY=false;const U=()=>S.phase==='oneup'&&S.up?S.up:null;
const upOpts=d=>d>=7?'1 upp, byt alla 7 eller behåll':d>0?'1 upp eller behåll':'gräv i slasken (1 kort) eller behåll'; // leken styr valen i pick
const rel=i=>(i-S.starter+4)%4,askable=u=>S.seats.some((q,i)=>q&&i!==S.starter&&i!==u.owner&&!u.used.includes(i)), // tackar ägaren nej frågas bara de som inte haft sin tur (aldrig startspelaren)
spent=(u,i)=>u.used.includes(i)&&rel(i)>rel(u.owner); // tog upp-kortet innan sin tur

// ---------- rendering ----------
function layout(s){ // placerar spelarna runt bordet, du längst ner
 const ord=[0,1,2,3].map(k=>s.you>=0?(s.you+k)%4:k).filter(i=>s.seats[i]);
 const t={1:['bottom'],2:['bottom','top'],3:['bottom','left','right'],4:['bottom','left','top','right']}[ord.length]||[];
 const m={};ord.forEach((i,k)=>m[i]=t[k]);return m}
function snapR(){const r={h:{},p:{},t:[]};if(RM.matches)return r;
 document.querySelectorAll('#hand .card').forEach(e=>r.h[e.dataset.k]=e.getBoundingClientRect());
 document.querySelectorAll('.plate[data-s]').forEach(e=>r.p[e.dataset.s]=e.getBoundingClientRect());
 document.querySelectorAll('#trick .card').forEach(e=>r.t.push({e:e.cloneNode(true),b:e.getBoundingClientRect(),k:e.dataset.k}));return r}
function render(p){
 const s=S,r=snapR(),pos=layout(s),h=s.hand||[],cnt=s.seats.filter(x=>x).length,inGame=game();
 const hsig=h.map(key).join();if(!p||hsig!==(p.hand||[]).map(key).join()){sel.clear();
  if(p&&p.hand&&['exchange','zlatan','respond','oneup'].includes(s.phase)){const o=new Set(p.hand.map(key));h.forEach(c=>{if(!o.has(key(c))&&p.hand.length)fresh.add(key(c))})} // nya kort efter byte
  if(s.phase==='exchange'&&p&&p.phase!=='exchange'||s.phase==='play'&&p&&h.length<(p.hand||[]).length||!inGame)fresh.clear()}
 if(!(U()?.stage==='offer'&&s.up.asking===s.you))upY=false;
 document.body.dataset.phase=s.phase;codeBtn();
 $('status').textContent=({lobby:()=>`${cnt}/4 vid bordet`,exchange:()=>`${who(s.starter)} ${s.tie?.length?'vann avgörandet':'har högst kort'} och börjar`,zlatan:()=>'Zlatan-läge',oneup:()=>{const u=s.up;return!u?'':u.stage==='pick'?`${who(u.owner)} väljer: ${upOpts(+s.deck)}`:u.stage==='decide'?`${who(u.owner)} funderar på upp-kortet`:`${who(u.asking)} får frågan om upp-kortet`},respond:()=>s.turn>=0?`${who(s.turn)} ${s.free?'väljer kort att byta':'väljer kasta eller behålla'}`:'',play:()=>`${who(s.turn)} är på tur`,over:()=>'Omgången är slut'}[s.phase]||(()=>''))();
 // spelarskyltar
 const pl={top:'',left:'',right:'',bottom:''};
 const tb=s.tie||[],u=U();
 s.seats.forEach((q,i)=>{if(!q)return;const rv=s.reveal.find(x=>x.seat===i),me=i===s.you,st=i===s.starter&&inGame;
  pl[pos[i]]=`<div class="plate s${i} ${s.turn===i&&inGame?'turn':''} ${u&&spent(u,i)?'sp':''} ${me?'you':''} ${q.on===false?'off':''}" data-s="${i}"><span class="av ${u&&u.used.includes(i)?'ud':''}" aria-hidden="true">${ini(q.name)}</span><div class="pi"><div class="pn">${i===s.host?crown:''}${esc(q.name)}${me?' (du)':''}${u&&u.used.includes(i)?`<span class="sr">, ${spent(u,i)?'turen förbrukad':'har haft sin tur'}</span>`:''}</div>${q.on===false?`<div class="pc x" title="Frånkopplad – platsen hålls en stund">⏳ <span class="gr" data-s="${i}">${+q.grace} s</span> <span class="sr">kvar innan platsen släpps</span></div>`:`<div class="pc">${+q.score} p · ${+q.n} kort${st?' · <span class="st" title="Startspelare">★</span>':''}</div>`}</div><div class="backs">${rv?card(rv,'sm in'+(i===s.starter&&!tb.length?' hi':''))+tbk(tb.filter(x=>x.seat===i),i===s.starter):'<span class="back"></span>'.repeat(Math.min(+q.n||0,13))}</div></div>`});
 for(const k in pl)if(put($('p-'+k),pl[k]))$('p-'+k).querySelectorAll('.backs').forEach(b=>{const n=b.children.length;[...b.children].forEach((x,k)=>x.style.setProperty('--k',k-(n-1)/2))});
 // sticket
 const tp=s.trick;
 put($('trick'),tp.map(q=>`<div class="play s${q.seat} ${s.done&&q.seat===s.top&&s.phase!=='over'?'won':''}" data-p="${pos[q.seat]||'bottom'}" data-s="${q.seat}"><div class="nm">${esc(who(q.seat))}</div><div class="cs">${q.cards.map(c=>card(c,'md')).join('')}</div>${s.phase==='over'&&q.seat===s.gurka?'<span class="tag g">🥒 Gurkan</span>':s.phase==='over'&&q.seat===s.winner?'<span class="tag w">🏆 Vinner</span>':q.sak?'<span class="tag sak">Sakat</span>':q.seat===s.top&&!s.done?'<span class="tag">Högst</span>':''}</div>`).join('')+`<div id="msg">${msg(s)}</div>`);
 // spelade stick
 const pa=$('past');if(put(pa,s.past.length?'<span class="pl">Spelade stick</span>'+s.past.map(t=>`<div class="grp">${t.flatMap(q=>q.cards.map(c=>mini(c,q.sak))).join('')}</div>`).join(''):''))pa.scrollLeft=pa.scrollWidth;
 score();
 // handen
 const hand=$('hand'),fk=document.activeElement?.closest?.('#hand .card')?.dataset.k;
 if(put(hand,h.map((c,i)=>({c,i})).sort((a,b)=>a.c.r-b.c.r||a.c.s-b.c.s).map((x,k)=>`<button type="button" class="card ${isRed(x.c)?'red':''} ${fresh.has(key(x.c))?'new':''}" data-i="${x.i}" data-k="${key(x.c)}" data-n="${k+1}" aria-label="${cn(x.c)}${fresh.has(key(x.c))?', nytt':''}">${ix(x.c)}</button>`).join(''))&&fk)hand.querySelector(`[data-k="${fk}"]`)?.focus();
 $('me').hidden=s.phase==='lobby';lobby();mark();acts();
 if(p)events(p,r);
 wake(inGame);
}
function msg(s){
 if(draw(s))return`<div class="res"><span class="w">🤝 Oavgjort</span><span class="sub">Ingen vinner – poängen står still</span></div>`;
 if(s.phase==='over')return`<div class="res"><span class="g">🥒 ${esc(who(s.gurka))} fick gurkan <small>(−${+s.points})</small></span><span class="w">🏆 ${esc(who(s.winner))} vinner <small>(+${+s.points})</small></span></div>`;
 if(s.phase==='exchange')return`${esc(who(s.starter))} ${s.tie?.length?'vann avgörandet':'fick högst kort'} och börjar<span class="sub">${s.starter===s.you?(s.zlMe?'Du kan köra Zlatan':'Välj kort att byta'):'Byter kort…'}</span>`;
 if(s.phase==='zlatan')return`${esc(who(s.starter))} ${+s.exn?`byter ${+s.exn} kort`:'behåller alla kort'}<span class="sub">${s.zlMe?'Du kan köra Zlatan':`Väntar på Zlatan-beslut (${+s.zlN})`}</span>`;
 if(s.phase==='oneup'&&s.up){const u=s.up,c=u.card,d=+s.deck-(c?1:0),dk=d<7?`<span class="sub">Leken: ${d} kort · slasken: ${+s.slask} kort</span>`:''; // uppvända kortet räknas i deck men ligger synligt
  if(u.stage==='pick'||!c)return`${esc(who(u.owner))} väljer<span class="sub">${upOpts(+s.deck)}</span>${dk}`;
  return`<div class="upw">${card(c,'up in')}<div class="ut"><span class="sr">Uppvänt kort: ${esc(cn(c))}. </span>${esc(who(u.owner))} körde 1 upp<span class="sub">${u.stage==='decide'?(u.owner===s.you?'Ta kortet eller tacka nej':'Tar kortet eller tackar nej…'):u.asking===s.you?'Vill du ta kortet?':`Vill ${esc(nm(u.asking))} ta kortet?`}</span>${dk}</div></div>`}
 if(s.phase==='respond')return s.deck<7?`<span class="sub">Leken: ${+s.deck} kort · slasken: ${+s.slask} kort</span>`:'';
 if(s.phase==='play'){if(s.done)return`${esc(who(s.top))} tog sticket<span class="sub">och spelar ut</span>`;if(!s.trick.length)return`${esc(who(s.turn))} spelar ut`}
 return''}
function score(){const s=S;
 put($('score'),'<h2>Poängställning</h2>'+(s.seats.filter(x=>x).sort((a,b)=>b.score-a.score).map(q=>`<div class="sc s${q.seat}"><i class="dot"></i><span>${esc(q.name)}${q.seat===s.you?' (du)':''}</span><b>${+q.score}</b></div>`).join('')||'<p class="hr">Ingen vid bordet ännu.</p>')+
  (hist.length?'<h3>Senaste omgångar</h3>'+hist.slice(-8).reverse().map(x=>`<div class="hr"><i>#${+x.n}</i><span>${x.d?'🤝 Oavgjort – poängen stod still':`🏆 ${esc(x.w)} +${+x.p} · 🥒 ${esc(x.g)} −${+x.p}`}</span></div>`).join(''):''))}

// startsidan: inget bord valt – skapa ett eget eller gå med via kod (det finns inget standardbord)
function codeBtn(){const b=$('codeBtn');b.hidden=!S||!room;b.textContent=room;b.setAttribute('aria-label','Bjud in – bordskod '+room)} // koden syns i headern även under spelet
function clearTable(){['p-top','p-left','p-right','p-bottom','trick','past','hand','actions'].forEach(i=>put($(i),''));$('me').hidden=true;$('result').hidden=true;resOpen=false;document.body.dataset.phase='home';codeBtn()}
function landing(){S=null;lv=0;clearTable();$('status').textContent='Skapa ett bord eller gå med via kod';const lb=$('lobby'),first=!lb._h;lb.hidden=false;
 put(lb,`<div class="lb"><h2>Gurka</h2><p>Kortspelet för 2–4 där ingen vill få gurkan. Skapa ett bord och bjud in de andra med länken eller koden.</p>
  <form class="join" data-f="create"><input id="nick" maxlength="14" placeholder="Ditt namn" aria-label="Ditt namn" autocomplete="nickname" enterkeyhint="go"><button class="pri">Skapa bord</button></form>
  <p class="or">eller gå med via kod</p>
  <form class="join" data-f="room"><input maxlength="8" placeholder="Bordskod" aria-label="Bordskod" autocapitalize="characters" autocomplete="off" spellcheck="false" enterkeyhint="go"><button>Gå med</button></form>
  <div class="row"><button data-a="help">Så spelar man</button></div></div>`);
 const n=$('nick');if(n&&!n.value){n.value=ls.get('name','');if(first&&WIDE.matches)n.focus()}}

// lobbyn: väntrummet innan första given
function lobby(){const s=S,lb=$('lobby');lb.hidden=s.phase!=='lobby';if(lb.hidden)return;
 const me=s.you>=0,cnt=s.seats.filter(x=>x).length,full=cnt===4;
 const slots=[0,1,2,3].map(i=>{const q=s.seats[i];return q?`<li class="slot s${i} ${i===s.you?'you':''}"><span class="av">${ini(q.name)}${i===s.host?crown:''}</span><span>${esc(q.name)}${i===s.you?' (du)':''}</span></li>`:'<li class="slot empty"><span class="av">+</span><span>Ledig plats</span></li>'}).join('');
 let a;
 if(!me)a=full?'<p>Bordet är fullt (4/4). Du kan titta på tills en plats blir ledig.</p>':`<form class="join" data-f="join"><input id="nick" maxlength="14" placeholder="Ditt namn" aria-label="Ditt namn" autocomplete="nickname" enterkeyhint="go"><button class="pri">Sätt dig</button></form>`;
 else a=cnt<2?`<p>Väntar på fler spelare – minst 2 behövs. ${s.host===s.you?'Bjud in någon!':''}</p>`:s.host===s.you?`<div class="row"><button class="pri" data-a="start">Starta spelet (${cnt} spelare)</button></div>`:`<p>${waitHost(s)}</p>`;
 const first=!lb._h,code=`<b class="code">${esc(room)}</b>`;
 put(lb,`<div class="lb"><h2>Välkommen till bordet</h2><p>Kortspelet för 2–4 där ingen vill få gurkan. Du sitter vid ${code}.</p><ul class="slots" aria-label="Platser">${slots}</ul>${a}<div class="row"><button data-a="help">Så spelar man</button><button data-a="invite">Bjud in</button>${me?'<button class="warn" data-a="leave">Lämna</button>':''}</div>
  <details class="rooms"><summary>Annat bord</summary><p>Varje bord har en kod som bara ni känner till.</p><div class="row"><button data-a="newRoom">Nytt bord</button><button data-a="home">Till startsidan</button></div>
  <form class="join" data-f="room"><input maxlength="8" placeholder="Bordskod" aria-label="Bordskod" autocapitalize="characters" autocomplete="off" spellcheck="false"><button>Gå till bord</button></form></details>
  <p class="small">Andra ${location.protocol==='https:'?'':'i samma nätverk '}ansluter via <b>${esc(location.host)}</b> och bordskoden <b>${esc(room)}</b>.</p></div>`);
 const n=$('nick');if(n&&!n.value){n.value=ls.get('name','');if(first&&WIDE.matches)n.focus()}}

function acts(){
 const s=S,h=s.hand||[],me=s.you>=0,my=myTurn();let a='';
 if(!me){a=s.phase==='over'?(s.seats.every(x=>x)?'<p>Bordet är fullt (4/4).</p>':`<form class="join" data-f="join"><input id="nick2" maxlength="14" placeholder="Ditt namn" aria-label="Ditt namn" autocomplete="nickname"><button class="pri">Sätt dig</button></form>`):s.phase==='lobby'?'':'<p>Spelet pågår – du tittar på. Du kan sätta dig när omgången är slut.</p>'}
 else if(s.phase==='over')a=(s.host===s.you?`<button class="pri" data-a="start" ${s.seats.filter(x=>x).length<2?'disabled':''}>Spela igen</button>`:`<p>${waitHost(s,1)}</p>`)+`<button data-a="showRes">Visa resultat</button>`;
 else if(s.phase==='zlatan'&&s.zlMe){const ok=s.deck-s.exn>=7;
  a=`<p>${esc(nm(s.starter))} ${+s.exn?`byter ${+s.exn} kort`:'behåller alla kort (du får sedan bara kasta alla eller behålla)'}. Du har inget kort över 10 – <span class="hint">Zlatan!</span> Kasta alla och få 7 nya innan bytet?${ok?'':' (Leken räcker inte.)'}</p><button class="pri" data-a="zy" ${ok?'':'disabled'}>Zlatan – 7 nya</button><button data-a="zn">Behåll</button>`}
 else if(s.phase==='zlatan')a=`<p>Väntar på ${+s.zlN>1?+s.zlN+' spelare':'en spelare'} som får välja Zlatan – 7 nya kort innan bytet…</p>`;
 else if(s.phase==='exchange'&&my&&s.zlMe&&!sel.size){const ok=s.deck>=7; // startspelarens Zlatan före bytet; markerar man kort byter man som vanligt
  a=`<p>Du börjar och har inget kort över 10 – <span class="hint">Zlatan!</span> Kasta alla och få 7 nya, eller strunta i det och byt som vanligt.${ok?'':' (Leken räcker inte.)'}</p><button class="pri" data-a="zy" ${ok?'':'disabled'}>Kör Zlatan – 7 nya kort</button><button data-a="zn">Strunta i det</button>`}
 else if(s.phase==='exchange'&&my)a=`<p>Du börjar! Markera de kort du vill byta mot nya från leken – eller behåll alla. Markerar du <span class="hint">exakt 1</span> kör du 1 upp.</p><button class="pri" data-a="x" ${sel.size>s.deck?'disabled':''}>${sel.size==1?'1 upp':sel.size?`Byt ${sel.size} kort`:'Behåll alla'}</button>${sel.size?'<button data-a="clr">Avmarkera</button>':''}`;
 else if(s.phase==='oneup'&&my&&s.up)a=oneUp(s,s.up);
 else if(s.phase==='respond'&&my&&s.free)a=freeResp(s);
 else if(s.phase==='respond'&&my)a=`<p>${esc(nm(s.starter))} bytte inga kort. Kasta alla och få 7 nya, eller behåll?${s.deck<7?' (Leken räcker inte för 7 nya.)':''}</p><button class="pri" data-a="ra" ${s.deck<7?'disabled':''}>Kasta alla</button><button data-a="rk">Behåll</button>`;
 else if(s.phase==='play'&&my){
  const L=lead(),tr=topRs(),can=L||h.some(c=>c.r>=tr[0]),ix=[...sel],j=ix.length?legal(ix):-1;
  const what=j<0?(L?'Spela ut':'Spela'):L?`Spela ut ${ix.length>1?ix.length+' × ':''}${RK(h[ix[0]].r)}`:j===0?'Slå':j===s.count?'Saka':`Slå + saka ${j}`;
  const tip=L?(ix.length?'Lägg till fler kort av samma valör, eller spela ut.':'Du spelar ut: välj ett kort, eller flera av samma valör.')
   :!can?'Du kan inte slå – du måste saka ditt lägsta.'
   :s.count==1?`Lägg ett kort som är <span class="hint">${RK(tr[0])} eller högre</span> för att slå, annars sakar du ditt lägsta.`
   :`Lägg ${s.count} kort. Slå lägst mot lägst (<span class="hint">≥ ${tr.map(RK).join(' och ≥ ')}</span>), eller saka dina lägsta och slå med resten.`;
  a=`<p>${tip}</p>`+(can?`<button class="pri" data-a="play" ${j<0?'disabled':''}>${what}</button>`:'')+(L?'':`<button ${can?'':'class="pri"'} data-a="sak">Saka ${s.count>1?s.count+' lägsta':'lägsta'}</button>`)}
 else if(game())a=`<p>${s.turn>=0?`Väntar på ${esc(nm(s.turn))}…`:''}</p>`;
 put($('actions'),a);
 $('hand').classList.toggle('live',!!picking());
 $('me').classList.toggle('my',my&&game()||s.phase==='zlatan'&&s.zlMe);
 const n2=$('nick2');if(n2&&!n2.value)n2.value=ls.get('name','');
}
const game=()=>!['lobby','over'].includes(S.phase);
function oneUp(s,u){const dk=+s.deck;
 if(u.stage==='decide'){const ask=askable(u),nx=`<span class="hint">${s.deck>1?'nästa kort, dolt':'ett dolt kort ur slasken'}</span>`;
  return`<p>Du körde 1 upp. Ta kortet – eller tacka nej, så ${ask?`får de som inte haft sin tur frågan och du får ${nx}`:`går kortet till slasken och du får ${nx}`}.</p><button class="pri" data-a="ut">Ta kortet</button><button data-a="un">${ask?'Tacka nej':'Tacka nej – du får nästa kort'}</button>`}
 if(u.stage==='offer')return upY?`<p>Markera kortet du slänger i stället för ${esc(cn(u.card))}.</p><button class="pri" data-a="uy" ${sel.size==1?'':'disabled'}>Släng valt kort</button><button data-a="ub">Ångra</button>`
  :`<p>${esc(nm(u.owner))} tackade nej. <span class="hint">Vill du ta kortet?</span>${u.used.includes(s.you)?'':' Då förbrukar du din tur.'}</p><button class="pri" data-a="uj">Ja</button><button data-a="un">Nej</button>`;
 let b=`<p>${esc(nm(s.starter))} körde 1 upp. ${dk>0?`Markera ett kort och kör 1 upp, ${dk>=7?'byt alla 7 ':''}eller behåll.${dk<7?` Leken har ${dk} kort kvar.`:''}`:'Leken är slut: markera ett kort och gräv i slasken (1 slumpat kort), eller behåll.'}</p>`;
 b+=dk>0?`<button class="pri" ${sel.size==1?'':'disabled'} data-a="up">1 upp</button>`:`<button class="pri" ${sel.size==1?'':'disabled'} data-a="ud">Gräv i slasken (1)</button>`;
 if(dk>=7)b+=`<button data-a="ra">Byt alla 7</button>`;
 return b+`<button data-a="rk">Behåll</button>`}
function freeResp(s){const N=+s.exn,dk=+s.deck;
 let b=`<p>${esc(nm(s.starter))} bytte ${N} kort. Byt <span class="hint">exakt ${N}</span>${dk>=7&&N<7?', alla 7':''} eller inga.${dk<N?` Leken har bara ${dk} kort kvar.`:''}</p>`;
 if(dk>=N)b+=`<button class="pri" ${sel.size==N?'':'disabled'} data-a="w">Byt ${N} valda${sel.size&&sel.size!=N?` (${sel.size}/${N})`:''}</button>`;
 if(dk<N&&dk>0)b+=`<button class="pri" ${sel.size==dk?'':'disabled'} data-a="w">Byt ${dk} valda (sista i leken)</button>`;
 if(dk<N)b+=`<button ${sel.size==N?'':'disabled'} data-a="wd">Gräv i slasken (${N})</button>`;
 if(dk>=7&&N<7)b+=`<button data-a="wa">Byt alla 7</button>`;
 return b+`<button data-a="wk">Behåll</button>`}

function mark(){
 const pk=picking(),play=pk&&S.phase==='play';
 document.querySelectorAll('#hand .card').forEach(c=>{const i=+c.dataset.i,on=sel.has(i);c.classList.toggle('sel',on);c.setAttribute('aria-pressed',pk?on:'false');
  const off=play&&!on&&!fits(new Set([...sel,i]))&&!(S.count===1&&!lead()&&fits([i]));c.classList.toggle('off',off);c.setAttribute('aria-disabled',off);c.tabIndex=pk||c===document.activeElement?0:-1})}
function pick(i){
 if(!picking())return;
 if(sel.has(i))sel.delete(i);
 else if(S.phase==='play'){const nx=new Set([...sel,i]);
  if(fits(nx))sel=nx;else if(fits([i]))sel=new Set([i]); // börja om med det nya kortet
  else{buzz(20);toast(lead()?'Vid utspel får korten ha samma valör.':`Det kortet går inte att lägga nu.`);return}}
 else{const mx=pickMax();if(mx===1)sel.clear();if(sel.size<mx)sel.add(i);else{toast(`Du kan välja högst ${mx} kort.`);return}}
 sfx('tick');mark();acts()}

// ---------- händelser: ljud, animationer, skärmläsare ----------
function fly(el,from,o={}){if(RM.matches||!from)return;const to=el.getBoundingClientRect();if(!to.width)return;
 const dx=from.left+from.width/2-(to.left+to.width/2),dy=from.top+from.height/2-(to.top+to.height/2),sc=Math.max(.3,Math.min(2,from.width/to.width));
 if(Math.abs(dx)<1&&Math.abs(dy)<1&&Math.abs(sc-1)<.01&&o.op==null)return;
 el.animate([{transform:`translate(${dx}px,${dy}px) scale(${sc}) rotate(${o.rot||0}deg)`,opacity:o.op??1},{transform:'none',opacity:1}],{duration:o.dur||420,delay:o.delay||0,easing:o.ease||'cubic-bezier(.2,.8,.25,1)',fill:'backwards'})}
function events(p,r){
 const s=S,f=$('felt').getBoundingClientRect(),C={left:f.left+f.width/2-24,top:f.top+f.height/2-34,width:48,height:68};
 // nya kort i sticket flyger in från handen eller spelarens skylt
 const old=new Set(r.t.map(x=>x.k));let played=0;
 document.querySelectorAll('#trick .play').forEach(q=>{const seat=+q.dataset.s;q.querySelectorAll('.card').forEach((e,j)=>{if(old.has(e.dataset.k))return;played++;
  fly(e,seat===s.you&&r.h[e.dataset.k]||r.p[seat]||C,{dur:460,delay:j*70,rot:(seat%2?14:-14)})})});
 const np=s.trick.filter(q=>!p.trick.some(o=>o.seat===q.seat&&key(o.cards[0])===key(q.cards[0])));
 if(np.length&&s.phase!=='exchange'){sfx('card');np.forEach(q=>q.seat!==s.you&&say(`${nm(q.seat)} ${q.sak?'sakade':'lade'} ${q.cards.map(cn).join(', ')}`))}
 // sticket som tas hem flyger till den som vann det
 const gone=r.t.filter(x=>!document.querySelector(`#trick .card[data-k="${x.k}"]`));
 if(gone.length){const to=s.phase==='play'&&r.p[p.top]||C,fx=$('fx');if(s.phase==='play')sfx('collect');
  gone.forEach((x,k)=>{const g=x.e;g.classList.remove('in');g.style.left=x.b.left+'px';g.style.top=x.b.top+'px';fx.append(g);
   const dx=to.left+to.width/2-(x.b.left+x.b.width/2),dy=to.top+to.height/2-(x.b.top+x.b.height/2);
   const an=g.animate([{transform:'none',opacity:1},{transform:`translate(${dx}px,${dy}px) scale(.3) rotate(${k%2?20:-20}deg)`,opacity:0}],{duration:560,delay:90+k*35,easing:'cubic-bezier(.55,0,.7,.4)',fill:'both'});
   an.onfinish=an.oncancel=()=>g.remove()})}
 // handen: kvarvarande kort glider på plats, nya kort delas ut från bordets mitt
 const hn=document.querySelectorAll('#hand .card');let k=0;
 hn.forEach(e=>{const b=r.h[e.dataset.k];if(b)fly(e,b,{dur:300});else if(s.you>=0)fly(e,C,{dur:520,delay:90*k++,op:0,rot:-12,ease:'cubic-bezier(.2,.9,.3,1.15)'})});
 if(k>1)sfx('deal');
 // turordning och faser
 const mine=x=>x.you>=0&&(x.turn===x.you&&['exchange','respond','oneup','play'].includes(x.phase)||x.phase==='zlatan'&&x.zlMe);
 if(mine(s)&&!(mine(p)&&p.phase===s.phase&&p.turn===s.turn)){sfx('turn');buzz(25);say('Din tur. '+$('actions').querySelector('p')?.textContent||'')}
 const uc=U()?.card;if(uc&&!(p.phase==='oneup'&&p.up?.card&&key(p.up.card)===key(uc)&&p.up.owner===S.up.owner)){sfx('card');say(`${nm(S.up.owner)} körde 1 upp: ${cn(uc)} vänds upp.`)}
 if(s.phase==='exchange'&&p.phase!=='exchange')say(`Ny giv. ${who(s.starter)} börjar.`);
 if(s.phase==='over'&&p.phase!=='over'){
  const dr=draw(s),e=dr?{n:(hist.at(-1)?.n||0)+1,d:1}:{n:(hist.at(-1)?.n||0)+1,w:nm(s.winner),g:nm(s.gurka),p:+s.points};hist.push(e);hist=hist.slice(-20);ss.set('hist.'+room,JSON.stringify(hist));score();
  setTimeout(()=>{showRes();if(dr){sfx('over');buzz(60)}else if(s.you>=0&&s.winner===s.you){sfx('win');confetti('');buzz([40,60,40,60,120])}else if(s.you>=0&&s.gurka===s.you){sfx('gurka');confetti('🥒');buzz(400)}else sfx('over')},Math.max(600,played*200))}
 if(s.phase!=='over'&&resOpen)hideRes();
}
function confetti(t){if(RM.matches)return;const fx=$('fx'),cs=['#ecc94b','#7fd48a','#6fc3df','#f2927f','#b9a3f5','#fff'];
 for(let i=0;i<(t?28:80);i++){const d=document.createElement('i'),st=d.style;d.className='cf';if(t){d.textContent=t;st.fontSize=(22+Math.random()*18)+'px';st.width=st.height='auto'}else st.background=cs[i%cs.length];
  st.left=Math.random()*100+'vw';st.setProperty('--x',(Math.random()*30-15)+'vw');st.setProperty('--r',(Math.random()*900-450)+'deg');st.setProperty('--t',(2.2+Math.random()*1.8)+'s');st.setProperty('--d',(Math.random()*.8)+'s');fx.append(d);setTimeout(()=>d.remove(),5200)}}

// resultatrutan efter sista sticket
function showRes(){const s=S;if(s.phase!=='over')return;const me=s.you>=0,w=s.winner,g=s.gurka;
 if(draw(s)){$('result').innerHTML=`<div class="big" aria-hidden="true">🤝</div><h2 id="rt">Oavgjort</h2><p>Ingen vinner – poängen står still.</p><p class="small">${tie(s)}</p>${many(s)?sumsH(s,-1,-1):''}
  <div class="row">${me&&s.host===s.you?`<button class="pri" data-a="start" ${s.seats.filter(x=>x).length<2?'disabled':''}>Spela igen</button>`:''}<button data-a="hideRes">Visa bordet</button></div>${me&&s.host!==s.you?`<p class="small">${waitHost(s,1)}</p>`:''}`;
  $('result').hidden=false;$('scrim').hidden=false;resOpen=true;$('result').querySelector('button')?.focus();return}
 const big=w===s.you?'🏆':'🥒',t=w===s.you?'Du vann!':g===s.you?'Du fick gurkan!':`${esc(nm(g))} fick gurkan!`;
 const last=s.trick.flatMap(q=>q.cards).reduce((a,c)=>c.r>a.r?c:a,{r:0,s:0}),gq=s.trick.find(q=>q.seat===g);
 $('result').innerHTML=`<div class="big" aria-hidden="true">${big}</div><h2 id="rt">${t}</h2>${many(s)?`<p>Sista sticket räknas på summan av korten.</p>${sumsH(s,g,w)}`+(gq?`<p class="small">Poäng: ${tot(gq)} × 5 = ${+s.points}</p>`:''):`<p>Högsta kortet i sista sticket var ${last.r?cn(last):'–'}.</p>`}
  <div class="rl"><div class="rr w s${w}"><span class="av">${ini(nm(w))}</span><span>🏆 ${esc(who(w))}</span><b>+${+s.points}</b></div><div class="rr g s${g}"><span class="av">${ini(nm(g))}</span><span>🥒 ${esc(who(g))}</span><b>−${+s.points}</b></div></div>
  <div class="row">${me&&s.host===s.you?`<button class="pri" data-a="start" ${s.seats.filter(x=>x).length<2?'disabled':''}>Spela igen</button>`:''}<button data-a="hideRes">Visa bordet</button></div>${me&&s.host!==s.you?`<p class="small">${waitHost(s,1)}</p>`:''}`;
 $('result').hidden=false;$('scrim').hidden=false;resOpen=true;$('result').querySelector('button')?.focus()}
function hideRes(){$('result').hidden=true;resOpen=false;if(!$('side').classList.contains('open'))$('scrim').hidden=true}

// ---------- knappar ----------
const A={
 start:()=>{hideRes();call('Start')},x:()=>{call('Exchange',[...sel]);sel.clear()},clr:()=>{sel.clear();mark();acts()},
 zy:()=>call('Zlatan',true),zn:()=>call('Zlatan',false),ra:()=>call('Respond',true),
 up:()=>{call('UpPick',[...sel][0],false);sel.clear()},ud:()=>{call('UpPick',[...sel][0],true);sel.clear()},ut:()=>call('UpTake',true,-1),un:()=>{upY=false;call('UpTake',false,-1)},
 uj:()=>{upY=true;sel.clear();mark();acts()},ub:()=>{upY=false;sel.clear();mark();acts()},uy:()=>{if(sel.size==1)call('UpTake',true,[...sel][0]);upY=false;sel.clear()},rk:()=>call('Respond',false),
 w:()=>{call('Swap',[...sel],false);sel.clear()},wd:()=>{call('Swap',[...sel],true);sel.clear()},wa:()=>call('Swap',[0,1,2,3,4,5,6],false),wk:()=>call('Swap',[],false),
 play:()=>{if(legal([...sel])>=0)call('PlayCards',[...sel])},
 sak:()=>{const h=S.hand;call('PlayCards',h.map((c,i)=>[c.r,i]).sort((a,b)=>a[0]-b[0]).slice(0,S.count).map(x=>x[1]))},
 showRes,hideRes,help:()=>openHelp(),
 invite:async()=>{const u=location.origin+'/'+(room?'?bord='+encodeURIComponent(room):'');try{if(navigator.share){await navigator.share({title:'Gurka',text:'Kom och spela Gurka!',url:u});return}await navigator.clipboard.writeText(u);toast('Länken är kopierad: '+u,'ok')}catch(e){if(e?.name!=='AbortError')toast('Dela den här adressen: '+u,'ok')}},
 leave:()=>{if(!confirm(game()?'Lämna bordet? Spelet avbryts för alla.':'Lämna bordet?'))return;hub.invoke('Leave').catch(()=>toast('Kunde inte lämna bordet'));TK.put(null,tok);wasSeated=false;$('help').open&&$('help').close()},
 newRoom:async()=>{if(S&&S.you>=0&&game()&&!confirm('Lämna det pågående spelet och öppna ett nytt bord?'))return;let c=null;try{c=await hub.invoke('NewRoom')}catch{}if(c)await hello(c)}, // vid null har servern redan skickat felet
 home:()=>{if(S&&S.you>=0&&game()&&!confirm('Lämna det pågående spelet?'))return;hello('')},reload:()=>location.reload(),
};
document.addEventListener('click',e=>{const b=e.target.closest('[data-a]');if(b&&!b.disabled&&A[b.dataset.a]){A[b.dataset.a](b)}});
document.addEventListener('submit',e=>{const f=e.target.closest('[data-f="join"]');if(!f)return;e.preventDefault();
 const n=f.querySelector('input').value.trim();ls.set('name',n);call('Join',n)});
document.addEventListener('submit',async e=>{const f=e.target.closest('[data-f="create"]');if(!f)return;e.preventDefault();const n=f.querySelector('input').value.trim(),b=f.querySelector('button');
 if(b.disabled)return;b.disabled=true;ls.set('name',n);let c=null;try{c=await hub.invoke('NewRoom')}catch{}if(c&&await hello(c))call('Join',n);b.disabled=false}); // skaparen blir värd när den sätter sig från samma anslutning
document.addEventListener('submit',e=>{const f=e.target.closest('[data-f="room"]');if(!f)return;e.preventDefault();const c=f.querySelector('input').value.replace(/[^A-Za-z0-9]/g,'').toUpperCase();if(c&&c!==room)hello(c)});
$('hand').addEventListener('click',e=>{const c=e.target.closest('.card');if(c)pick(+c.dataset.i)});
$('cin').addEventListener('submit',e=>{e.preventDefault();const v=$('ci').value;if(v.trim()){call('Chat',v);$('ci').value=''}});

// sidopanelen (poäng + chatt) är en låda på mindre skärmar
const sideShown=()=>WIDE.matches||$('side').classList.contains('open');
function badge(){const b=$('badge');b.hidden=!unread;b.textContent=unread>9?'9+':unread}
function side(o){const sd=$('side');o??=!sd.classList.contains('open');if(WIDE.matches)o=false;sd.classList.toggle('open',o);$('sideBtn').setAttribute('aria-expanded',o);$('scrim').hidden=!o&&!resOpen;
 if(o){unread=0;badge();$('log').scrollTop=$('log').scrollHeight;setTimeout(()=>$('ci').focus({preventScroll:true}),WIDE.matches?0:280)}}
$('sideBtn').onclick=()=>side();$('scrim').onclick=()=>{side(false);if(resOpen)hideRes()};
WIDE.addEventListener('change',()=>side(false));

// hjälp, ljud, helskärm
function openHelp(){$('leaveBox').innerHTML=S&&S.you>=0?'<button class="warn" data-a="leave">Lämna bordet</button>':'';$('help').showModal()}
$('helpBtn').onclick=openHelp;
const sndUI=()=>{$('sndBtn').setAttribute('aria-pressed',snd);$('sndBtn').title=snd?'Ljud på (M)':'Ljud av (M)'};sndUI();
$('sndBtn').onclick=()=>{snd=!snd;ls.set('snd',snd);sndUI();if(snd){au();sfx('turn')}};
if(document.fullscreenEnabled){const fb=$('fsBtn');fb.hidden=false;fb.onclick=()=>document.fullscreenElement?document.exitFullscreen():document.documentElement.requestFullscreen().catch(()=>{});
 document.addEventListener('fullscreenchange',()=>fb.setAttribute('aria-label',document.fullscreenElement?'Avsluta helskärm':'Helskärm'))}

// tangentbord
document.addEventListener('keydown',e=>{
 if(e.ctrlKey||e.metaKey||e.altKey)return;const tg=e.target,typing=tg.matches?.('input,textarea');
 if(e.key==='Escape'){if(resOpen)return hideRes();if($('side').classList.contains('open'))return side(false);if(sel.size){sel.clear();mark();acts()}return}
 if(typing||$('help').open)return;
 const k=e.key.toLowerCase(),cs=[...document.querySelectorAll('#hand .card')];
 if(/^[1-7]$/.test(k)&&cs[+k-1]){pick(+cs[+k-1].dataset.i);cs[+k-1].focus();e.preventDefault()}
 else if((k==='arrowleft'||k==='arrowright')&&tg.closest?.('#hand')){const i=cs.indexOf(tg.closest('.card'));cs[(i+(k==='arrowleft'?-1:1)+cs.length)%cs.length]?.focus();e.preventDefault()}
 else if(k==='enter'&&!tg.closest?.('button,a,input')){const b=document.querySelector('#result:not([hidden]) .pri,#actions .pri:not(:disabled),#lobby .pri');if(b){b.click();e.preventDefault()}}
 else if(k==='h')openHelp();else if(k==='c')side();else if(k==='m')$('sndBtn').click();else if(k==='f'&&!$('fsBtn').hidden)$('fsBtn').click()});

// håll skärmen vaken under en omgång
let wl=null,wlp=false;
async function wake(on){try{if(on&&!wl&&!wlp&&navigator.wakeLock&&document.visibilityState==='visible'){wlp=true;wl=await navigator.wakeLock.request('screen');wl.addEventListener('release',()=>wl=null)}else if(!on&&wl){const w=wl;wl=null;await w.release()}}catch{}finally{wlp=false}}
document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible'&&S)wake(game())});
// installerbar app (service worker kräver https eller localhost)
if('serviceWorker' in navigator&&isSecureContext)addEventListener('load',()=>navigator.serviceWorker.register('sw.js').catch(()=>{}));
