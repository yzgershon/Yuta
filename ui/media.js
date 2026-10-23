import {escapeHtml as esc} from './core.js';

export function shortTrackTitle(title){
 if(typeof title!=='string'||!title.trim())return 'Music';
 const concise=title.replace(/\s*[\(\[]\s*(?:feat\.?|ft\.?|featuring)\s+[^\)\]]*[\)\]]/gi,'').replace(/\s+(?:[-–—]\s*)?(?:feat\.?|ft\.?|featuring)\s+.+$/i,'').trim();
 return concise||title.trim();
}

export function mediaPosition(media,now=Date.now()){
 if(!media?.available)return 0;
 const position=Math.max(0,Number(media.position)||0),duration=Math.max(0,Number(media.duration)||0);
 const rate=Number.isFinite(media.playbackRate)?Math.max(0,media.playbackRate):1;
 const elapsed=media.playing&&media.updatedAt?Math.max(0,(now-media.updatedAt)/1000)*rate:0;
 return duration?Math.min(duration,position+elapsed):position+elapsed;
}
export function mediaTime(seconds){const value=Math.max(0,Math.floor(Number(seconds)||0));return `${Math.floor(value/60)}:${String(value%60).padStart(2,'0')}`;}

export function mediaCard(media,pending,icon,ib){
 const m=media||{},available=Boolean(m.available),duration=Math.max(0,Number(m.duration)||0),position=mediaPosition(m);
 const artwork=typeof m.artwork==='string'&&/^data:image\/(png|jpe?g|webp|gif|bmp);base64,/i.test(m.artwork)?m.artwork:null;
 const disabled=capability=>!available||m[capability]===false?' disabled':'';
 const playbackCapability=m.playing?'canPause':'canPlay';
 return `<section class="media-card${pending?' is-pending':''}" aria-label="Music player"><div class="media-body"><div class="album-art">${artwork?`<img src="${esc(artwork)}" alt="${esc(m.album?m.album+' cover':'Current track artwork')}" decoding="async">`:icon('music-notes')}</div><div class="media-info"><div class="media-heading"><span class="media-source">${icon('music-notes')}${esc(available?(m.sourceName||'Now playing'):'Music')}</span><span class="media-status" role="status">${pending?'Updating…':available?(m.playing?'Playing':'Paused'):'Not playing'}</span></div><strong title="${esc(m.title||'')}">${esc(available?(m.title||'Untitled track'):'No active track')}</strong><span class="media-artist">${esc(available?(m.artist||'Unknown artist'):'Play a track in Spotify or another music app.')}</span></div></div><div class="media-transport"><div class="media-buttons">${ib('media','Previous track','skip-back','data-command="previous"'+disabled('canPrevious'))}${ib('media',m.playing?'Pause music':'Play music',m.playing?'pause':'play','data-command="toggle"'+(!available||(m[playbackCapability]===false&&m.canToggle===false)?' disabled':''))}${ib('media','Next track','skip-forward','data-command="next"'+disabled('canNext'))}</div></div><div class="media-timeline"><label class="sr-only" for="media-progress">Track progress</label><input id="media-progress" type="range" min="0" max="${duration||1}" step="1" value="${Math.min(position,duration||0)}" aria-valuetext="${mediaTime(position)} of ${mediaTime(duration)}" style="--media-progress:${duration?position/duration*100:0}%" ${!available||!duration||m.canSeek!==true?'disabled':''}><div class="media-times"><span id="media-elapsed">${duration?mediaTime(position):'—:—'}</span><span>${duration?mediaTime(duration):'—:—'}</span></div></div></section>`;
}
