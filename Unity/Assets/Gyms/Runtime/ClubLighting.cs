using UnityEngine;
namespace LucidLoop.Gyms
{
    public class ClubLighting : MonoBehaviour
    {
        public Light[] Lights;
        public Transform[] Dancers;
        float[] baseIntensity;
        void Start(){baseIntensity=new float[Lights.Length];for(int i=0;i<Lights.Length;i++)baseIntensity[i]=Lights[i]?Lights[i].intensity:0;}
        void Update()
        {
            float time = Time.time;
            for (int i=0;i<Lights.Length;i++)
            {
                if (!Lights[i]) continue;
                Lights[i].intensity = baseIntensity[i]*(1+Mathf.Sin(time * .7f + i)*.2f);
                if (Lights[i].type == LightType.Spot)
                    Lights[i].transform.localRotation = Quaternion.Euler(70+Mathf.Sin(time*.35f+i)*12, i*73+Mathf.Sin(time*.25f+i)*25,0);
            }
            for (int i=0;i<Dancers.Length;i++)
                if (Dancers[i]) Dancers[i].localRotation = Quaternion.Euler(0, i*47, Mathf.Sin(time*2.3f+i)*5);
        }
    }
}
