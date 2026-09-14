using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Rendering.Universal;

namespace LucidLoop.Gyms.Mvp
{
    // The fixed-angle painting and live geometry share one orthographic projection.
    // Camera translation changes framing, never the alignment of feet and scenery.
    public sealed class ExpandedClub : MonoBehaviour
    {
        const float HalfHeight=18,Aspect=16f/9;
        static readonly Quaternion Angle=Quaternion.Euler(51,-23,0);
        FirstLoop loop;Camera view;NavMeshDataInstance navigation;Mesh floorMesh;Material paint;
        Vector3 cameraFocus;float size=11.4f;
        public static Vector3 Point(float x,float y)
        {
            var local=new Vector3((x-.5f)*HalfHeight*2*Aspect,(.5f-y)*HalfHeight*2,0);
            var ray=Angle*local;var forward=Angle*Vector3.forward;
            return ray-forward*(ray.y/forward.y);
        }
        public static Vector2 UV(Vector3 p)
        {var local=Quaternion.Inverse(Angle)*p;return new Vector2(.5f+local.x/(HalfHeight*2*Aspect),.5f-local.y/(HalfHeight*2));}
        public static Vector3 Opening=>Point(.575f,.65f);
        public static Vector3 LeftPreparation=>Point(.37f,.68f);
        public static Vector3 VipTheo=>Point(.818f,.595f);
        public static Vector3 VipPlayer=>Point(.789f,.606f);
        public static bool Recognition(Vector3 p){var uv=UV(p);return uv.x>.535f&&uv.x<.635f&&uv.y>.59f&&uv.y<.70f;}
        public static Vector3 ClampOpening(Vector3 p)
        {var uv=UV(p);return Point(Mathf.Clamp(uv.x,.46f,.63f),Mathf.Clamp(uv.y,.65f,.92f));}
        public void Install(FirstLoop game)
        {
            loop=game;view=loop.Rig.Camera;loop.Rig.enabled=false;
            foreach(var a in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))a.enabled=false;
            foreach(var o in FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None))o.enabled=false;
            foreach(var surface in FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None)){surface.RemoveData();surface.enabled=false;}
            foreach(var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))if(!c.GetComponentInParent<CharacterActor>())c.enabled=false;
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if(!r.GetComponentInParent<CharacterActor>()&&!r.GetComponentInParent<CastVisual>()&&!r.transform.IsChildOf(loop.Partner))r.enabled=false;
            BuildFloor();
            Furniture();
            Place(loop.Player,Point(.49f,.89f));Place(loop.Maya.GetComponent<NavMeshAgent>(),Point(.465f,.89f));
            Place(loop.Theo.GetComponent<NavMeshAgent>(),Point(.804f,.592f));Place(loop.Luca.GetComponent<NavMeshAgent>(),Point(.285f,.535f));
            loop.Ren.transform.position=Point(.487f,.265f);loop.Partner.position=Point(.824f,.601f);
            loop.Theo.transform.LookAt(loop.Partner.position);loop.Partner.LookAt(loop.Theo.transform.position);loop.Ren.transform.LookAt(Point(.48f,.5f));loop.Luca.transform.LookAt(Point(.48f,.5f));
            var club=FindFirstObjectByType<ClubLighting>();
            if(club){var spots=new[]{new Vector2(.412f,.48f),new Vector2(.49f,.49f),new Vector2(.535f,.535f),new Vector2(.42f,.565f),new Vector2(.478f,.592f),new Vector2(.55f,.455f),new Vector2(.44f,.43f),new Vector2(.51f,.62f),new Vector2(.37f,.54f)};int i=0;
                foreach(var d in club.Dancers){if(i>=spots.Length){d.gameObject.SetActive(false);continue;}d.position=Point(spots[i].x,spots[i].y);d.localScale=Vector3.one*(.92f+(i%3)*.055f);i++;}}
            var fill=new GameObject("Soft character fill").AddComponent<Light>();fill.transform.SetParent(transform,false);fill.type=LightType.Directional;fill.color=new Color(.72f,.8f,1);fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(55,-35,0);fill.shadows=LightShadows.None;
            view.orthographic=true;view.orthographicSize=size;view.aspect=Aspect;view.farClipPlane=180;
            view.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Expanded painted club — world space";Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform,false);quad.transform.rotation=Angle;quad.transform.position=Angle*Vector3.forward*70;
            quad.transform.localScale=new Vector3(HalfHeight*2*Aspect,HalfHeight*2,1);
            paint=new Material(Shader.Find("BTD/ExpandedBackdrop"));paint.mainTexture=Resources.Load<Texture2D>("MvpArt/ClubExpanded");quad.GetComponent<Renderer>().sharedMaterial=paint;
            cameraFocus=loop.Player.transform.position;ApplyCamera(true);
        }
        void Furniture()
        {
            var depth=new Material(Shader.Find("BTD/PaintedOcclusion"));
            foreach(var uv in new[]{new Vector2(.326f,.537f),new Vector2(.365f,.422f),new Vector2(.64f,.609f),new Vector2(.646f,.437f)})
            {
                var table=GameObject.CreatePrimitive(PrimitiveType.Cylinder);table.name="Cocktail table collision / occlusion";table.transform.SetParent(transform,false);table.transform.position=Point(uv.x,uv.y)+Vector3.up*.85f;table.transform.localScale=new Vector3(1.25f,1.7f/2,1.25f);table.GetComponent<Renderer>().sharedMaterial=depth;
                var obstacle=table.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Capsule;obstacle.radius=.5f;obstacle.height=2;obstacle.carving=true;
            }
            var a=Point(.685f,.48f);var b=Point(.742f,.675f);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="VIP partition — use the front stairs";wall.transform.SetParent(transform,false);wall.transform.position=(a+b)/2+Vector3.up*.6f;wall.transform.rotation=Quaternion.LookRotation(b-a);wall.transform.localScale=new Vector3(.32f,1.2f,Vector3.Distance(a,b));wall.GetComponent<Renderer>().sharedMaterial=depth;
            var screen=wall.AddComponent<NavMeshObstacle>();screen.shape=NavMeshObstacleShape.Box;screen.size=Vector3.one;screen.carving=true;
            var planter=new GameObject("Entrance planter obstacle");planter.transform.SetParent(transform,false);planter.transform.position=Point(.588f,.748f);var plant=planter.AddComponent<NavMeshObstacle>();plant.shape=NavMeshObstacleShape.Capsule;plant.radius=.8f;plant.height=2;plant.carving=true;
        }
        static void Place(NavMeshAgent agent,Vector3 point)
        {
            agent.transform.position=point;agent.enabled=true;
            if(!NavMesh.SamplePosition(point,out var hit,1.5f,NavMesh.AllAreas)||!agent.Warp(hit.position))throw new InvalidOperationException("Expanded club spawn outside floor: "+agent.name);
        }
        void BuildFloor()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Polygon(params float[] xy)
            {
                int start=vertices.Count,count=xy.Length/2;
                for(int i=0;i<count;i++)vertices.Add(Point(xy[i*2],xy[i*2+1]));
                for(int i=1;i<count-1;i++){
                    var a=vertices[start];var b=vertices[start+i];var c=vertices[start+i+1];
                    if(Vector3.Cross(b-a,c-a).y>0)triangles.AddRange(new[]{start,start+i,start+i+1});else triangles.AddRange(new[]{start,start+i+1,start+i});
                }
            }
            // Overlapping convex floor patches describe actual aisles; sofas,
            // counters, plants and walls remain outside the navigation surface.
            Polygon(.405f,.94f,.602f,.94f,.598f,.76f,.425f,.76f); // vestibule
            Polygon(.39f,.795f,.62f,.795f,.68f,.66f,.65f,.38f,.39f,.34f,.25f,.62f); // dancefloor
            Polygon(.13f,.58f,.28f,.625f,.39f,.36f,.32f,.32f); // bar aisle
            Polygon(.60f,.37f,.72f,.38f,.79f,.455f,.685f,.68f,.635f,.67f); // right aisle
            Polygon(.60f,.68f,.71f,.75f,.805f,.69f,.79f,.615f,.675f,.625f); // VIP approach
            Polygon(.758f,.708f,.804f,.698f,.817f,.612f,.777f,.603f); // stairs into VIP
            Polygon(.777f,.626f,.875f,.615f,.881f,.544f,.804f,.527f,.768f,.565f); // private seating gap
            Polygon(.36f,.40f,.42f,.36f,.427f,.26f,.39f,.267f); // stage left stairs
            Polygon(.408f,.31f,.61f,.344f,.618f,.25f,.428f,.215f); // DJ platform
            Polygon(.707f,.415f,.82f,.433f,.91f,.30f,.83f,.245f); // rear passage
            floorMesh=new Mesh{name="Expanded club walkable aisles"};floorMesh.SetVertices(vertices);floorMesh.SetTriangles(triangles,0);floorMesh.RecalculateNormals();floorMesh.RecalculateBounds();
            var floor=new GameObject("Walkable club zones");floor.transform.SetParent(transform,false);floor.AddComponent<MeshCollider>().sharedMesh=floorMesh;
            var settings=NavMesh.GetSettingsByID(loop.Player.agentTypeID);settings.overrideVoxelSize=true;settings.voxelSize=.08f;settings.agentRadius=.32f;
            var source=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=floorMesh,transform=Matrix4x4.identity,area=0};
            var data=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{source},new Bounds(Vector3.zero,new Vector3(120,20,120)),Vector3.zero,Quaternion.identity);
            if(!data)throw new InvalidOperationException("Expanded club navigation failed");navigation=NavMesh.AddNavMeshData(data);
        }
        void LateUpdate(){ApplyCamera(false);if(paint)paint.SetFloat("_Mood",loop.State.Intimate?1:0);}
        void ApplyCamera(bool instant)
        {
            if(!view||!loop)return;
            bool encounter=loop.Busy&&loop.State.Recognized;
            var target=encounter?Point(.58f,.61f):loop.Player.transform.position;
            // Clamp in image space so the camera never exposes beyond the painting.
            float desired=encounter?12.1f:11.4f;
            var uv=UV(target);float margin=desired/(HalfHeight*2);
            uv.x=Mathf.Clamp(uv.x,margin,1-margin);uv.y=Mathf.Clamp(uv.y,margin,1-margin);
            target=Point(uv.x,uv.y);
            float t=instant?1:1-Mathf.Exp(-Time.unscaledDeltaTime*4);
            cameraFocus=Vector3.Lerp(cameraFocus,target,t);size=Mathf.Lerp(size,desired,t);
            view.transform.SetPositionAndRotation(cameraFocus-Angle*Vector3.forward*45,Angle);view.orthographicSize=size;
            float aspect=(float)Screen.width/Screen.height;view.rect=aspect>Aspect?new Rect((1-Aspect/aspect)/2,0,Aspect/aspect,1):new Rect(0,(1-aspect/Aspect)/2,1,aspect/Aspect);
        }
        void OnDestroy(){navigation.Remove();if(floorMesh)Destroy(floorMesh);}
    }
}
