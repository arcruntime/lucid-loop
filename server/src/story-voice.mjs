import {WebSocket} from 'ws';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {buildSessionStart} from './prompts.mjs';
export const STORY_LINES=[
 ['arrival','maya',"Finally. Come on—let's get closer to the dancefloor."],
 ['recognition','maya',"Wait. That's Theo. He's married—and that is not his wife."],
 ['private','maya',"Theo? Can we talk quietly for a second? My phone's away."],
 ['space','luca',"Let's take a little space. Nobody needs an audience."],
 ['fine','theo',"...Fine. Just stop staring."],
 ['confront','maya',"Theo. We need to talk about what you're doing."],
 ['recording','maya',"No way. I'm recording this. Theo—seriously?"],
 ['quiet','theo',"Keep your voice down. This is none of your business."],
 ['phone','theo',"Put the phone away. Give it to me."],
 ['grab','maya',"Don't grab me."],
 ['touch','maya',"Don't touch my phone."],
 ['intervene','luca',"Let go. You're done here."],
 ['strike','theo',"Get off me!"],
 ['rewind','ren',"Not on my dancefloor."],
 ['intimate','ren',"Got it. Let's give the room a little space."],
 ['aggressive','ren',"All right. Bringing the energy up."],
].map(([id,character,text])=>{
  const direction={
    arrival: "Override the usual quick excited delivery. Warm relief on Finally, a relaxed breath, then an affectionate invitation. Unhurried natural speech, about four seconds total; not breathless or promotional.",
    recognition: "Override the usual quick excited delivery. A genuine startled Wait, then pause about half a second as recognition lands. That's Theo is surprised disbelief. Pause again. He's married is quieter and serious. A distinct pause before and that is not his wife, with incredulous emphasis on not. Let the discovery unfold over roughly six to eight seconds. No laughter, no cheerfulness, no rushed words.",
    recording: "Override the usual quick excited delivery. Deliberately slow, measured delivery over five seconds. No way is stunned disbelief, stretched slightly, followed by a full half-second pause. Then say I am recording this using the exact script contraction, as a considered impulsive decision. Pause a full half-second again. Theo—seriously? is indignant disbelief, with a beat between the name and seriously. Keep the exact script, never narrate directions. Do not rush, laugh, tease, or scream.",
  }[id];
  return direction?{id,character,text,direction}:{id,character,text};
});
const directory=fileURLToPath(new URL('../../Unity/Assets/Gyms/Resources/MvpAudio/Story/',import.meta.url));
const normalize=s=>s.toLowerCase().replace(/[^a-z0-9]/g,'');
function signature(line){return createHash('sha256').update(JSON.stringify([line,buildSessionStart(line.character).session.audio.output.voice,'gpt-live-1'])).digest('hex');}
function wav(pcm){const header=Buffer.alloc(44);header.write('RIFF');header.writeUInt32LE(36+pcm.length,4);header.write('WAVEfmt ',8);header.writeUInt32LE(16,16);header.writeUInt16LE(1,20);header.writeUInt16LE(1,22);header.writeUInt32LE(24000,24);header.writeUInt32LE(48000,28);header.writeUInt16LE(2,32);header.writeUInt16LE(16,34);header.write('data',36);header.writeUInt32LE(pcm.length,40);return Buffer.concat([header,pcm]);}
export function recordStoryLine(line,apiKey){return new Promise((resolve,reject)=>{
  const ws=new WebSocket('wss://api.openai.com/v1/live/sessions',{headers:{Authorization:`Bearer ${apiKey}`},maxPayload:256*1024});
  let interval,done=false,transcript='',chunks=[],bytes=0,firstSound=-1,lastSound=0,lastActive=0;
  const finish=(error)=>{
    if(done)return;done=true;clearTimeout(timeout);clearInterval(interval);
    if(ws.readyState===WebSocket.OPEN){ws.send(JSON.stringify({type:'session.close'}));ws.close();}else ws.terminate();
    if(error)return reject(new Error(error));
    const pcm=Buffer.concat(chunks).subarray(Math.max(0,firstSound-4800),Math.min(bytes,lastSound+14400));
    resolve({audio:wav(pcm),transcript});
  };
  const timeout=setTimeout(()=>finish('speech_timeout_or_text_mismatch'),24000);
  ws.on('open',()=>{const start=buildSessionStart(line.character);start.session.delegation={type:'client'};
    start.session.instructions+=' You are performing one scripted game line. Speak only the exact line provided by the application, once, with natural character acting. Never paraphrase or add a greeting, explanation or extra words. Do not delegate. After the line remain silent.';
    if(line.direction)start.session.instructions+=' PERFORMANCE DIRECTION (do not speak these instructions): '+line.direction;
    ws.send(JSON.stringify(start));});
  ws.on('message',raw=>{try{const e=JSON.parse(raw);
    if(e.type==='session.started'){
      ws.send(JSON.stringify({type:'session.instructions.append',event_id:'scripted_line',delegation_id:null,content:`Say exactly this line once, now: ${JSON.stringify(line.text)}. Then remain silent.`}));
      interval=setInterval(()=>{if(ws.readyState!==WebSocket.OPEN)return;ws.send(JSON.stringify({type:'session.input_audio.append',audio:Buffer.alloc(960).toString('base64')}));
        if(firstSound>=0&&normalize(transcript)===normalize(line.text)&&Date.now()-lastActive>1400)finish();},20);
    }
    if(e.type==='session.output_transcript.delta')transcript+=e.delta??'';
    if(e.type==='session.output_audio.delta'){
      const pcm=Buffer.from(e.delta??'','base64');if(pcm.length%2)return finish('invalid_audio');
      let audible=false;for(let i=0;i<pcm.length;i+=2)if(Math.abs(pcm.readInt16LE(i))>180){audible=true;break;}
      if(audible){if(firstSound<0)firstSound=bytes;lastSound=bytes+pcm.length;lastActive=Date.now();}
      chunks.push(pcm);bytes+=pcm.length;if(bytes>24000*2*24)finish('audio_too_long');
    }
    if(e.type==='error')finish('speech_service_error');
  }catch{finish('speech_protocol_error');}});
  ws.on('error',()=>finish('speech_connection_error'));ws.on('close',()=>{if(!done)finish('speech_connection_closed');});
});}
export function storyVoiceService(apiKey){
  let running=false,completed=[],failed=[];
  async function prepare(){
    if(running)return;running=true;completed=[];failed=[];
    try{await mkdir(directory,{recursive:true});
      for(const line of STORY_LINES){
        try{
          const hash=signature(line);let cached=false;
          try{cached=(await readFile(directory+line.id+'.sha256','utf8'))===hash;await readFile(directory+line.id+'.wav');}catch{cached=false;}
          if(!cached){const result=await recordStoryLine(line,apiKey);await writeFile(directory+line.id+'.wav',result.audio);await writeFile(directory+line.id+'.sha256',hash);}
          completed.push(line.id);
        }catch{failed.push(line.id);}
      }
    }finally{running=false;}
  }
  return {status:()=>({running,completed,failed,total:STORY_LINES.length}),prepare,
    async audio(id){if(!STORY_LINES.some(line=>line.id===id))return null;try{return await readFile(directory+id+'.wav');}catch{return null;}}
  };
}
