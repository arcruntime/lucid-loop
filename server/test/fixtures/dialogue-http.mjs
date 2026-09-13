// Explicit automated transport fixture. Never launched by the player/server command.
import {createDialogueServer} from '../../src/dialogue.mjs';
const server=createDialogueServer({apiKey:'TEST_ONLY_NOT_A_REAL_KEY',fetchImpl:async(_url,args)=>{
 const body=JSON.parse(args.body); const message=body.input.at(-1).content;
 if(message==='DELAY_TEST')await new Promise(r=>setTimeout(r,2000));
 const actions=message==='DELAY_TEST'?['wait']:message.startsWith('Wait right')?['wait','private_approach']:message.startsWith('Theo may')?['prepare_intervention']:message.startsWith('Could you')?['music_intimate']:['follow'];
 return {ok:true,json:async()=>({status:'completed',output:[{content:[{type:'output_text',text:JSON.stringify({reply:'AUTOMATED FIXTURE RESPONSE: '+message,actions})}]}]})};
}});
server.listen(8083,'127.0.0.1',()=>console.log('Automated dialogue fixture on 8083; no OpenAI calls'));
