Shader "LucidLoop/TimeLoopFullscreen"
{
    Properties { _Progress("Progress",Range(0,1))=0 _Aspect("Aspect",Float)=1 _Opacity("Opacity",Range(0,1))=1 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _Progress,_Aspect,_Opacity,_EffectClock,_ReducedMotion;
            float _GrooveOpacity,_RotationStrength,_DistortionStrength,_BlurStrength,_SpiralStrength,_TonearmVisibility,_Glow,_DimStrength,_HasEmblem;
            float4 _EffectCenter;
            float ring(float r,float at,float width){return 1-smoothstep(width,width+0.0025,abs(r-at));}
            float segment(float2 p,float2 a,float2 b)
            {float2 d=b-a;return length(p-a-d*saturate(dot(p-a,d)/dot(d,d)));}
            half4 frag(Varyings i):SV_Target
            {
                float t=saturate(_Progress);
                float2 p=(i.texcoord-_EffectCenter.xy)*float2(_Aspect,1);
                float r=length(p), angle=atan2(p.y,p.x);
                // Hold the scene readable under delicate circular grooves first.
                float spin=smoothstep(.50,.73,t);
                float collapse=smoothstep(.82,.92,t);
                float vinyl=smoothstep(.72,.82,t);
                float scale=max(.035,1-collapse*.965*(1-_ReducedMotion));
                // Stage 3 accelerates backward rotation; the camera itself never moves.
                float rotation=spin*6.283185*_RotationStrength*(1-_ReducedMotion);
                float twist=(spin*1.8+collapse*12)*_DistortionStrength*(1-_ReducedMotion);
                float theta=angle-rotation-twist*r;
                float blur=spin*.30*_BlurStrength*(1-_ReducedMotion);

                float3 scene=0;
                float weight=0;
                // Angular shutter samples produce a continuous rotational blur.
                [unroll] for(int tap=0;tap<9;tap++)
                {
                    float offset=(tap-4)/4.0;
                    float sampleAngle=theta+offset*blur;
                    float2 uv=float2(cos(sampleAngle),sin(sampleAngle))*r/scale/float2(_Aspect,1)+_EffectCenter.xy;
                    // Mirror the edges so rotation does not reveal black wedges.
                    uv=1-abs(frac(uv*.5)*2-1);
                    float w=1-abs(offset)*.65;
                    scene+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb*w;
                    weight+=w;
                }
                scene/=weight;
                // Ease into a dim, dreamy backdrop as the rewind caption settles.
                // Keep the record, tonearm and text bright above this scene-only grade.
                float dream=smoothstep(0,.055,t);
                float3 dreamTint=lerp(float3(1,1,1),float3(.98,.94,1),dream*.4);
                float3 col=scene*dreamTint*(1-dream*_DimStrength)*(1-vinyl)*(1-spin*.18);
                float appear=smoothstep(.06,.16,t)*(1-smoothstep(.89,.94,t));
                float recordR=r/max(.035,1-collapse*.965*(1-_ReducedMotion));
                float3 pink=float3(1,.09,.46);
                // The record is larger than the viewport: no shrinking disc edge.
                float surfaceFade=vinyl*(1-smoothstep(.89,.98,t));
                float grooves=pow(.5+.5*cos(r*1100+sin(r*93)*.35),24);
                // Four restrained rim reflections, aligned to the viewport corners.
                float cornerAngle=atan2(abs(p.y),abs(p.x));
                float cornerDirection=atan2(1.0,_Aspect);
                float cornerLobe=exp(-pow((cornerAngle-cornerDirection)/.19,2));
                float rimWeight=smoothstep(.28,.78,r);
                float sheen=cornerLobe*rimWeight;
                col+=float3(.007,.006,.008)*surfaceFade;
                col+=float3(.032,.009,.019)*sheen*surfaceFade;
                col+=float3(.030,.010,.019)*grooves*sheen*surfaceFade;
                // Sparse, irregularly spaced grooves leave a large open label area.
                // Static highlights vary each line without rotating the frozen scene.
                float openingGrooves=0;
                [unroll] for(int g=0;g<13;g++)
                {
                    float radius=.275+g*.0185+sin(g*2.31)*.004;
                    float waviness=sin(angle*3+g*1.71)*.0009;
                    float width=.00045+.00055*(.5+.5*sin(g*2.7));
                    float grooveStroke=1-smoothstep(width,width+.001,abs(recordR-radius-waviness));
                    float glint=.20+.80*pow(.5+.5*sin(angle*2+g*.79),3);
                    float strength=.22+.28*(.5+.5*sin(g*1.93));
                    openingGrooves+=grooveStroke*glint*strength;
                    // A few close companion arcs add the layered, hand-drawn feel.
                    if(g==2 || g==7 || g==11)
                        openingGrooves+=(1-smoothstep(.0004,.0013,abs(recordR-radius-.004)))*glint*.13;
                }
                col+=pink*appear*(1-vinyl)*openingGrooves*_GrooveOpacity;
                float centerDot=1-smoothstep(.012,.014,recordR);
                col+=pink*appear*(1-vinyl)*centerDot*.86;
                // Wind the same spiral into a ring and move it to the loop icon.
                float morph=smoothstep(.82,.96,t)*_SpiralStrength;
                float2 iconOffset=(float2(.5,.595)-_EffectCenter.xy)*float2(_Aspect,1);
                float2 q=p-iconOffset*morph;
                float qr=length(q),qa=atan2(q.y,q.x);
                float spinAngle=rotation*1.3+(smoothstep(.70,.94,t)*8+_EffectClock*.3*spin)*(1-_ReducedMotion);
                float wrapped=frac((qa+spinAngle)/6.283185)*6.283185;
                float spiralInk=0;
                [unroll] for(int winding=0;winding<5;winding++)
                {
                    float sourceRadius=(wrapped+6.283185*winding)/58;
                    float targetRadius=lerp(sourceRadius,.072,morph);
                    float strokeWidth=lerp(.0013,.006,smoothstep(.90,1,morph));
                    float stroke=1-smoothstep(strokeWidth,strokeWidth+.0015,abs(qr-targetRadius));
                    float tail=exp(-abs(qr-targetRadius-.004*(1-morph))*340)*.13*(1-morph);
                    tail+=exp(-abs(qr-targetRadius)*110)*.08*(1-morph);
                    float brightness=lerp(.30+.40*pow(.5+.5*sin(qa+sourceRadius*9-spinAngle),3),1,morph);
                    float outerFade=1-smoothstep(.30,.54,sourceRadius);
                    float windingFade=winding==0?1:1-smoothstep(.50,.95,morph);
                    spiralInk=max(spiralInk,(stroke+tail)*brightness*lerp(outerFade,1,morph)*windingFade);
                }
                float gap=saturate(step(-.7854,qa)+step(qa,-2.3562));
                spiralInk*=lerp(1,gap,smoothstep(.90,.96,t));
                col+=pink*spiralInk*vinyl*(1-smoothstep(.95,.99,t))*_Glow;
                col+=pink*vinyl*(1-morph)*exp(-qr*qr*26000)*.6;
                // Tonearm stays outside the spinning grooves, then lifts away.
                float armFade=smoothstep(.50,.65,t)*(1-smoothstep(.80,.89,t))*_TonearmVisibility;
                float2 pivot=float2(.52,.57),elbow=float2(.40,.25),tip=float2(.28,.13);
                float arm=min(segment(p,pivot,elbow),segment(p,elbow,tip));
                // Narrow metal body, burgundy flank, and a fine hot-pink specular edge.
                float body=1-smoothstep(.0035,.0055,arm);
                col=lerp(col,float3(.25,.025,.085),body*armFade);
                float highlightDistance=min(segment(p+float2(.0012,0),pivot,elbow),segment(p+float2(.0012,0),elbow,tip));
                col+=float3(1,.38,.58)*armFade*.65*(1-smoothstep(.0007,.0018,highlightDistance));
                col+=pink*armFade*.055*exp(-arm*140);
                float2 head=p-tip;
                float2 tilted=float2(head.x*.707+head.y*.707,-head.x*.707+head.y*.707);
                float cartridge=(1-smoothstep(.028,.030,abs(tilted.x)))*(1-smoothstep(.011,.013,abs(tilted.y)));
                float bevel=smoothstep(.006,.012,abs(tilted.y));
                float3 cartridgeMetal=lerp(float3(.30,.035,.10),float3(.72,.20,.34),bevel);
                col=lerp(col,cartridgeMetal,cartridge*armFade);
                float screw=1-smoothstep(.0015,.0028,length(tilted-float2(.014,0)));
                col=lerp(col,float3(.08,.012,.028),screw*cartridge*armFade);
                float needle=segment(p,tip+float2(-.011,-.019),tip+float2(-.003,-.035));
                col+=float3(.9,.25,.42)*armFade*(1-smoothstep(.0006,.0015,needle));
                // The last groove resolves into the loop symbol, above the title.
                q=p-iconOffset;qr=length(q);
                qa=atan2(q.y,q.x);
                gap=saturate(step(-.7854,qa)+step(qa,-2.3562));
                float cap=min(length(q-float2(-.05091,-.05091)),length(q-float2(.05091,-.05091)));
                float beacon=max(ring(qr,.072,.006)*gap,1-smoothstep(.006,.0085,cap));
                float2 a=float2(-.024,-.062),b=float2(0,-.086),c=float2(.024,-.062);
                float2 ab=b-a,bc=c-b;
                float d1=length(q-a-ab*saturate(dot(q-a,ab)/dot(ab,ab)));
                float d2=length(q-b-bc*saturate(dot(q-b,bc)/dot(bc,bc)));
                beacon=max(beacon,1-smoothstep(.006,.009,min(d1,d2)));
                float arcDistance=gap>0?abs(qr-.072):cap;
                float glow=(exp(-arcDistance*110)*.22+exp(-min(d1,d2)*110)*.12)*(1+.06*sin(_EffectClock*5));
                col+=pink*(beacon+glow)*smoothstep(.95,.99,t)*_Glow*(1-_HasEmblem);
                float3 original=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,i.texcoord).rgb;
                return half4(lerp(original,col,_Opacity),1);
            }
            ENDHLSL
        }
    }
}
