using UnityEngine;
namespace LucidLoop.Gyms
{
    public sealed class ClubBeamMotion:MonoBehaviour
    {
        public Light Fixture;
        Material owned,source;
        void OnEnable()
        {
            var renderer=GetComponent<Renderer>();source=renderer.sharedMaterial;if(source){owned=new Material(source);renderer.sharedMaterial=owned;}
        }
        void LateUpdate()
        {
            if(!Fixture)return;
            transform.SetPositionAndRotation(Fixture.transform.position,Fixture.transform.rotation);
            if(owned)owned.SetColor("_BaseColor",Fixture.color);
        }
        void OnDisable(){if(source)GetComponent<Renderer>().sharedMaterial=source;if(owned)Destroy(owned);owned=null;}
    }
}
