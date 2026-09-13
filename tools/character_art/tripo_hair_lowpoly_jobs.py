"""One authorized static native-hair reduction; never retries task creation."""
from __future__ import annotations
import argparse
import json
import re
import shutil
import sys
from tripo_bust_jobs import ApiError, PipelineError, ROOT, TripoApi, load_api_key, now, sha256, write_json
from tripo_parts_jobs import clean, copy_originals, exclusive_json, fingerprint, run_cli

PUBLIC=ROOT/'art/generated/characters/ren/parts-workflow-v1/tripo-hair-lowpoly-v1'
PRIVATE=ROOT/'.local/character-art/tripo-hair-lowpoly-v1'
SOURCE=ROOT/'art/generated/characters/ren/parts-generation-v1/tripo/hair/originals/model.fbx'
SOURCE_HASH='9f8c2cb79c763bb048b4c0b62c058e2357802e86012badf0ce0a15f240a083c9'
SOURCE_TASK='1a8b7598-cddf-4b6c-a344-9e9ae16dcd5d'
SETTINGS=dict(model='v2.0',face_limit=8000,quad=False,bake=False)
ENDPOINT='/mesh/decimate'

def identity():
    if not SOURCE.is_file() or sha256(SOURCE)!=SOURCE_HASH:
        raise PipelineError('Native hair source missing or changed; no task may be submitted.')
    return dict(source_task_id=SOURCE_TASK,sources=[dict(role='immutable_native_hair',path=SOURCE.relative_to(ROOT).as_posix(),sha256=SOURCE_HASH,bytes=SOURCE.stat().st_size)],requested_settings=SETTINGS)

def save(state,secrets=()):
    state['updated_utc']=now()
    write_json(PRIVATE/'state.json',state)
    public={k:v for k,v in state.items() if k!='latest_response'}
    write_json(PUBLIC/'provenance.json',clean(public,secrets))
    if state.get('latest_response'):write_json(PUBLIC/'response.json',clean(state['latest_response'],secrets))

def run(mode):
    PRIVATE.mkdir(parents=True,exist_ok=True)
    lock=PRIVATE/'invocation.lock';exclusive_json(lock,dict(started_utc=now()))
    secrets=[];state={}
    try:
        ident=identity();state_path=PRIVATE/'state.json';intent_path=PRIVATE/'submission-intent.json'
        if state_path.exists():
            state=json.loads(state_path.read_text(encoding='utf-8'))
            if state.get('fingerprint')!=fingerprint(ident):raise PipelineError('Source/settings changed; saved job cannot be reused.')
        else:
            if intent_path.exists():raise PipelineError('Intent exists without state; reconcile instead of resubmitting.')
            state={**ident,'schema_version':1,'fingerprint':fingerprint(ident),'status':'PREPARED','created_utc':now(),'task_id':None,
              'authorization':'Parent authorized exactly one static native-hair retopology experiment at expected30credits after local8k hair remained ribbon-like; no head upload or second paid job.',
              'documentation':'https://developers.tripo3d.ai/en/docs/mesh-decimate',
              'transport':'One non-retrying V3 creation POST after durable exclusive intent; official CLI watches/downloads only.',
              'scope':'Separate static comparison; original source files unchanged. Count, topology, UV and likeness remain review items.'}
        if intent_path.exists() and state.get('intent_sha256')!=sha256(intent_path):raise PipelineError('Immutable intent hash changed.')
        if mode=='submit' and (intent_path.exists() or state.get('task_id') or state['status']!='PREPARED'):
            raise PipelineError('Submission already attempted. Use resume for known task; do not resubmit.')
        if mode=='resume' and not state.get('task_id'):raise PipelineError('No known task ID. Reconcile; automatic resubmission forbidden.')
        save(state)
        if mode=='prepare':
            print('PREPARED: no upload or paid request made.');return 0
        key=load_api_key();secrets.append(key);api=TripoApi(key)
        if mode=='submit':
            code,doctor,_=run_cli(['doctor','--json','--no-open'],key,PRIVATE)
            state['preflight']=dict(checked_utc=now(),doctor=doctor,cli_exit_code=code)
            save(state,secrets)
            if code or not doctor.get('ok'):raise PipelineError('CLI preflight failed; nothing submitted.')
            state['balance_before']=api.request('GET','/account/balance')['data']
            if float(state['balance_before'].get('balance',0))<30:raise PipelineError('Insufficient balance for30credit trial.')
            payload={'input':SOURCE_TASK,**SETTINGS}
            intent=dict(created_utc=now(),fingerprint=state['fingerprint'],endpoint=ENDPOINT,request=payload)
            exclusive_json(intent_path,intent)
            write_json(PUBLIC/'submission-intent.json',intent)
            state.update(status='SUBMITTING',intent_sha256=sha256(intent_path));save(state,secrets)
            try:
                response=api.request('POST',ENDPOINT,payload)
                task=response.get('data',{}).get('task_id')
                if not isinstance(task,str) or not re.fullmatch(r'(?:task_)?[A-Za-z0-9-]{8,80}',task):raise ApiError('No valid task identity returned; acceptance unknown.')
                state.update(task_id=task,status='SUBMITTED',latest_response=response)
                write_json(state_path,state)
            except ApiError as error:
                state.update(status='REJECTED' if error.definite_rejection else 'SUBMISSION_UNKNOWN',error=str(error))
                if error.definite_rejection:
                    target=PUBLIC/'prepared-fallback-input/native-hair.fbx';target.parent.mkdir(parents=True,exist_ok=True)
                    if target.exists() and sha256(target)!=SOURCE_HASH:raise PipelineError('Fallback input exists with different bytes.')
                    if not target.exists():shutil.copyfile(SOURCE,target)
                    state['fallback_prepared']=dict(path=target.relative_to(ROOT).as_posix(),sha256=sha256(target),uploaded=False,note='Immutable FBX prepared only; no second creation POST authorized by this helper.')
                save(state,secrets);raise
            save(state,secrets);print('Submitted once: '+state['task_id'],flush=True)
        code,result,_=run_cli(['task','watch',state['task_id'],'--download','-o',str(PRIVATE/'downloads'),'--timeout','3600','--json','--no-open'],key,PRIVATE)
        state.update(cli_exit_code=code,latest_response=result)
        if code or result.get('status')!='success' or result.get('task_id')!=state['task_id']:
            state['status']='CLI_INCOMPLETE';save(state,secrets);print('Incomplete: resume saved task, never resubmit.');return code or 1
        state['downloads']=copy_originals(result,PRIVATE,PUBLIC)
        state['credits_consumed']=result.get('credits_consumed')
        state['balance_after']=api.request('GET','/account/balance')['data']
        state['status']='COMPLETE_AWAITING_STATIC_REVIEW'
        identity();save(state,secrets)
        write_json(PUBLIC/'receipt.json',dict(task_id=state['task_id'],status=result['status'],credits_consumed=state['credits_consumed'],checked_utc=now()))
        print(f"Downloaded actual result: {state['credits_consumed']} credits.",flush=True);return 0
    finally:lock.unlink(missing_ok=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('mode',choices=['prepare','submit','resume'])
    try:raise SystemExit(run(parser.parse_args().mode))
    except PipelineError as error:
        print(str(error),file=sys.stderr);raise SystemExit(1)
