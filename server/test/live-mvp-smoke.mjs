// Explicit opt-in integration check. Sends synthetic speech to the local keyed server.
import {WebSocket} from 'ws';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
const bytes=await readFile('/tmp/btd-spoken-request.wav');let pcm;
for(let i=12;i+8<bytes.length;){const size=bytes.readUInt32LE(i+4);if(bytes.toString('ascii',i,i+4)==='data'){pcm=bytes.subarray(i+8,i+8+size);break;}i+=8+size+(size%2);}
assert.ok(pcm?.length);
const state={loop:2,intimate:false,waiting:false,privateApproach:false,lucaPrepared:false,recognized:false,resolved:false,inVip:false};
const ws=new WebSocket('ws://127.0.0.1:8082/mvp-live');
let input='',output='',actions=[],audio=[],tick,offset=-24000,committed=false,closed=false;
const events=[];
const completed=new Promise((resolve,reject)=>{
 const timeout=setTimeout(()=>{ws.terminate();reject(new Error('Live voice check timed out'));},45000);
 ws.on('error',reject);
 ws.on('open',()=>ws.send(JSON.stringify({type:'mvp.start',character:'maya',state,history:[]})));
 ws.on('message',raw=>{
  const e=JSON.parse(raw);events.push(e.type);
  if(e.type==='mvp.ready')tick=setInterval(()=>{
   let chunk=Buffer.alloc(960);if(offset>=0&&offset<pcm.length)pcm.copy(chunk,0,offset,Math.min(offset+960,pcm.length));offset+=960;
   if(ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify({type:'session.input_audio.append',audio:chunk.toString('base64')}));
  },20);
  if(e.type==='session.input_transcript.delta')input+=e.delta??'';
  if(e.type==='session.output_transcript.delta')output+=e.delta??'';
  if(e.type==='session.output_audio.delta')audio.push(Buffer.from(e.delta,'base64'));
  if(e.type==='mvp.decision'){
   actions=e.actions;state.waiting=actions.includes('wait');state.privateApproach=actions.includes('private_approach');
   ws.send(JSON.stringify({type:'mvp.commit',id:e.id,accepted:true,state}));
  }
  if(e.type==='mvp.committed'&&!committed){committed=true;setTimeout(()=>{clearInterval(tick);ws.close();},10000);}
  if(e.type==='mvp.notice')console.log(e.message);
 });
 ws.on('close',()=>{clearTimeout(timeout);clearInterval(tick);closed=true;resolve();});
});
try{await completed;}finally{clearInterval(tick);if(!closed)ws.terminate();}
const folder=new URL('../../.local/voice-smoke/',import.meta.url);await mkdir(folder,{recursive:true});
const raw=Buffer.concat(audio),header=Buffer.alloc(44);header.write('RIFF');header.writeUInt32LE(raw.length+36,4);header.write('WAVEfmt ',8);header.writeUInt32LE(16,16);header.writeUInt16LE(1,20);header.writeUInt16LE(1,22);header.writeUInt32LE(24000,24);header.writeUInt32LE(48000,28);header.writeUInt16LE(2,32);header.writeUInt16LE(16,34);header.write('data',36);header.writeUInt32LE(raw.length,40);
await writeFile(new URL('maya-live.wav',folder),Buffer.concat([header,raw]));
await writeFile(new URL('result.json',folder),JSON.stringify({input,output,actions,committed,events},null,2));
assert.ok(committed,'No committed live decision');assert.ok(actions.includes('wait'));assert.ok(actions.includes('private_approach'));assert.ok(output.trim());assert.ok(raw.length>48000);
console.log('BTD_LIVE_VOICE_OK',JSON.stringify({input,output,actions,audioSeconds:raw.length/48000}));
