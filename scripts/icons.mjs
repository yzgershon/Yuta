import {copyFile, mkdir} from 'node:fs/promises';
const names = ['house','sliders-horizontal','calendar-blank','chart-line','clipboard-text','squares-four','image','power','gear-six','caret-up','caret-down','caret-left','caret-right','arrow-right','arrow-up-right','plus','x','check','magnifying-glass','speaker-high','speaker-slash','sun','moon','wifi-high','bluetooth','headphones','play','pause','skip-back','skip-forward','timer','check-circle','circle','clock','list-checks','note-pencil','lightning','folder','file','star','push-pin','copy','trash','arrow-down','arrow-up','cpu','memory','hard-drives','cloud-sun','cloud-rain','snowflake','cloud','lock-simple','arrows-clockwise','desktop','keyboard','monitor','battery-high','dots-three','link','text-t','music-notes','rocket-launch','code','globe','shield-check','download-simple','stop'];
await mkdir('ui/assets/icons',{recursive:true});
for(const n of names) await copyFile(`node_modules/@phosphor-icons/core/assets/regular/${n}.svg`,`ui/assets/icons/${n}.svg`);
await copyFile('node_modules/@phosphor-icons/core/LICENSE','ui/assets/icons/LICENSE');
console.log(`Copied ${names.length} Phosphor icons.`);
