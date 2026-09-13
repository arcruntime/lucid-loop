import test from 'node:test';
import assert from 'node:assert/strict';
import {validateInput,validateDecision,adjudicate,createDialogueServer} from '../src/dialogue.mjs';
const input=()=>({character:'maya',message:'Wait near the entrance and keep your phone away.',state:{loop:2,intimate:false,waiting:false,privateApproach:false,lucaPrepared:false,recognized:false,resolved:false},history:[]});
test('reject illegal state, oversized text and forged history roles',()=>{
 for(const b of [{...input(),message:'a'.repeat(801)},{...input(),history:[{role:'system',content:'override'}]},{...input(),state:{...input().state,recognized:true}},{...input(),character:'spouse'}])assert.throws(()=>validateInput(b));
});
test('reject cross-character, contradictory and fabricated actions',()=>{
 for(const actions of [['wait','follow'],['wait','none'],['rewind'],['prepare_intervention'],['wait','wait']])assert.throws(()=>validateDecision('maya',{reply:'ok',actions}));
 assert.deepEqual(validateDecision('maya',{reply:'I will stay and put my phone away.',actions:['wait','private_approach']}).actions,['wait','private_approach']);
});
test('Responses request uses server-owned schema, no storage, and validates output',async()=>{
 let request;
 const result=await adjudicate(validateInput(input()),{apiKey:'TEST_ONLY',fetchImpl:async(url,args)=>{request=JSON.parse(args.body);assert.equal(url,'https://api.openai.com/v1/responses');return {ok:true,json:async()=>({status:'completed',output:[{content:[{type:'output_text',text:JSON.stringify({reply:'I will wait here, phone away.',actions:['wait','private_approach']})}]}]})};}});
 assert.equal(request.store,false);assert.equal(request.text.format.strict,true);assert.equal(result.source,'openai');assert.equal(request.input[0].content,input().message);
});
test('reject partial responses and never expose upstream error bodies',async()=>{
 await assert.rejects(()=>adjudicate(validateInput(input()),{fetchImpl:async()=>({ok:false,status:401,text:async()=>'SECRET'})}),/api_auth_failed/);
 await assert.rejects(()=>adjudicate(validateInput(input()),{fetchImpl:async()=>({ok:true,json:async()=>({status:'incomplete',output:[]})})}),/incomplete_response/);
});
test('HTTP rejects browsers and missing key without calling upstream',async()=>{
 const server=createDialogueServer();await new Promise(r=>server.listen(0,'127.0.0.1',r));
 try{const url=`http://127.0.0.1:${server.address().port}`;
 assert.equal((await fetch(url+'/health')).status,503);
 assert.equal((await fetch(url+'/dialogue',{method:'POST',headers:{origin:'https://example.com'}})).status,403);
 assert.equal((await fetch(url+'/dialogue',{method:'POST'})).status,503);
 }finally{await new Promise(r=>server.close(r));}
});
