"""Original procedural placeholder score, no samples or external assets. Run with Python 3."""
import math, random, wave, array
from pathlib import Path
RATE=22050
OUT=Path(__file__).resolve().parents[2]/'Unity/Assets/Gyms/Resources/MvpAudio'
OUT.mkdir(parents=True,exist_ok=True)
def make(name,bpm,aggressive):
    beat=60/bpm; length=32*beat; n=round(length*RATE); data=[0.0]*n; rng=random.Random(17)
    def event(start,duration,fn,gain=1):
        for j in range(int(duration*RATE)):
            t=j/RATE; data[(round(start*RATE)+j)%n]+=gain*fn(t,duration)
    for b in range(32):
        if aggressive or b%2==0:
            event(b*beat,.28,lambda t,d: math.sin(2*math.pi*(47*t+9*(1-math.exp(-30*t))))*math.exp(-19*t),.55 if aggressive else .3)
        for h in range(2 if aggressive else 1):
            event((b+h/2)*beat,.055,lambda t,d: rng.uniform(-1,1)*math.exp(-75*t),.055 if aggressive else .025)
        if b%4 in (1,3):event(b*beat,.14,lambda t,d: rng.uniform(-1,1)*math.exp(-28*t),.11 if aggressive else .035)
    roots=[110,87.307,130.813,97.999]
    for bar in range(8):
        root=roots[(bar//2)%4]
        if aggressive:
            for step in range(8):
                f=root/2*(1 if step%4!=3 else 1.5)
                event((bar*4+step/2)*beat,beat*.42,lambda t,d,f=f: (math.sin(2*math.pi*f*t)+.25*math.sin(2*math.pi*3*f*t))*min(1,t/.008)*math.exp(-8*t),.2)
        else:
            for ratio in [1,2**(3/12),1.5,2**(10/12)]:
                f=root*ratio*2
                event(bar*4*beat,4*beat,lambda t,d,f=f: math.sin(math.pi*t/d)**2*(math.sin(2*math.pi*f*t)+.12*math.sin(2*math.pi*2*f*t)),.065)
    peak=max(abs(x) for x in data); scale=.65/max(peak,.01)
    pcm=array.array('h',(int(max(-1,min(1,x*scale))*32767) for x in data))
    with wave.open(str(OUT/(name+'.wav')),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
    print(name,round(length,2),'seconds', 'peak',.65)
make('Aggressive',128,True)
make('Intimate',92,False)
