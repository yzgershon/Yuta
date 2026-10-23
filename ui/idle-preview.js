// Browser-only examples. The Windows app draws its real idle activity natively.
import {shortTrackTitle} from './media.js';
const cover = `data:image/svg+xml,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="64" height="64" fill="#4f3d74"/><circle cx="32" cy="29" r="23" fill="#ddbadf"/><path d="M0 44Q32 24 64 42V64H0Z" fill="#6f82a7"/></svg>')}`;
export function createIdlePreview(root,pill,collapse){
 let demo='auto',last={},wasExpanded;
 const activity=document.createElement('span');activity.className='idle-activity';activity.setAttribute('aria-hidden','true');
 activity.innerHTML='<img class="idle-cover" alt=""><span class="idle-notice-icon"></span><span class="idle-title"></span><span class="idle-percent"></span><span class="idle-wave"><i></i><i></i><i></i><i></i><i></i></span>';
 pill.append(activity);
 const examples=document.createElement('div');examples.className='activity-examples';examples.setAttribute('role','group');examples.setAttribute('aria-label','Preview activity');
 const label=document.createElement('span');label.textContent='Try the idle notch';examples.append(label);
 for(const [key,name] of [['clock','Clock'],['music','Music'],['power','Charging'],['audio','Headphones']]){
  const button=document.createElement('button');button.type='button';button.textContent=name;button.dataset.example=key;button.setAttribute('aria-pressed','false');
  button.addEventListener('click',()=>{demo=key;collapse();update(last);});examples.append(button);
 }
 document.querySelector('#preview-caption').append(examples);
 function silhouette(){
  const width=pill.clientWidth,height=28;if(!width)return;
  const shoulder=Math.min(20,height*4/9,width/4),r=Math.min(26,height-shoulder,(width-2*shoulder)/2),k=r*.55228475,right=width-shoulder,cy=height-r;
  const path=`M0 0H${width}C${width-shoulder*.75} 0 ${right} ${shoulder*.4375} ${right} ${shoulder}V${cy}C${right} ${cy+k} ${right-r+k} ${height} ${right-r} ${height}H${shoulder+r}C${shoulder+r-k} ${height} ${shoulder} ${cy+k} ${shoulder} ${cy}V${shoulder}C${shoulder} ${shoulder*.4375} ${shoulder*.75} 0 0 0Z`;
  const attached=root.classList.contains('notch');
  const svg=pill.querySelector('.pill-surface-notch');svg.setAttribute('viewBox',`0 0 ${width} ${height}`);svg.querySelectorAll('path').forEach(p=>p.setAttribute('d',path));
  const rounded=pill.querySelector('.pill-surface-rounded');rounded.setAttribute('viewBox',`0 0 ${width} ${height}`);const rect=rounded.querySelector('rect');rect.setAttribute('width',String(width-.5));rect.setAttribute('height',String(height-.5));rect.setAttribute('rx','14');
  pill.style.clipPath=attached?`path("${path}")`:'inset(0 round 14px)';
 }
 new ResizeObserver(silhouette).observe(pill);
 function update(value){
  last=value;
  const music=demo==='music'?{playing:true,available:true,title:'Rover (feat. Lil Tecca)',artwork:cover}:demo==='auto'?value.media:null;
  const playing=Boolean(music?.available&&music.playing),notice=demo==='power'||demo==='audio',active=playing||notice;
  const expanded=root.dataset.expanded==='true';
  if(expanded!==wasExpanded)root.style.transition='none';
  const width=active||pill.classList.contains('running')?224:root.classList.contains('notch')?176:168;
  root.style.setProperty('--idle-width',`${width}px`);
  if(expanded!==wasExpanded){void root.offsetWidth;root.style.removeProperty('transition');wasExpanded=expanded;}
  pill.classList.toggle('is-activity',active);pill.classList.toggle('is-music',playing);pill.classList.toggle('is-notice',notice);
  activity.querySelector('.idle-title').textContent=playing?shortTrackTitle(music.title):demo==='power'?'Charging':demo==='audio'?'My headphones':'';
  pill.title=playing?`${music.title}${music.artist?' — '+music.artist:''}`:'Open Yuta';
  pill.classList.toggle('has-notice-percent',demo==='power');
  const artwork=activity.querySelector('.idle-cover'),src=music?.artwork||cover;if(artwork.getAttribute('src')!==src)artwork.src=src;
  activity.querySelector('.idle-notice-icon').style.setProperty('--icon',`url('./assets/icons/${demo==='power'?'battery-high':'headphones'}.svg')`);
  activity.querySelector('.idle-percent').textContent=demo==='power'?'48%':'';
  examples.querySelectorAll('button').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.example===demo)));
  silhouette();
 }
 return update;
}
