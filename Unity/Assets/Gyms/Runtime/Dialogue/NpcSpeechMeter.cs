using System;
using UnityEngine;

namespace LucidLoop.Gyms
{
    // Bounded PCM history indexed by the dedicated speech player's CONSUMED clock.
    // Packet arrival only stores samples. It never advances the visible waveform.
    public sealed class NpcSpeechMeter
    {
        const int Capacity=24000*12;
        readonly float[] samples=new float[Capacity];
        public readonly float[] Bars=new float[35];
        long accepted, consumed;
        int generation;
        public float Level {get;private set;}
        public void Begin(int id){generation=id;accepted=consumed=0;Reset();}
        public void Reset(){Level=0;Array.Clear(Bars,0,Bars.Length);}
        public void Push(byte[] pcm,int id)
        {
            if(id!=generation || pcm==null || pcm.Length%2!=0)return;
            for(int i=0;i<pcm.Length;i+=2)samples[(accepted++)%Capacity]=(short)(pcm[i]|pcm[i+1]<<8)/32768f;
        }
        public void Advance(int id,long position,bool ended)
        {
            if(id!=generation)return;
            if(ended){Reset();return;}
            position=Math.Min(position,accepted);
            if(position<=consumed){Reset();return;}
            long from=Math.Max(consumed,Math.Max(accepted-Capacity,position-2048));
            consumed=position;
            double total=0;int count=(int)(position-from);
            Array.Clear(Bars,0,Bars.Length);
            for(int i=0;i<count;i++)
            {
                float v=samples[(from+i)%Capacity];total+=v*v;
                int bar=Math.Min(Bars.Length-1,i*Bars.Length/Math.Max(1,count));Bars[bar]+=v*v;
            }
            for(int b=0;b<Bars.Length;b++)Bars[b]=Mathf.Sqrt(Bars[b]/Mathf.Max(1,count/(float)Bars.Length));
            Level=count>0?(float)Math.Sqrt(total/count):0;
        }
        // For a complete clip, call only with GetOutputData from its dedicated AudioSource.
        public void Output(float[] data,bool playing)
        {
            Reset();if(!playing || data==null)return;
            double sum=0;
            for(int i=0;i<data.Length;i++){float v=data[i];sum+=v*v;Bars[i*Bars.Length/data.Length]+=v*v;}
            for(int i=0;i<Bars.Length;i++)Bars[i]=Mathf.Sqrt(Bars[i]/Mathf.Max(1,data.Length/(float)Bars.Length));
            Level=(float)Math.Sqrt(sum/Math.Max(1,data.Length));
        }
    }
}
