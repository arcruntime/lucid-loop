import {WebSocket, WebSocketServer} from 'ws';
import {buildSessionStart} from './prompts.mjs';

// A separate endpoint leaves the team's LiveGym relay unchanged. No client can
// choose upstream URLs, voices, instructions or models. Only the loopback app.
export function attachMvpVoice(server,{apiKey,adjudicate,validateInput,characterInstructions,connectUpstream}={}) {
  const wss=new WebSocketServer({noServer:true,maxPayload:256*1024});
  let sessions=0;
  server.on('upgrade',(req,socket,head)=>{
    if(!['127.0.0.1','::1','::ffff:127.0.0.1'].includes(req.socket.remoteAddress)||req.url!=='/mvp-live'||req.headers.origin||!apiKey||sessions>=2){socket.destroy();return;}
    wss.handleUpgrade(req,socket,head,ws=>wss.emit('connection',ws));
  });
  server.on('close',()=>{for(const client of wss.clients)client.terminate();wss.close();});
  wss.on('connection',client=>{
    sessions++;
    let upstream,input,ready=false,closed=false,revision=0,sequence=0,pending=null,controller=null;
    let heard='',lastHearing=0,handledRevision=-1,delegationTimer,activeDelegation=null;
    let requests=[],speechApproved=false;
    const seen=new Set();
    const send=(ws,event)=>{if(ws?.readyState===WebSocket.OPEN&&ws.bufferedAmount<1024*1024)ws.send(JSON.stringify(event));};
    const status=message=>send(client,{type:'mvp.notice',message});
    const append=(type,content,id=null)=>send(upstream,{type,event_id:`mvp_${++sequence}`,delegation_id:id,content});
    const cleanup=()=>{if(closed)return;closed=true;sessions--;clearTimeout(startup);clearTimeout(duration);clearTimeout(delegationTimer);controller?.abort();upstream?.terminate();};
    const fail=()=>{status('Voice connection ended. You can retry or type instead.');cleanup();client.close();};
    const startup=setTimeout(fail,20000),duration=setTimeout(fail,5*60*1000);
    function changed() {
      speechApproved=false;revision++;controller?.abort();controller=null;pending=null;clearTimeout(delegationTimer);
    }
    async function decide(id) {
      if(closed||!ready||!heard.trim()||handledRevision===revision)return;
      // Wait for a brief gap in delivered transcript fragments. More speech
      // invalidates an unfinished decision rather than applying partial intent.
      if(Date.now()-lastHearing<650){delegationTimer=setTimeout(()=>decide(id),650);return;}
      requests=requests.filter(t=>Date.now()-t<60000);
      if(requests.length>=15){status('Please pause briefly before trying again.');return;}
      requests.push(Date.now());handledRevision=revision;const version=revision,message=heard.trim().slice(-800);
      controller=new AbortController();const active=controller;
      const timeout=setTimeout(()=>active.abort(),20000);
      status('Considering what you said…');
      try {
        const result=await adjudicate({...input,message},{signal:active.signal});
        if(closed||version!==revision||active.signal.aborted)return;
        pending={id:`turn_${++sequence}`,delegation:id,version,message,result};
        send(client,{type:'mvp.decision',id:pending.id,loop:input.state.loop,character:input.character,...result});
        // A speech result isn't sent until the actual game acknowledges commit.
      } catch {if(!closed&&!active.signal.aborted){status('Could not decide that request. Nothing changed; please retry.');append('session.instructions.append','The request could not be completed. Briefly ask the player to try again. Do not claim any action happened.',id);}}
      finally {clearTimeout(timeout);if(controller===active)controller=null;}
    }
    client.on('message',raw=>{
      try {
        const event=JSON.parse(raw);
        if(!input){
          if(event.type!=='mvp.start')return fail();
          input=validateInput({...event,message:'Begin conversation.'});
          const start=buildSessionStart(input.character);
          start.session.delegation={type:'client'};
          start.session.instructions=characterInstructions(input)+'\nVOICE: Stay in character. Delegate every player question or request before giving the substantive answer or agreeing to any action. You may give a brief natural hesitation, but never invent an agreement. The backend returns your approved in-character reply after the game commits the action. Deliver that reply naturally, preserving its facts and commitments. Never mention backend, delegation, AI, tools or game rules to the player. Wait for the player to speak first.';
          start.session.input=input.history.map(t=>({type:'message',role:t.role,content:[{type:t.role==='user'?'input_text':'output_text',text:t.content}]}));
          upstream=connectUpstream?connectUpstream():new WebSocket('wss://api.openai.com/v1/live/sessions',{headers:{Authorization:`Bearer ${apiKey}`},maxPayload:256*1024});
          upstream.on('open',()=>send(upstream,start));
          upstream.on('error',fail);upstream.on('close',fail);
          upstream.on('message',data=>{
            try {
              const e=JSON.parse(data);
              if(e.type==='session.started'){ready=true;clearTimeout(startup);send(client,{type:'mvp.ready'});}
              if(e.type==='session.input_transcript.delta'){
                changed();heard+=(e.delta??'');lastHearing=Date.now();
                if(heard.length>4000)return fail();
                // Always adjudicate completed speech, even if the live model skips delegation.
                // The LLM still interprets intent; only a validated game commit authorizes speech.
                delegationTimer=setTimeout(()=>decide(activeDelegation),900);
              }
              if(e.type==='session.delegation.created'&&e.delegation?.target==='client'&&typeof e.delegation.id==='string'&&!seen.has(e.delegation.id)){
                seen.add(e.delegation.id);activeDelegation=e.delegation.id;clearTimeout(delegationTimer);delegationTimer=setTimeout(()=>decide(e.delegation.id),700);
              }
              if(['session.output_transcript.delta','session.output_audio.delta'].includes(e.type)){if(speechApproved)send(client,e);}
              else if(['session.input_transcript.delta','session.input_audio.muted','session.input_audio.unmuted'].includes(e.type))send(client,e);
              if(e.type==='error')status('Voice service reported an error. No unconfirmed action was applied.');
              if(e.type==='session.closed'){send(client,e);cleanup();client.close();}
            }catch{fail();}
          });
          return;
        }
        if(event.type==='session.close'){send(upstream,{type:'session.close'});cleanup();client.close();return;}
        if(event.type==='mvp.commit'){
          if(event.state?.loop!==input.state.loop)return;
          const updated=validateInput({...input,state:event.state,message:'Continue.'});
          input.state=updated.state;
          if(!pending||event.id!==pending.id||pending.version!==revision)return;
          const p=pending;pending=null;
          if(event.accepted!==true){append('session.instructions.append','The situation changed before that action could happen. Do not claim it happened. Ask the player to try again.',p.delegation);return;}
          input=validateInput({...input,state:event.state,message:'Continue.'});
          input.history=[...input.history,{role:'user',content:p.message},{role:'assistant',content:p.result.reply}].slice(-12);
          heard='';activeDelegation=null;speechApproved=true;
          append('session.commentary.append',p.result.reply,p.delegation);
          send(client,{type:'mvp.committed',id:p.id});return;
        }
        if(!ready)return;
        if(['session.input_audio.mute','session.input_audio.unmute'].includes(event.type)){send(upstream,{type:event.type});return;}
        if(event.type==='session.input_audio.append'&&typeof event.audio==='string'&&event.audio.length<=64000&&/^[A-Za-z0-9+/]*={0,2}$/.test(event.audio)){
          const pcm=Buffer.from(event.audio,'base64');if(pcm.length&&pcm.length%2===0)send(upstream,{type:event.type,audio:event.audio});return;
        }
        fail();
      }catch{fail();}
    });
    client.on('close',cleanup);client.on('error',cleanup);
  });
  return wss;
}
