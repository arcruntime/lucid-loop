using System;
using UnityEngine;
using UnityEngine.AI;
namespace LucidLoop.Gyms
{
    [DisallowMultipleComponent]
    public sealed class LoopRewindActor : MonoBehaviour
    {
        [Min(.1f)] public float HistorySeconds=6;
        [Range(1,60)] public int SampleRate=20;
        [Tooltip("Optional local visual transforms (e.g. articulated limbs). Root is always recorded.")]
        public Transform[] VisualTransforms=Array.Empty<Transform>();
        public SpriteRenderer[] Sprites=Array.Empty<SpriteRenderer>();
        [Tooltip("Only explicitly supported animation/AI writers. Do not include billboard-facing scripts.")]
        public Behaviour[] MovementWriters=Array.Empty<Behaviour>();
        public sealed class Pose
        {
            public Vector3 Position,Scale; public Quaternion Rotation;
            public Vector3[] LocalPositions,LocalScales; public Quaternion[] LocalRotations; public bool[] VisualActive;
            public Sprite[] Frames; public bool[] SpriteEnabled,FlipX,FlipY; public Color[] Colors;
            public Pose(int transforms,int sprites){LocalPositions=new Vector3[transforms];LocalRotations=new Quaternion[transforms];LocalScales=new Vector3[transforms];VisualActive=new bool[transforms];Frames=new Sprite[sprites];SpriteEnabled=new bool[sprites];FlipX=new bool[sprites];FlipY=new bool[sprites];Colors=new Color[sprites];}
        }
        Pose[] samples; double[] times; int next,count; double nextSample;
        LoopActivityLease lease; NavMeshAgent agent; Rigidbody body; bool wasKinematic; Vector3 velocity,angularVelocity;
        public int SampleCount=>count;
        public bool IsPaused=>lease!=null;
        void Start(){Initialize();Record(Time.unscaledTimeAsDouble);}
        public void Initialize()
        {
            if(Sprites.Length==0)Sprites=GetComponentsInChildren<SpriteRenderer>(true);
            if(samples!=null && samples[0].LocalPositions.Length==VisualTransforms.Length && samples[0].Frames.Length==Sprites.Length)return;
            count=next=0;
            int capacity=Mathf.Clamp(Mathf.CeilToInt(HistorySeconds*SampleRate)+1,2,3601);
            samples=new Pose[capacity];times=new double[capacity];for(int i=0;i<capacity;i++)samples[i]=NewPose();
            agent=GetComponent<NavMeshAgent>();body=GetComponent<Rigidbody>();
        }
        Pose NewPose()=>new Pose(VisualTransforms.Length,Sprites.Length);
        void LateUpdate(){if(!IsPaused && Time.unscaledTimeAsDouble>=nextSample){Record(Time.unscaledTimeAsDouble);nextSample=Time.unscaledTimeAsDouble+1.0/Mathf.Max(1,SampleRate);}}
        public void Record(double time){Initialize();CaptureInto(samples[next]);times[next]=time;next=(next+1)%samples.Length;count=Mathf.Min(count+1,samples.Length);}
        public Pose Capture(){Initialize();var pose=NewPose();CaptureInto(pose);return pose;}
        void CaptureInto(Pose pose)
        {
            pose.Position=transform.position;pose.Rotation=transform.rotation;pose.Scale=transform.localScale;
            for(int i=0;i<VisualTransforms.Length;i++)if(VisualTransforms[i]){pose.LocalPositions[i]=VisualTransforms[i].localPosition;pose.LocalRotations[i]=VisualTransforms[i].localRotation;pose.LocalScales[i]=VisualTransforms[i].localScale;pose.VisualActive[i]=VisualTransforms[i].gameObject.activeSelf;}
            for(int i=0;i<Sprites.Length;i++)if(Sprites[i]){pose.Frames[i]=Sprites[i].sprite;pose.SpriteEnabled[i]=Sprites[i].enabled;pose.FlipX[i]=Sprites[i].flipX;pose.FlipY[i]=Sprites[i].flipY;pose.Colors[i]=Sprites[i].color;}
        }
        public void Apply(Pose pose)=>Blend(pose,pose,0);
        void Blend(Pose a,Pose b,float t)
        {
            transform.SetPositionAndRotation(Vector3.Lerp(a.Position,b.Position,t),Quaternion.Slerp(a.Rotation,b.Rotation,t));
            transform.localScale=Vector3.Lerp(a.Scale,b.Scale,t);
            for(int i=0;i<VisualTransforms.Length;i++)if(VisualTransforms[i]){VisualTransforms[i].localPosition=Vector3.Lerp(a.LocalPositions[i],b.LocalPositions[i],t);VisualTransforms[i].localRotation=Quaternion.Slerp(a.LocalRotations[i],b.LocalRotations[i],t);VisualTransforms[i].localScale=Vector3.Lerp(a.LocalScales[i],b.LocalScales[i],t);VisualTransforms[i].gameObject.SetActive((t>=1?b:a).VisualActive[i]);}
            var selected=t>=1?b:a;
            for(int i=0;i<Sprites.Length;i++)if(Sprites[i]){Sprites[i].sprite=selected.Frames[i];Sprites[i].enabled=selected.SpriteEnabled[i];Sprites[i].flipX=selected.FlipX[i];Sprites[i].flipY=selected.FlipY[i];Sprites[i].color=selected.Colors[i];}
        }
        public void Replay(float normalized)
        {
            if(count<1)return;
            int oldest=(next-count+samples.Length)%samples.Length,newest=(next-1+samples.Length)%samples.Length;
            double target=times[newest]+(times[oldest]-times[newest])*Mathf.Clamp01(normalized);
            int prior=oldest;
            for(int n=1;n<count;n++)
            {
                int current=(oldest+n)%samples.Length;
                if(times[current]>=target){float t=(float)((target-times[prior])/Math.Max(.00001,times[current]-times[prior]));Blend(samples[prior],samples[current],t);return;}
                prior=current;
            }
            Apply(samples[newest]);
        }
        public void Pause()
        {
            Initialize();if(IsPaused)return;Record(Time.unscaledTimeAsDouble);lease=new LoopActivityLease();
            if(agent && agent.enabled && agent.isOnNavMesh){agent.ResetPath();agent.velocity=Vector3.zero;}
            lease.Pause(agent);foreach(var writer in MovementWriters)lease.Pause(writer);
            foreach(var animator in GetComponentsInChildren<Animator>(true))lease.Pause(animator);
            if(body){wasKinematic=body.isKinematic;velocity=body.linearVelocity;angularVelocity=body.angularVelocity;body.isKinematic=true;}
        }
        public void Resume(bool resetSucceeded)
        {
            if(lease==null)return;
            var position=transform.position;lease.Dispose();lease=null;
            if(agent && agent.enabled){if(NavMesh.SamplePosition(position,out var hit,.5f,agent.areaMask))agent.Warp(hit.position);if(agent.isOnNavMesh){agent.ResetPath();agent.velocity=Vector3.zero;}}
            if(body){body.isKinematic=wasKinematic;if(!wasKinematic){body.linearVelocity=resetSucceeded?Vector3.zero:velocity;body.angularVelocity=resetSucceeded?Vector3.zero:angularVelocity;}}
            if(resetSucceeded)ClearHistory();
        }
        public void ClearHistory(){count=next=0;nextSample=0;}
        void OnDisable(){Resume(false);}
    }
}
