using System;
namespace LucidLoop.Gyms
{
    public sealed class AudioRingBuffer
    {
        readonly float[] buffer;
        readonly object gate=new object();
        int read,write,count;
        public AudioRingBuffer(int capacity)
        {if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));buffer=new float[capacity];}
        public int Count {get{lock(gate)return count;}}
        public void WritePcm16(byte[] bytes)
        {
            if(bytes==null||bytes.Length%2!=0)throw new ArgumentException("PCM16 must contain complete samples.");
            lock(gate)for(int i=0;i<bytes.Length;i+=2)
            {
                if(count==buffer.Length){read=(read+1)%buffer.Length;count--;}
                buffer[write]=(short)(bytes[i]|bytes[i+1]<<8)/32768f;
                write=(write+1)%buffer.Length;count++;
            }
        }
        public float Read()
        {lock(gate){if(count==0)return 0;float v=buffer[read];read=(read+1)%buffer.Length;count--;return v;}}
        public void Clear() {lock(gate){read=write=count=0;}}
        public static byte[] Encode(float[] samples, int count)
        {
            if(samples==null||count<0||count>samples.Length)throw new ArgumentOutOfRangeException(nameof(count));
            var bytes=new byte[count*2];for(int i=0;i<count;i++)
            {float f=Math.Clamp(samples[i],-1,1);short v=(short)(f<0?f*32768:f*32767);bytes[i*2]=(byte)v;bytes[i*2+1]=(byte)(v>>8);}return bytes;
        }
    }
}
