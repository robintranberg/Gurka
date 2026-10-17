// Mäter om #msg/.play överlappar skyltar i alla faser och storlekar med påhittade tillstånd. Arg: [utmapp] [taggprefix]. Env: SIZES, PH
const {launch,open,BASE,SHOTS}=require('./lib'),OUT=process.argv[2]||SHOTS,TAG=process.argv[3]||'a';
const SIZES=(process.env.SIZES||'844x390,740x360,667x375,360x640,390x844,768x1024,1440x900').split(',').map(x=>x.split('x').map(Number));
const PH=(process.env.PH||'exchange,exchangeMe,zlatan,respond,play0,play4,playDone,over,upPickMe,upPick2,upDecideMe,upDecide,upOfferMe,upOfferYes,upOffer,upPickDig,upDecide2p').split(',');
(async()=>{const br=await launch();
const p=await br.newPage(),errs=[];p.on('pageerror',e=>errs.push(e.message));p.on('console',m=>m.type()==='error'&&errs.push(m.text()));
await open(p, BASE+'/');await p.waitForFunction(()=>hub.state==='Connected');await p.evaluate(()=>A.newRoom());
await p.waitForFunction(()=>S,{timeout:8000});await p.evaluate(()=>hub.off('state'));let bad=0;
for(const [w,h] of SIZES){await p.setViewport({width:w,height:h});
 for(const ph of PH){
 const r=await p.evaluate(async ph=>{document.querySelectorAll(".dbgx").forEach(e=>e.remove());
  const seats=[0,1,2,3].map(i=>({seat:i,name:['Anna','Bo','Cecilia Långnamn','Dag'][i],n:ph.startsWith('play')?5:7,score:i*3-4,on:true,grace:0}));
  const hand=[{r:3,s:0},{r:4,s:1},{r:6,s:2},{r:7,s:3},{r:9,s:0},{r:10,s:1},{r:11,s:2}];
  const reveal=[{seat:0,r:14,s:0},{seat:1,r:14,s:3},{seat:2,r:9,s:1},{seat:3,r:14,s:2}],tie=[{seat:0,r:8,s:1,round:1},{seat:1,r:12,s:2,round:1},{seat:3,r:12,s:0,round:1},{seat:1,r:13,s:0,round:2},{seat:3,r:2,s:2,round:2}];
  const b={...S,_y:0,up:null,v:Date.now(),you:0,seats,starter:1,turn:1,count:0,top:-1,done:false,free:false,exn:0,deck:20,slask:5,zl:[],zlMe:false,zlN:0,reveal:[],tie:[],trick:[],past:[],points:0,gurka:-1,winner:-1,last:'',hand};
  const pl=(seat,cs)=>({seat,sak:false,cards:cs.map(([r,s])=>({r,s}))});
  const st={exchange:{...b,phase:'exchange',reveal,tie},exchangeMe:{...b,phase:'exchange',starter:0,turn:0,reveal},
   zlatan:{...b,phase:'zlatan',reveal,tie,exn:3,zlMe:true,zlN:2,turn:-1},respond:{...b,phase:'respond',turn:2,exn:3,deck:5},
   play0:{...b,phase:'play',turn:1,past:[[pl(0,[[5,1]]),pl(1,[[7,2]])]],hand:hand.slice(0,5)},
   play4:{...b,phase:'play',turn:1,top:3,count:2,trick:[pl(2,[[5,1],[5,2]]),pl(3,[[9,0],[9,3]]),pl(0,[[6,1],[6,0]])],past:[[pl(0,[[5,1]]),pl(1,[[7,2]])]],hand:hand.slice(0,5)},
   playDone:{...b,phase:'play',turn:3,top:3,done:true,count:2,trick:[pl(1,[[4,1],[4,2]]),pl(2,[[5,1],[5,2]]),pl(3,[[9,0],[9,3]]),pl(0,[[6,1],[6,0]])],hand:hand.slice(0,3)},
   upPickMe:{...b,phase:'oneup',exn:1,starter:1,turn:0,deck:20,up:{owner:0,stage:'pick',card:null,asking:-1,used:[1,2],mark:-1}},
   upPick2:{...b,phase:'oneup',exn:1,starter:1,turn:3,deck:5,up:{owner:3,stage:'pick',card:null,asking:-1,used:[1,2],mark:-1}},
   upDecideMe:{...b,phase:'oneup',exn:1,starter:0,turn:0,hand:hand.slice(0,6),up:{owner:0,stage:'decide',card:{r:13,s:2},asking:-1,used:[],mark:3}},
   upDecide:{...b,phase:'oneup',exn:1,starter:1,turn:1,up:{owner:1,stage:'decide',card:{r:12,s:3},asking:-1,used:[],mark:-1}},
   upOfferMe:{...b,phase:'oneup',exn:1,starter:1,turn:0,up:{owner:2,stage:'offer',card:{r:10,s:1},asking:0,used:[1],mark:-1}},
   upOfferYes:{...b,phase:'oneup',exn:1,starter:1,turn:0,up:{owner:2,stage:'offer',card:{r:10,s:1},asking:0,used:[1],mark:-1},_y:1},
   upOffer:{...b,phase:'oneup',exn:1,starter:1,turn:3,up:{owner:1,stage:'offer',card:{r:14,s:0},asking:3,used:[2],mark:-1}},
   upPickDig:{...b,phase:'oneup',exn:1,starter:1,turn:0,deck:0,slask:12,up:{owner:0,stage:'pick',card:null,asking:-1,used:[1,2,3],mark:-1}},
   upDecide2p:{...b,seats:[seats[0],seats[1],null,null],phase:'oneup',exn:1,starter:1,turn:1,up:{owner:1,stage:'decide',card:{r:9,s:0},asking:-1,used:[],mark:-1}},
   over:{...b,phase:'over',turn:-1,done:true,gurka:3,winner:1,points:5,trick:[pl(1,[[4,1]]),pl(2,[[5,1]]),pl(3,[[14,0]]),pl(0,[[6,1]])],hand:[]}}[ph];
  upY=!!st._y;S=st;render(null);await new Promise(r=>setTimeout(r,700));
  const R=e=>{const b=e.getBoundingClientRect();return{l:b.left,t:b.top,r:b.right,b:b.bottom}},X=(a,c)=>a.l<c.r-1&&c.l<a.r-1&&a.t<c.b-1&&c.t<a.b-1;
  const plates=[...document.querySelectorAll('.felt .plate')].map(R);
  const m=document.getElementById('msg'),rg=document.createRange();m.querySelectorAll(".sr").forEach(e=>e.remove());rg.selectNodeContents(m);const mr=m.textContent.trim()?(b=>({l:b.left,t:b.top,r:b.right,b:b.bottom}))(rg.getBoundingClientRect()):null;
  const plays=[...document.querySelectorAll('#trick .play')].map(R),felt=R(document.getElementById('felt'));
  const issues=[];if(mr&&plates.some(q=>X(mr,q)))issues.push('msg×plate');if(mr&&plays.some(q=>X(mr,q)))issues.push('msg×play');
  if(plays.some(a=>plates.some(q=>X(a,q))))issues.push('play×plate');
  if([...plates,...plays,...(mr?[mr]:[])].some(a=>a.t<felt.t-2||a.b>felt.b+2))issues.push('utanför filt');
  if(document.documentElement.scrollWidth>innerWidth)issues.push('hscroll');
  const res=document.getElementById('result');if(!res.hidden){res.hidden=true}
  const cd=document.querySelector('#trick .card');return{issues,cw:cd?Math.round(parseFloat(getComputedStyle(cd).width)):'-',act:document.getElementById('actions').textContent.replace(/\s+/g,' ').trim().slice(0,90),hs:document.documentElement.scrollWidth>innerWidth,msg:m.textContent.replace(/\s+/g,' ').trim().slice(0,40)}},ph);
 if(r.issues.length)bad++;
 console.log(`${w}x${h} ${ph.padEnd(10)} ${r.issues.join(',')||'ok'} cw=${r.cw} | ${r.msg} | ${r.act}${errs.length?' FEL:'+errs.join('|'):''}`);errs.length=0;
 await p.screenshot({path:`${OUT}/${TAG}-${w}x${h}-${ph}.png`})}}
console.log('problem:',bad);await br.close()})().catch(e=>{console.error('KRASCH',e);process.exit(1)});
