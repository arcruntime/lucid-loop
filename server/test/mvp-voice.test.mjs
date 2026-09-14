import test from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import {once,EventEmitter} from 'node:events';
import {WebSocket} from 'ws';
import {attachMvpVoice} from '../src/mvp-voice.mjs';
import {validateInput,characterInstructions} from '../src/dialogue.mjs';
const state={loop:2,intimate:false,waiting:false,privateApproach:false,lucaPrepared:false,recognized:false,resolved:false};
const pause=ms=>new Promise(r=>setTimeout(r,ms));
class Upstream extends EventEmitter {
  readyState=WebSocket.OPEN;bufferedAmount=0;events=[];
  send(text){this.events.push(JSON.parse(text));}
  terminate(){this.readyState=WebSocket.CLOSED;this.emit('close');}
  push(event){this.emit('message',JSON.stringify(event));}
}
async function setup(t,decide,history=[]){
  const server=http.createServer();const upstream=new Upstream();
  attachMvpVoice(server,{apiKey:'fixture-only',validateInput,characterInstructions,adjudicate:decide,connectUpstream:()=>{setImmediate(()=>upstream.emit('open'));return upstream;}});
  server.listen(0,'127.0.0.1');await once(server,'listening');
  const client=new WebSocket(`ws://127.0.0.1:${server.address().port}/mvp-live`);const events=[];
  client.on('message',raw=>events.push(JSON.parse(raw)));await once(client,'open');
  t.after(async()=>{client.terminate();upstream.terminate();server.closeAllConnections();await new Promise(r=>server.close(r));});
  client.send(JSON.stringify({type:'mvp.start',character:'maya',state,history}));await pause(30);
  upstream.push({type:'session.started'});await pause(20);
  return {client,upstream,events};
}
test('voice retains cast and waits for game commit before speaking agreed result',async t=>{
  let calls=0;const x=await setup(t,async input=>{calls++;assert.equal(input.message,'Please wait here.');return {reply:'Sure. Right here.',actions:['wait'],source:'openai'};});
  const start=x.upstream.events[0];assert.equal(start.session.audio.output.voice,'gleam');assert.equal(start.session.delegation.type,'client');
  assert.match(start.session.instructions,/not spotted anything unusual/);
  x.upstream.push({type:'session.input_transcript.delta',delta:'Please wait here.'});
  x.upstream.push({type:'session.delegation.created',delegation:{type:'delegation',target:'client',id:'d1'}});await pause(900);
  const decision=x.events.find(e=>e.type==='mvp.decision');assert.ok(decision);assert.equal(calls,1);
  assert.equal(x.upstream.events.some(e=>e.type==='session.commentary.append'),false);
  x.client.send(JSON.stringify({type:'mvp.commit',id:decision.id,accepted:true,state:{...state,waiting:true}}));await pause(40);
  assert.equal(x.upstream.events.filter(e=>e.type==='session.commentary.append').length,1);
  x.client.send(JSON.stringify({type:'mvp.commit',id:decision.id,accepted:true,state:{...state,waiting:true}}));
  x.upstream.push({type:'session.delegation.created',delegation:{type:'delegation',target:'client',id:'d1'}});await pause(750);
  assert.equal(calls,1);assert.equal(x.upstream.events.filter(e=>e.type==='session.commentary.append').length,1);
});
test('speech correction cancels pending adjudication, close cancels remaining work',async t=>{
  const signals=[];const x=await setup(t,async(input,{signal})=>{signals.push(signal);await pause(1000);return {reply:'Sure.',actions:['wait'],source:'openai'};});
  x.upstream.push({type:'session.input_transcript.delta',delta:'Wait here.'});x.upstream.push({type:'session.delegation.created',delegation:{type:'delegation',target:'client',id:'d2'}});await pause(950);
  x.upstream.push({type:'session.input_transcript.delta',delta:' Actually, come with me.'});await pause(20);assert.equal(signals[0].aborted,true);
  await pause(950);x.client.close();await pause(50);assert.equal(signals[1].aborted,true);
  await pause(1100);assert.equal(x.events.some(e=>e.type==='mvp.decision'),false);
});
test('typed current-loop history seeds voice, without backend instructions in user role',async t=>{
  const history=[{role:'user',content:'Please keep your phone away.'},{role:'assistant',content:'Okay, sure.'}];
  const x=await setup(t,async()=>({reply:'Okay.',actions:['none'],source:'openai'}),history);
  assert.deepEqual(x.upstream.events[0].session.input.map(x=>({role:x.role,content:x.content[0].text})),history);
  assert.equal(x.upstream.events[0].session.input[1].content[0].type,'output_text');
});

test('offline-night request is adjudicated without delegation and speech waits for commit',async t=>{
  let calls=0;
  const request="Hey Maya, I've got a great idea. Let's have kind of an offline night tonight. No phones. Is that a deal";
  const x=await setup(t,async input=>{calls++;assert.equal(input.message,request);return {reply:"Okay, I'll keep it tucked away.",actions:['private_approach'],source:'openai'};});
  x.upstream.push({type:'session.input_transcript.delta',delta:request});
  x.upstream.push({type:'session.output_transcript.delta',delta:"Sure, I'll keep it tucked away."});
  x.upstream.push({type:'session.output_audio.delta',delta:'AAAA'});
  await pause(1100);
  const decision=x.events.find(e=>e.type==='mvp.decision');assert.ok(decision);assert.equal(calls,1);
  assert.equal(x.events.some(e=>e.type.startsWith('session.output_')),false);
  x.client.send(JSON.stringify({type:'mvp.commit',id:decision.id,accepted:true,state:{...state,privateApproach:true}}));await pause(40);
  const reply=x.upstream.events.find(e=>e.type==='session.commentary.append');assert.equal(reply.delegation_id,null);
  x.upstream.push({type:'session.output_transcript.delta',delta:reply.content});await pause(20);
  assert.ok(x.events.some(e=>e.type==='session.output_transcript.delta'));
  // A late delegation must not execute the already completed request a second time.
  x.upstream.push({type:'session.delegation.created',delegation:{target:'client',id:'late'}});await pause(750);assert.equal(calls,1);
});
