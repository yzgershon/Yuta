import {test} from 'node:test';
import assert from 'node:assert/strict';
import {shortTrackTitle} from '../ui/media.js';

test('idle music titles omit feature credits while preserving track versions',()=>{
 for(const title of ['Rover (feat. Lil Tecca)','Rover [FEAT. Lil Tecca]','Rover - ft. Lil Tecca'])assert.equal(shortTrackTitle(title),'Rover');
 assert.equal(shortTrackTitle('Song (Live)'),'Song (Live)');
 assert.equal(shortTrackTitle('Song (Remix)'),'Song (Remix)');
 assert.equal(shortTrackTitle(''),'Music');
});
import {timerRemaining,parseTargets,filterClips,dateKey,weekDays,escapeHtml} from '../ui/core.js';
test('timer uses wall time across suspend, page changes and reload',()=>{assert.equal(timerRemaining({running:true,endsAt:10000},3000),7);assert.equal(timerRemaining({running:true,endsAt:10000},20000),0);assert.equal(timerRemaining({running:false,remaining:45},20000),45);});
test('routine destinations accept Windows paths and websites but reject commands',()=>{assert.deepEqual(parseTargets('https://example.com\nC:\\Dev'),['https://example.com','C:\\Dev']);assert.throws(()=>parseTargets('powershell -Command doSomething'));assert.throws(()=>parseTargets('javascript:alert(1)'));assert.throws(()=>parseTargets(''));assert.throws(()=>parseTargets(Array(13).fill('https://example.com').join('\n')));});
test('clipboard filter combines type, search and pinned order',()=>{const items=[{type:'text',label:'one',text:'first',pinned:false},{type:'link',label:'site',text:'https://one.test',pinned:true},{type:'text',label:'two',text:'one note',pinned:true}];assert.deepEqual(filterClips(items,'one','text').map(x=>x.label),['two','one']);});
test('calendar starts on Monday across year boundaries without changing input',()=>{const d=new Date(2027,0,1,12),week=weekDays(d);assert.equal(dateKey(week[0]),'2026-12-28');assert.equal(dateKey(week[6]),'2027-01-03');assert.equal(dateKey(d),'2027-01-01');});
test('untrusted clipboard text cannot become UI markup',()=>{assert.equal(escapeHtml('<img src=x onerror="bad()">'), '&lt;img src=x onerror=&quot;bad()&quot;&gt;');});
