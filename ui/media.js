import {escapeHtml as esc} from './core.js';
import {paletteStyle,safeArtwork} from './media-palette.js';

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

// The progress wave is a transport treatment, separate from the live audio meter.
const wavePath='M-24 9'+Array.from({length:24},()=> 'q6 -6 12 0t12 0').join('');
export function mediaCard(media,pending,icon,ib,compact=false){
 const m=media||{},available=Boolean(m.available),duration=Math.max(0,Number(m.duration)||0),position=mediaPosition(m);
 const artwork=safeArtwork(m.artwork),progress=duration?Math.min(100,position/duration*100):0;
 const disabled=capability=>!available||m[capability]===false?' disabled':'';
 const playbackCapability=m.playing?'canPause':'canPlay';
 const timeline=`<div class="media-timeline"><label class="sr-only" for="media-progress">Track progress</label><div class="media-seek${!duration?' is-indeterminate':''}" style="--media-progress:${progress}%"><div class="seek-track" aria-hidden="true"><div class="seek-played"><svg class="seek-wave" width="576" height="18" viewBox="0 0 576 18"><path d="${wavePath}"/></svg></div><span class="seek-dot"></span></div><input id="media-progress" type="range" min="0" max="${duration||1}" step="1" value="${Math.min(position,duration||0)}" aria-valuetext="${mediaTime(position)} of ${mediaTime(duration)}" ${!available||!duration||m.canSeek!==true?'disabled':''}></div><div class="media-times"><span id="media-elapsed">${duration?mediaTime(position):'\u2014:\u2014'}</span><span>${duration?mediaTime(duration):''}</span></div></div>`;
 const transport=`<div class="media-transport"><div class="media-buttons">${ib('media','Previous track','skip-back','data-command="previous"'+disabled('canPrevious'))}${ib('media',m.playing?'Pause music':'Play music',m.playing?'pause':'play','data-command="toggle"'+(!available||(m[playbackCapability]===false&&m.canToggle===false)?' disabled':''))}${ib('media','Next track','skip-forward','data-command="next"'+disabled('canNext'))}</div></div>`;
 return `<section class="media-card${pending?' is-pending':''}${available&&m.playing?' is-playing':''}" aria-label="Music player" style="${paletteStyle(artwork)}">
 <div class="media-body"><div class="album-art">${artwork?`<img src="${esc(artwork)}" alt="${esc(m.album?m.album+' cover':'Current track artwork')}" decoding="async">`:icon('music-notes')}</div><div class="media-info"><div class="media-heading"><span class="media-source">${icon('music-notes')}${esc(available?(m.sourceName||'Now playing'):'Music')}</span><span class="media-status" role="status">${pending?'Updating\u2026':available?(m.playing?'Playing':'Paused'):'Not playing'}</span></div><strong title="${esc(m.title||'')}">${esc(available?(m.title||'Untitled track'):'No active track')}</strong><span class="media-artist">${esc(available?(m.artist||'Unknown artist'):'Play music or a video to see it here.')}</span></div></div>
 ${compact?transport+timeline:timeline+transport}</section>`;
}
