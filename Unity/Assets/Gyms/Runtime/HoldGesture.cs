namespace LucidLoop.Gyms
{
    public enum GestureResult { None, Tap, Hold }
    public sealed class HoldGesture
    {
        bool active, isCharacter, fired;
        float originX,originY,started;
        public float Progress(float now) => active && isCharacter && !fired ? System.Math.Clamp((now-started)/.45f,0,1) : 0;
        public void Begin(float x, float y, float now, bool character)
        {originX=x;originY=y;started=now;active=true;isCharacter=character;fired=false;}
        public void Move(float x, float y, float tolerance)
        {if((x-originX)*(x-originX)+(y-originY)*(y-originY)>tolerance*tolerance)Cancel();}
        public void Cancel() {active=false;}
        public GestureResult Tick(float now)
        {
            if(!active||!isCharacter||fired||now-started<.45f)return GestureResult.None;
            fired=true;return GestureResult.Hold;
        }
        public GestureResult Release()
        {var result=active&&!fired?GestureResult.Tap:GestureResult.None;active=false;return result;}
    }
}
