using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
namespace LucidLoop.Gyms.Mvp
{
    public partial class FirstLoop
    {
        AudioSource storySpeaker;
        readonly Dictionary<string,AudioClip> storyAudio=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,string> storyIds=new Dictionary<string,string>{
            {"Finally. Come on—let's get closer to the dancefloor.","arrival"},
            {"Wait. That's Theo. He's married—and that is not his wife.","recognition"},
            {"Theo? Can we talk quietly for a second? My phone's away.","private"},
            {"Let's take a little space. Nobody needs an audience.","space"},
            {"...Fine. Just stop staring.","fine"},
            {"Theo. We need to talk about what you're doing.","confront"},
            {"No way. I'm recording this. Theo—seriously?","recording"},
            {"Keep your voice down. This is none of your business.","quiet"},
            {"Put the phone away. Give it to me.","phone"},
            {"Don't grab me.","grab"},
            {"Don't touch my phone.","touch"},
            {"Let go. You're done here.","intervene"},
            {"Get off me!","strike"},
            {"Not on my dancefloor.","rewind"},
            {"Got it. Let's give the room a little space.","intimate"},
            {"All right. Bringing the energy up.","aggressive"},
        };
        IEnumerator LoadStoryAudio()
        {
            storySpeaker=gameObject.AddComponent<AudioSource>();storySpeaker.spatialBlend=0;
            foreach(var pair in storyIds){
                var clip=Resources.Load<AudioClip>("MvpAudio/Story/"+pair.Value);
                if(!clip){using(var request=UnityWebRequestMultimedia.GetAudioClip("http://127.0.0.1:8082/story-audio/"+pair.Value,AudioType.WAV)){
                    request.timeout=2;yield return request.SendWebRequest();
                    if(request.result==UnityWebRequest.Result.Success)clip=DownloadHandlerAudioClip.GetContent(request);
                }}
                if(clip)storyAudio[pair.Key]=clip;
            }
        }
        void PlayStoryLine(string text){if(storySpeaker&&storyAudio.TryGetValue(text,out var clip)){storySpeaker.Stop();storySpeaker.clip=clip;storySpeaker.Play();}}
        void StopStoryLine(){if(storySpeaker)storySpeaker.Stop();}
    }
}
