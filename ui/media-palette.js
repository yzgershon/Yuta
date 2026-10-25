const fallback={wash:'rgb(44, 47, 67)',deep:'rgb(19, 22, 32)',accent:'rgb(186, 196, 236)'};
const cache=new Map();
export function safeArtwork(value){return typeof value==='string'&&/^data:image\/(png|jpe?g|webp|gif|bmp);base64,/i.test(value)?value:null;}
export function paletteFromPixels(pixels){
 const buckets=new Map();
 for(let i=0;i<pixels.length;i+=4){
  if(pixels[i+3]<180)continue;
  const rgb=[pixels[i],pixels[i+1],pixels[i+2]],high=Math.max(...rgb),low=Math.min(...rgb);
  if(high<28||low>235)continue;
  const weight=.25+(high-low)/255,key=rgb.map(v=>v>>5).join(',');
  const bucket=buckets.get(key)||{weight:0,channels:[0,0,0]};
  bucket.weight+=weight;rgb.forEach((v,j)=>bucket.channels[j]+=v*weight);buckets.set(key,bucket);
 }
 const dominant=[...buckets.values()].sort((a,b)=>b.weight-a.weight)[0];
 if(!dominant)return fallback;
 const color=dominant.channels.map(v=>v/dominant.weight);
 const mix=(base,factor)=>`rgb(${color.map((v,i)=>Math.round(v*factor+base[i]*(1-factor))).join(', ')})`;
 return {wash:mix([20,22,29],.28),deep:mix([12,14,21],.1),accent:mix([248,246,255],.4)};
}
export function mediaPalette(artwork){return cache.get(safeArtwork(artwork))?.palette||fallback;}
export function paletteStyle(artwork){const p=mediaPalette(artwork);return `--media-wash:${p.wash};--media-deep:${p.deep};--media-accent:${p.accent}`;}
export function loadMediaPalette(value){
 const artwork=safeArtwork(value);if(!artwork)return Promise.resolve(false);
 if(cache.has(artwork))return cache.get(artwork).promise;
 const entry={palette:fallback,promise:null};
 entry.promise=new Promise(resolve=>{
  const image=new Image();image.decoding='async';
  image.onload=()=>{
   try{const canvas=document.createElement('canvas');canvas.width=canvas.height=24;const context=canvas.getContext('2d',{willReadFrequently:true});context.drawImage(image,0,0,24,24);entry.palette=paletteFromPixels(context.getImageData(0,0,24,24).data);resolve(true);}catch{resolve(false);}
  };
  image.onerror=()=>resolve(false);image.src=artwork;
 });
 cache.set(artwork,entry);
 if(cache.size>24)cache.delete(cache.keys().next().value);
 return entry.promise;
}
