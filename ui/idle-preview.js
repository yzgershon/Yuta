// Browser-only examples. The Windows app draws its real idle activity natively.
import {shortTrackTitle} from './media.js';
import {notchPath} from './notch-path.js';
const cover = `data:image/svg+xml,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="64" height="64" fill="#4f3d74"/><circle cx="32" cy="29" r="23" fill="#ddbadf"/><path d="M0 44Q32 24 64 42V64H0Z" fill="#6f82a7"/></svg>')}`;
export function createIdlePreview(root,pill,collapse){
 let demo='auto',last={},wasExpanded,frames=[],frameMs=25,request=0,playingWave=false;
 const reduced=matchMedia('(prefers-reduced-motion: reduce)');
 const activity=document.createElement('span');activity.className='idle-activity';activity.setAttribute('aria-hidden','true');
 activity.innerHTML=`<img class="idle-cover" alt=""><span class="idle-notice-icon"></span><span class="idle-title"></span><span class="idle-percent"></span><span class="idle-battery"><b></b></span><span class="idle-wave">${Array.from({length:21},(_,i)=>{const t=i/20,from=[179,148,255],to=[86,182,255];return `<i style="background:rgb(${from.map((v,j)=>Math.round(v+(to[j]-v)*t)).join(',')})"></i>`;}).join('')}</span>`;
 pill.append(activity);
 const bars=[...activity.querySelectorAll('.idle-wave i')];
 function drawWave(now){
  request=0;
  if(!playingWave||document.hidden||reduced.matches||!frames.length)return;
  const cursor=now/frameMs,index=Math.floor(cursor)%frames.length,mix=cursor%1;
  const current=frames[index],next=frames[(index+1)%frames.length];
  bars.forEach((bar,i)=>bar.style.setProperty('--level',current[i]+(next[i]-current[i])*mix));
  request=requestAnimationFrame(drawWave);
 }
 function syncWave(){
  cancelAnimationFrame(request);request=0;
  if(reduced.matches)bars.forEach(bar=>bar.style.setProperty('--level',0));
  else if(playingWave&&!document.hidden&&frames.length)request=requestAnimationFrame(drawWave);
 }
 fetch('./assets/waveform-demo.json').then(r=>r.ok?r.json():null).then(data=>{if(!data?.frames?.length)return;frames=data.frames.slice(bars.length);frameMs=data.frameMs;syncWave();}).catch(()=>{});
 reduced.addEventListener('change',syncWave);document.addEventListener('visibilitychange',syncWave);
 const examples=document.createElement('div');examples.className='activity-examples';examples.setAttribute('role','group');examples.setAttribute('aria-label','Preview activity');
 const label=document.createElement('span');label.textContent='Try the idle notch';examples.append(label);
 for(const [key,name] of [['clock','Clock'],['music','Music'],['voice','Call'],['power','Charging'],['audio','Headphones']]){
  const button=document.createElement('button');button.type='button';button.textContent=name;button.dataset.example=key;button.setAttribute('aria-pressed','false');
  button.addEventListener('click',()=>{demo=key;collapse();update(last);});examples.append(button);
 }
 document.querySelector('#preview-caption').append(examples);
 function silhouette(){
  const width=pill.clientWidth,height=28;if(!width)return;
  const path=notchPath(width,height);
  const attached=root.classList.contains('notch');
  const svg=pill.querySelector('.pill-surface-notch');svg.setAttribute('viewBox',`0 0 ${width} ${height}`);svg.querySelectorAll('path').forEach(p=>p.setAttribute('d',path));
  const rounded=pill.querySelector('.pill-surface-rounded');rounded.setAttribute('viewBox',`0 0 ${width} ${height}`);const rect=rounded.querySelector('rect');rect.setAttribute('width',String(width-.5));rect.setAttribute('height',String(height-.5));rect.setAttribute('rx','14');
  pill.style.clipPath=attached?`path("${path}")`:'inset(0 round 14px)';
 }
 new ResizeObserver(silhouette).observe(pill);
 function update(value){
  last=value;
  const music=demo==='music'?{playing:true,available:true,title:'Rover (feat. Lil Tecca)',artwork:cover}:demo==='auto'?value.media:null;
  const playing=Boolean(music?.available&&music.playing),notice=demo==='power'||demo==='audio',voice=demo==='voice',active=playing||notice||voice;
  const expanded=root.dataset.expanded==='true';
  if(expanded!==wasExpanded)root.style.transition='none';
  const width=active||pill.classList.contains('running')?224:root.classList.contains('notch')?176:168;
  root.style.setProperty('--idle-width',`${width}px`);
  if(expanded!==wasExpanded){void root.offsetWidth;root.style.removeProperty('transition');wasExpanded=expanded;}
  pill.classList.toggle('is-activity',active);pill.classList.toggle('is-music',playing);pill.classList.toggle('is-notice',notice);pill.classList.toggle('is-voice',voice);
  activity.querySelector('.idle-title').textContent=playing?shortTrackTitle(music.title):voice?'Mic \u00b7 Discord':demo==='power'?'Charging':demo==='audio'?'My headphones':'';
  pill.title=playing?`${music.title}${music.artist?' — '+music.artist:''}`:'Open Yuta';
  pill.classList.toggle('has-notice-percent',demo==='power');
  const artwork=activity.querySelector('.idle-cover'),src=music?.artwork||cover;if(artwork.getAttribute('src')!==src)artwork.src=src;
  activity.querySelector('.idle-notice-icon').style.setProperty('--icon',`url('./assets/icons/${voice?'microphone':'headphones'}.svg')`);
  activity.querySelector('.idle-percent').textContent=demo==='power'?'72%':'';
  examples.querySelectorAll('button').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.example===demo)));
  playingWave=(playing||voice)&&!expanded;syncWave();
  silhouette();
 }
 return update;
}
