using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    // Scratch-only import helper. No execute-menu or Unity launch entry point.
    public static class RenDesignerPatchImport
    {
        const string Root = "Assets/CharacterArt/Generated/RenDesignerRefinementReview/Patch";
        const string FrameAudit = "Assets/CharacterArt/Generated/RenDesignerEyeReview/ImportAudit.json";
        const string FbxName = "Ren_H_EyeInterfacePatch.fbx";
        const string FbxHash = "45edb235b532efe14941c7782e131a526e1971d1db22e24a5a1c97acd8186106";
        const string EyeFbxHash = "ee7fbae3066f0576328f32fd635583da0d419d8dcfd6033ed102d2695f6338fc";
        const string PatchName = "Ren_H_EyeInterface_L_Patch";
        const float PositionTolerance = 2e-6f, UvTolerance = 2e-5f, ColorTolerance = .5f / 255f + .00002f;
        const int CornerCount = 186;
        const string CornersSha256 = "b362439c35c6e807d50f0c722fed58c7a48ed57810fac428de28c95595491eef";
        const string CornersBase64 = "AiQ5Pv2/fL6ASxw/AFByPwBwPD/SwEA/zoQIP2rx4z4AAIA/AiA5Pv23e76AURw/AGByPwBwPD/SwEA/zoQIP2rx4z4AAIA/Apw4Pv0jfb4A3xw/AEByPwAwPD/SwEA/zoQIP2rx4z4AAIA/AiA5Pv23e76AURw/AGByPwBwPD/SwEA/zoQIP2rx4z4AAIA/ArA4Pv2Pe76Atxw/AHByPwBAPD/SwEA/zoQIP2rx4z4AAIA/AmA5Pv2beL4ABxw/ALByPwCQPD/SwEA/BfEGP8Aa4T4AAIA/Asg5Pv3rfb6Akhs/ACByPwCwPD/Q0j4/BfEGP8Aa4T4AAIA/Avw4Pv0Pfr4Ajhw/ADByPwBQPD/SwEA/zoQIP2rx4z4AAIA/AiQ5Pv2/fL6ASxw/AFByPwBwPD/SwEA/zoQIP2rx4z4AAIA/AiQ5Pv2/fL6ASxw/AFByPwBwPD/SwEA/zoQIP2rx4z4AAIA/AmA5Pv2beL4ABxw/ALByPwCQPD/SwEA/BfEGP8Aa4T4AAIA/AiA5Pv23e76AURw/AGByPwBwPD/SwEA/zoQIP2rx4z4AAIA/Asg5Pv3rfb6Akhs/ACByPwCwPD/Q0j4/BfEGP8Aa4T4AAIA/AiQ5Pv2/fL6ASxw/AFByPwBwPD/SwEA/zoQIP2rx4z4AAIA/AmA5Pv2beL4ABxw/ALByPwCQPD/SwEA/BfEGP8Aa4T4AAIA/Asg5Pv3rfb6Akhs/ACByPwCwPD/Q0j4/BfEGP8Aa4T4AAIA/AtQ5Pv1Td76AmBs/ANByPwDAPD/SwEA/BfEGP8Aa4T4AAIA/AmA5Pv2beL4ABxw/ALByPwCQPD/SwEA/BfEGP8Aa4T4AAIA/Aqg6Pv4BgL4A5Bo/AOBxPwDwPD+z5zw/VBsKP2rx4z4AAIA/Arg5Pv0/f74A4xs/AAByPwCQPD/k3D0/zoQIP2uF4j4AAIA/Asg5Pv3rfb6Akhs/ACByPwCwPD/Q0j4/BfEGP8Aa4T4AAIA/Asg5Pv3rfb6Akhs/ACByPwCwPD/Q0j4/BfEGP8Aa4T4AAIA/Amw6Pv3Hdr6AAxs/ANByPwDwPD/SwEA/BfEGP8Aa4T4AAIA/AtQ5Pv1Td76AmBs/ANByPwDAPD/SwEA/BfEGP8Aa4T4AAIA/Aqg6Pv4BgL4A5Bo/AOBxPwDwPD+z5zw/VBsKP2rx4z4AAIA/Asg5Pv3rfb6Akhs/ACByPwCwPD/Q0j4/BfEGP8Aa4T4AAIA/Amw6Pv3Hdr6AAxs/ANByPwDwPD/SwEA/BfEGP8Aa4T4AAIA/Aqg6Pv4BgL4A5Bo/AOBxPwDwPD+z5zw/VBsKP2rx4z4AAIA/Amw6Pv3Hdr6AAxs/ANByPwDwPD/SwEA/BfEGP8Aa4T4AAIA/Ahg7Pv3bdb6ATBo/AOByPwBAPT/Q0j4/BfEGP8Aa4T4AAIA/Avw7Pv6JgL4AjBk/ALBxPwBwPT/Q0j4/mrQLP2bN5j4AAIA/Aqg6Pv4BgL4A5Bo/AOBxPwDwPD+z5zw/VBsKP2rx4z4AAIA/Ahg7Pv3bdb6ATBo/AOByPwBAPT/Q0j4/BfEGP8Aa4T4AAIA/Avw7Pv6JgL4AjBk/ALBxPwBwPT/Q0j4/mrQLP2bN5j4AAIA/Ahg7Pv3bdb6ATBo/AOByPwBAPT/Q0j4/BfEGP8Aa4T4AAIA/AsA7Pv0zdb6ApBk/ADB0PwCgYz/SwEA/BfEGP8Aa4T4AAIA/Avw7Pv6JgL4AjBk/ALBxPwBwPT/Q0j4/mrQLP2bN5j4AAIA/AsA7Pv0zdb6ApBk/ADB0PwCgYz/SwEA/BfEGP8Aa4T4AAIA/AnQ8Pv2/dL4A9Rg/APBzPwCAYz/SwEA/zoQIP2rx4z4AAIA/Amw9Pv6dgL6Aoxc/AJBxPwAQPj+z5zw/VBsKP2rx4z4AAIA/AnQ8Pv2/dL4A9Rg/APBzPwCAYz/SwEA/zoQIP2rx4z4AAIA/Avw7Pv6JgL4AjBk/ALBxPwBwPT/Q0j4/mrQLP2bN5j4AAIA/AnQ8Pv2/dL4A9Rg/APBzPwCAYz/SwEA/zoQIP2rx4z4AAIA/AsQ7Pv3Pcr6Aghk/AAB0PwDQYz90yT8/BfEGP8Aa4T4AAIA/AtQ8Pv0Hcr4AhBg/AKBzPwCgYz/SwEA/zoQIP2rx4z4AAIA/Amw9Pv6dgL6Aoxc/AJBxPwAQPj+z5zw/VBsKP2rx4z4AAIA/AtQ8Pv0Hcr4AhBg/AKBzPwCgYz/SwEA/zoQIP2rx4z4AAIA/AnQ8Pv2/dL4A9Rg/APBzPwCAYz/SwEA/zoQIP2rx4z4AAIA/Amw9Pv6dgL6Aoxc/AJBxPwAQPj+z5zw/VBsKP2rx4z4AAIA/Alg+Pv2rb74AFRc/ABBzPwBwYz/Q0j4/BfEGP8Aa4T4AAIA/AtQ8Pv0Hcr4AhBg/AKBzPwCgYz/SwEA/zoQIP2rx4z4AAIA/Ajg+Pv4Jgb4AERc/AHBxPwBQPj+z5zw/VBsKP2rx4z4AAIA/Ajw9Pv4lgb4Acxg/AIBxPwDQPT868zs/n+cKP2rx4z4AAIA/Amw9Pv6dgL6Aoxc/AJBxPwAQPj+z5zw/VBsKP2rx4z4AAIA/Ajg+Pv4Jgb4AERc/AHBxPwBQPj+z5zw/VBsKP2rx4z4AAIA/Alg+Pv2rb74AFRc/ABBzPwBwYz/Q0j4/BfEGP8Aa4T4AAIA/Amw9Pv6dgL6Aoxc/AJBxPwAQPj+z5zw/VBsKP2rx4z4AAIA/Ajg+Pv4Jgb4AERc/AHBxPwBQPj+z5zw/VBsKP2rx4z4AAIA/Asw+Pv3vb76ArRY/AABzPwBQYz/k3D0/JygGP2ix3z4AAIA/Alg+Pv2rb74AFRc/ABBzPwBwYz/Q0j4/BfEGP8Aa4T4AAIA/AgA/Pv5pgb4AnhY/AGBxPwCAPj+z5zw/VBsKP2rx4z4AAIA/Ajg+Pv4Jgb4AERc/AHBxPwBQPj+z5zw/VBsKP2rx4z4AAIA/Asw+Pv3vb76ArRY/AABzPwBQYz/k3D0/JygGP2ix3z4AAIA/AgA/Pv5pgb4AnhY/AGBxPwCAPj+z5zw/VBsKP2rx4z4AAIA/Asw+Pv3vb76ArRY/AABzPwBQYz/k3D0/JygGP2ix3z4AAIA/Avg+Pv1Xbb4AYxY/AMByPwBwYz/k3D0/JygGP2ix3z4AAIA/Asg/Pv45gb4ARhU/AAAvPwAA4z0kGjk/VBsKP8Aa4T4AAIA/AgA/Pv5pgb4AnhY/AGBxPwCAPj+z5zw/VBsKP2rx4z4AAIA/Avg+Pv1Xbb4AYxY/AMByPwBwYz/k3D0/JygGP2ix3z4AAIA/Asg/Pv45gb4ARhU/AAAvPwAA4z0kGjk/VBsKP8Aa4T4AAIA/ApA/Pv1zb76A4xU/ALByPwAwYz+z5zw/+F8FP2RJ3j4AAIA/Avg+Pv1Xbb4AYxY/AMByPwBwYz/k3D0/JygGP2ix3z4AAIA/Asg/Pv45gb4ARhU/AAAvPwAA4z0kGjk/VBsKP8Aa4T4AAIA/AoxAPv2fcL4A+xQ/AJByPwDgYj80kzk/sagCP75n2T4AAIA/ApA/Pv1zb76A4xU/ALByPwAwYz+z5zw/+F8FP2RJ3j4AAIA/AoBAPv5pgb6AxhQ/AAAvPwCA4T2z5zw/n1ANP2bN5j4AAIA/AjRAPv6Zgb4AkxU/ADAvPwAA4z1fbTw/W+kMP+cV5j4AAIA/Asg/Pv45gb4ARhU/AAAvPwAA4z0kGjk/VBsKP8Aa4T4AAIA/AoBAPv5pgb6AxhQ/AAAvPwCA4T2z5zw/n1ANP2bN5j4AAIA/AoxAPv2fcL4A+xQ/AJByPwDgYj80kzk/sagCP75n2T4AAIA/Asg/Pv45gb4ARhU/AAAvPwAA4z0kGjk/VBsKP8Aa4T4AAIA/AixBPv5lgb4A7xM/AOAuPwAA3z16/zo/n1ANP2bN5j4AAIA/AvhAPv61gb4A/hQ/ACAvPwCA4T2z5zw/n1ANP2bN5j4AAIA/AoBAPv5pgb6AxhQ/AAAvPwCA4T2z5zw/n1ANP2bN5j4AAIA/AoBAPv5pgb6AxhQ/AAAvPwCA4T2z5zw/n1ANP2bN5j4AAIA/AgRBPv3HcL4AjRQ/AHByPwDAYj9CoTg/k+MBP6wF2D4AAIA/AoxAPv2fcL4A+xQ/AJByPwDgYj80kzk/sagCP75n2T4AAIA/AixBPv5lgb4A7xM/AOAuPwAA3z16/zo/n1ANP2bN5j4AAIA/AgRBPv3HcL4AjRQ/AHByPwDAYj9CoTg/k+MBP6wF2D4AAIA/AoBAPv5pgb6AxhQ/AAAvPwCA4T2z5zw/n1ANP2bN5j4AAIA/AixBPv5lgb4A7xM/AOAuPwAA3z16/zo/n1ANP2bN5j4AAIA/AoBBPv0rcL6ADhQ/AEByPwCwYj9fbTw/IvwEP+CV3T4AAIA/AgRBPv3HcL4AjRQ/AHByPwDAYj9CoTg/k+MBP6wF2D4AAIA/ApRBPv5Jgb4ANBM/ALAuPwAA3T16/zo/n1ANP2bN5j4AAIA/AtRBPv6tgb4AHhQ/AAAvPwAA3z16/zo/n1ANP2bN5j4AAIA/AixBPv5lgb4A7xM/AOAuPwAA3z16/zo/n1ANP2bN5j4AAIA/AixBPv5lgb4A7xM/AOAuPwAA3z16/zo/n1ANP2bN5j4AAIA/AhRCPv3rb76AdxM/ABByPwCQYj/k3D0/JygGP2ix3z4AAIA/AoBBPv0rcL6ADhQ/AEByPwCwYj9fbTw/IvwEP+CV3T4AAIA/ApRBPv5Jgb4ANBM/ALAuPwAA3T16/zo/n1ANP2bN5j4AAIA/AhRCPv3rb76AdxM/ABByPwCQYj/k3D0/JygGP2ix3z4AAIA/AixBPv5lgb4A7xM/AOAuPwAA3z16/zo/n1ANP2bN5j4AAIA/ApRBPv5Jgb4ANBM/ALAuPwAA3T16/zo/n1ANP2bN5j4AAIA/ApxCPv0zb74A2xI/ANBxPwCAYj90yT8/kroHP75e5T4AAIA/AhRCPv3rb76AdxM/ABByPwCQYj/k3D0/JygGP2ix3z4AAIA/AmxCPv5tgb4A1hI/ALAuPwCA2z16/zo/n1ANP2bN5j4AAIA/ApxCPv6pgb6AchM/AOAuPwAA3T16/zo/n1ANP2bN5j4AAIA/ApRBPv5Jgb4ANBM/ALAuPwAA3T16/zo/n1ANP2bN5j4AAIA/AmxCPv5tgb4A1hI/ALAuPwCA2z16/zo/n1ANP2bN5j4AAIA/ApxCPv0zb74A2xI/ANBxPwCAYj90yT8/kroHP75e5T4AAIA/ApRBPv5Jgb4ANBM/ALAuPwAA3T16/zo/n1ANP2bN5j4AAIA/AmxCPv5tgb4A1hI/ALAuPwCA2z16/zo/n1ANP2bN5j4AAIA/AohDPv0rb74A6RE/AJBxPwBAYj/DVz4/gIwGP+ll4D4AAIA/ApxCPv0zb74A2xI/ANBxPwCAYj90yT8/kroHP75e5T4AAIA/AuhDPv51gb6A9RE/ACAJPwDgTT96/zo/n1ANP2bN5j4AAIA/AmxCPv5tgb4A1hI/ALAuPwCA2z16/zo/n1ANP2bN5j4AAIA/AohDPv0rb74A6RE/AJBxPwBAYj/DVz4/gIwGP+ll4D4AAIA/AnRDPv6Jgb4A7xE/ABAJPwDgTT96/zo/n1ANP2bN5j4AAIA/AohDPv0rb74A6RE/AJBxPwBAYj/DVz4/gIwGP+ll4D4AAIA/AuhDPv51gb6A9RE/ACAJPwDgTT96/zo/n1ANP2bN5j4AAIA/AvhDPv7lgb6A7BE/ABAJPwDQTT96/zo/n1ANP2bN5j4AAIA/AnRDPv6Jgb4A7xE/ABAJPwDgTT96/zo/n1ANP2bN5j4AAIA/AohDPv0rb74A6RE/AJBxPwBAYj/DVz4/gIwGP+ll4D4AAIA/AlBDPv6Bgr6A2xE/APAIPwDATT96/zo/n1ANP1yV7D4AAIA/AvhDPv7lgb6A7BE/ABAJPwDQTT96/zo/n1ANP2bN5j4AAIA/AohDPv0rb74A6RE/AJBxPwBAYj/DVz4/gIwGP+ll4D4AAIA/AlBDPv6Bgr6A2xE/APAIPwDATT96/zo/n1ANP1yV7D4AAIA/AgxEPv2vb76AaxE/AHBxPwAgYj/fhTo/mh8IP+cV5j4AAIA/AohDPv0rb74A6RE/AJBxPwBAYj/DVz4/gIwGP+ll4D4AAIA/AmRDPv7Rgr6AzRE/AOAIPwCwTT96/zo/n1ANP1yV7D4AAIA/AgxEPv2vb76AaxE/AHBxPwAgYj/fhTo/mh8IP+cV5j4AAIA/AlBDPv6Bgr6A2xE/APAIPwDATT96/zo/n1ANP1yV7D4AAIA/ArhDPv7tgr4AwBE/AOAIPwCgTT96/zo/n1ANP1yV7D4AAIA/AmRDPv7Rgr6AzRE/AOAIPwCwTT96/zo/n1ANP1yV7D4AAIA/AgxEPv2vb76AaxE/AHBxPwAgYj/fhTo/mh8IP+cV5j4AAIA/AmxDPv5Bg74ApBE/ANAIPwCgTT96/zo/n1ANP1yV7D4AAIA/AgxEPv2vb76AaxE/AHBxPwAgYj/fhTo/mh8IP+cV5j4AAIA/ArhDPv7tgr4AwBE/AOAIPwCgTT96/zo/n1ANP1yV7D4AAIA/AlhDPv5hg74AeRE/AMAIPwCQTT868zs/qh8OP7EK7j4AAIA/AgxEPv2vb76AaxE/AHBxPwAgYj/fhTo/mh8IP+cV5j4AAIA/AmxDPv5Bg74ApBE/ANAIPwCgTT96/zo/n1ANP1yV7D4AAIA/AgREPv4dg76AWRE/ANAIPwBwTT868zs/n+cKP14h6z4AAIA/AgxEPv2vb76AaxE/AHBxPwAgYj/fhTo/mh8IP+cV5j4AAIA/AlhDPv5hg74AeRE/AMAIPwCQTT868zs/qh8OP7EK7j4AAIA/AgREPv4dg76AWRE/ANAIPwBwTT868zs/n+cKP14h6z4AAIA/AoREPv0Db76A8RA/AEBxPwAQYj9DeTs/LuoIPzqF5z4AAIA/AgxEPv2vb76AaxE/AHBxPwAgYj/fhTo/mh8IP+cV5j4AAIA/AvxDPv4Fg76AOxE/ANAIPwBwTT868zs/n+cKP14h6z4AAIA/AoREPv0Db76A8RA/AEBxPwAQYj9DeTs/LuoIPzqF5z4AAIA/AgREPv4dg76AWRE/ANAIPwBwTT868zs/n+cKP14h6z4AAIA/AuhFPv5Jgr4ALhE/AOAIPwBATT9QVS8/X1sAP6wF2D4AAIA/AvxDPv4Fg76AOxE/ANAIPwBwTT868zs/n+cKP14h6z4AAIA/AoREPv0Db76A8RA/AEBxPwAQYj9DeTs/LuoIPzqF5z4AAIA/AgxDPv7Bgr6AyRA/ALAIPwBATT/h/Sg//Cf2Pvx7zj4AAIA/AlRFPv5Bgr6AAhE/ANAIPwAwTT9AAR4/avHjPt4Svj4AAIA/AuhFPv5Jgr4ALhE/AOAIPwBATT9QVS8/X1sAP6wF2D4AAIA/AlRFPv5Bgr6AAhE/ANAIPwAwTT9AAR4/avHjPt4Svj4AAIA/AkRGPv7pgb4AEhE/AOAIPwAQTT813h4/vl7lPoFbvz4AAIA/AuhFPv5Jgr4ALhE/AOAIPwBATT9QVS8/X1sAP6wF2D4AAIA/AkRGPv7pgb4AEhE/AOAIPwAQTT813h4/vl7lPoFbvz4AAIA/AiBHPv41gb4AHxE/AAAJPwDwTD868zs/qh8OP7Wu6T4AAIA/AuhFPv5Jgr4ALhE/AOAIPwBATT9QVS8/X1sAP6wF2D4AAIA/AiBHPv41gb4AHxE/AAAJPwDwTD868zs/qh8OP7Wu6T4AAIA/AuhFPv5Jgr4ALhE/AOAIPwBATT9QVS8/X1sAP6wF2D4AAIA/AoREPv0Db76A8RA/AEBxPwAQYj9DeTs/LuoIPzqF5z4AAIA/AkRGPv7pgb4AEhE/AOAIPwAQTT813h4/vl7lPoFbvz4AAIA/AmBGPv6Zgb4AEBE/APAIPwAATT8eWDU/BfEGP2uF4j4AAIA/AiBHPv41gb4AHxE/AAAJPwDwTD868zs/qh8OP7Wu6T4AAIA/AvhFPv5Vgb4ABxE/ACB5PwAAsjx6/zo/n1ANP2bN5j4AAIA/AiBHPv41gb4AHxE/AAAJPwDwTD868zs/qh8OP7Wu6T4AAIA/AoREPv0Db76A8RA/AEBxPwAQYj9DeTs/LuoIPzqF5z4AAIA/AlRFPv5Bgr6AAhE/ANAIPwAwTT9AAR4/avHjPt4Svj4AAIA/AiBFPv4Rgr6A5hA/ANAIPwAQTT/IUCY/4bXxPjB5yj4AAIA/AkRGPv7pgb4AEhE/AOAIPwAQTT813h4/vl7lPoFbvz4AAIA/AohFPv5Zgb6A+RA/ACB5PwAAsjx6/zo/n1ANP2bN5j4AAIA/AvhFPv5Vgb4ABxE/ACB5PwAAsjx6/zo/n1ANP2bN5j4AAIA/AoREPv0Db76A8RA/AEBxPwAQYj9DeTs/LuoIPzqF5z4AAIA/AlxEPv5Bgr4AzxA/AMAIPwAgTT+8GiQ/sQruPn0qxz4AAIA/AiBFPv4Rgr6A5hA/ANAIPwAQTT/IUCY/4bXxPjB5yj4AAIA/AlRFPv5Bgr6AAhE/ANAIPwAwTT9AAR4/avHjPt4Svj4AAIA/AjBEPv5hgb4A0RA/ADB5PwAAtjx6/zo/n1ANP2bN5j4AAIA/AohFPv5Zgb6A+RA/ACB5PwAAsjx6/zo/n1ANP2bN5j4AAIA/AoREPv0Db76A8RA/AEBxPwAQYj9DeTs/LuoIPzqF5z4AAIA/AjBEPv5hgb4A0RA/ADB5PwAAtjx6/zo/n1ANP2bN5j4AAIA/ApxEPv37bb4AxxA/ACBxPwAQYj+z5zw/VBsKP7Wu6T4AAIA/AoREPv0Db76A8RA/AEBxPwAQYj9DeTs/LuoIPzqF5z4AAIA/";

        [Serializable] sealed class Frame { public string sourceSha256; public float[] nativeToUnity; }
        [Serializable] sealed class Endpoint { public string name; public bool imported; public int frames; public float maximumNativeDelta; }
        [Serializable] sealed class Audit
        {
            public string status = "PREPARING", unityVersion, sourceFbxSha256, sourceContractSha256, frameAuditSha256, cornersSha256, rendererType, colorFormat;
            public int vertices, triangles, drawnVertices, unusedVertices, corners = CornerCount;
            public float existingEyeMarkerError, importedMarkerError, nativePositionError, uvError, linearColorError, neutralBakeError;
            public float positionTolerance = PositionTolerance, uvTolerance = UvTolerance, colorTolerance = ColorTolerance;
            public float[] nativeToUnity, colorMin, colorMax;
            public string[] importedShapeNames;
            public Endpoint[] endpoints;
            public bool sourceUnchanged, canonicalEndpointsStatic = true, materialListLeftToCaller = true;
            public string limitation = "One additive neutral patch only. Canonical A/seal endpoints equal Basis. No functional speech/blink deformation or likeness claim. Lower extraction remnants remain; no watertight-head claim. Every drawn corner is checked against frozen FBX native positions/UV/linear color; Unity UNorm8 half-step allowance is explicit.";
        }
        sealed class Corner { public Vector3 position; public Vector2 uv; public Color color; }

        public static Renderer Add(RenDesignerEyeReview review, string exportFolder)
        {
            if (Application.dataPath.Replace('\\', '/').IndexOf("/LucidLoopScratch/ren-eye-import-verification/Assets", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Designer patch import is restricted to the existing scratch project.");
            if (!review || !review.ModelRoot) throw new ArgumentNullException(nameof(review));
            Directory.CreateDirectory(Root);
            var audit = new Audit { unityVersion = Application.unityVersion, sourceFbxSha256 = FbxHash, cornersSha256 = CornersSha256 };
            string source = Path.Combine(exportFolder, FbxName), contract = Path.Combine(exportFolder, "contract.json");
            try
            {
                Verify(source, FbxHash);
                audit.sourceContractSha256 = Hash(contract);
                audit.frameAuditSha256 = Hash(FrameAudit);
                if (!string.IsNullOrEmpty(review.ImportAuditSha256) && audit.frameAuditSha256 != review.ImportAuditSha256)
                    throw new InvalidDataException("Existing neutral-eye frame audit differs from the loaded review.");
                var frame = JsonUtility.FromJson<Frame>(File.ReadAllText(FrameAudit));
                if (frame.sourceSha256 != EyeFbxHash || frame.nativeToUnity == null || frame.nativeToUnity.Length != 16)
                    throw new InvalidDataException("Expected frozen neutral-eye native frame is absent.");
                var nativeToUnity = Matrix(frame.nativeToUnity); audit.nativeToUnity = frame.nativeToUnity;
                audit.existingEyeMarkerError = MarkerError(Find(review.ModelRoot, "NeutralDesignerEyes"), nativeToUnity);
                if (audit.existingEyeMarkerError > 2e-5f) throw new InvalidDataException("Loaded eye transform no longer matches its import audit.");
                var payload = Convert.FromBase64String(CornersBase64);
                if (Hash(payload) != CornersSha256 || payload.Length != CornerCount * 9 * 4)
                    throw new InvalidDataException("Embedded frozen-FBX corner payload mismatch.");
                var corners = ReadCorners(payload);
                File.Copy(source, Root + "/" + FbxName, true);
                File.Copy(contract, Root + "/SourceContract.json", true);
                File.WriteAllBytes(Root + "/CanonicalCorners.f32", payload);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var asset = Root + "/" + FbxName;
                var importer = AssetImporter.GetAtPath(asset) as ModelImporter;
                if (!importer) throw new InvalidDataException("Patch FBX importer absent.");
                importer.isReadable = true; importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
                importer.importBlendShapes = true; importer.importNormals = ModelImporterNormals.Import;
                importer.importBlendShapeNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.Import;
                importer.meshCompression = ModelImporterMeshCompression.Off; importer.optimizeMeshVertices = false; importer.optimizeMeshPolygons = false;
                importer.weldVertices = false; importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
                Verify(asset, FbxHash);
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset)); instance.name = "NeutralEyeInterfacePatch";
                var importedNative = MarkerMatrix(instance.transform);
                var wrapper = new GameObject("DesignerPatchRegistration").transform; wrapper.SetParent(review.ModelRoot, false); instance.transform.SetParent(wrapper, false);
                SetMatrix(wrapper, review.ModelRoot.worldToLocalMatrix * nativeToUnity * importedNative.inverse);
                audit.importedMarkerError = MarkerError(instance.transform, nativeToUnity);
                if (audit.importedMarkerError > 2e-5f) throw new InvalidDataException("Imported patch native-marker calibration failed: " + audit.importedMarkerError);
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length != 1 || renderers[0].name != PatchName) throw new InvalidDataException("Patch export must contain exactly the requested one renderer.");
                var renderer = renderers[0]; var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
                if (!mesh) throw new InvalidDataException("Imported patch mesh absent.");
                var used = Enumerable.Range(0, mesh.subMeshCount).SelectMany(i => mesh.GetIndices(i)).Distinct().ToArray();
                var vertices = mesh.vertices; var uv = mesh.uv; var colors = mesh.colors;
                audit.vertices = mesh.vertexCount; audit.triangles = Enumerable.Range(0, mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i)) / 3;
                audit.drawnVertices = used.Length; audit.unusedVertices = mesh.vertexCount - used.Length; audit.rendererType = renderer.GetType().Name;
                if (audit.triangles != 62 || used.Length == 0 || colors.Length != mesh.vertexCount || uv.Length != mesh.vertexCount)
                    throw new InvalidDataException("Imported patch drawn triangle/UV/color count mismatch.");
                audit.colorFormat = mesh.GetVertexAttributeFormat(VertexAttribute.Color).ToString();
                var toNative = nativeToUnity.inverse * renderer.transform.localToWorldMatrix;
                foreach (var index in used)
                {
                    var p = toNative.MultiplyPoint3x4(vertices[index]);
                    var candidates = corners.Where(c => Vector3.Distance(c.position, p) <= PositionTolerance && Vector2.Distance(c.uv, uv[index]) <= UvTolerance).ToArray();
                    if (candidates.Length == 0) throw new InvalidDataException("No frozen FBX native position/UV corner match at patch vertex " + index + ": " + p.ToString("R"));
                    var best = candidates.OrderBy(c => ColorError(c.color, colors[index])).First(); var colorError = ColorError(best.color, colors[index]);
                    if (colorError > ColorTolerance || colors[index].a != 1) throw new InvalidDataException("Patch drawn linear pigment mismatch at " + index + ": " + colorError);
                    audit.nativePositionError = Mathf.Max(audit.nativePositionError, Vector3.Distance(best.position, p));
                    audit.uvError = Mathf.Max(audit.uvError, Vector2.Distance(best.uv, uv[index])); audit.linearColorError = Mathf.Max(audit.linearColorError, colorError);
                }
                audit.colorMin = Enumerable.Range(0, 4).Select(c => used.Min(i => colors[i][c])).ToArray();
                audit.colorMax = Enumerable.Range(0, 4).Select(c => used.Max(i => colors[i][c])).ToArray();
                audit.importedShapeNames = Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();
                var allowed = new[] { "jawOpen_A", "mouthSeal" };
                if (audit.importedShapeNames.Any(n => !allowed.Contains(n))) throw new InvalidDataException("Unexpected patch shape name; do not assume old eye controls.");
                audit.endpoints = allowed.Select(name =>
                {
                    var index = mesh.GetBlendShapeIndex(name); var row = new Endpoint { name = name, imported = index >= 0 };
                    if (index < 0) return row; // Canonical payload explicitly has zero deltas; importer may omit these shapes.
                    row.frames = mesh.GetBlendShapeFrameCount(index); if (row.frames != 1) throw new InvalidDataException("Unexpected patch shape frame count.");
                    var delta = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(index, 0, delta, null, null);
                    row.maximumNativeDelta = used.Max(i => toNative.MultiplyVector(delta[i]).magnitude);
                    if (row.maximumNativeDelta > PositionTolerance) throw new InvalidDataException("Expected static patch canonical endpoint moved: " + name);
                    if (skin) skin.SetBlendShapeWeight(index, 0); return row;
                }).ToArray();
                if (skin)
                {
                    var baked = new Mesh(); skin.BakeMesh(baked, false);
                    try { if (baked.vertexCount != mesh.vertexCount) throw new InvalidDataException("Patch neutral bake vertex count changed."); var points = baked.vertices; foreach (var i in used) audit.neutralBakeError = Mathf.Max(audit.neutralBakeError, toNative.MultiplyVector(points[i] - vertices[i]).magnitude); }
                    finally { Object.DestroyImmediate(baked); }
                    if (audit.neutralBakeError > PositionTolerance) throw new InvalidDataException("Actual rendered neutral patch differs from imported Basis.");
                }
                var donor = review.NewEyes.Single(r => r.name == "Ren_DesignerEye_L_SkinShutter").sharedMaterial;
                var material = new Material(donor) { name = "Ren_H_EyeInterface_L_MatchedSkin" };
                material.SetVector("_BaseColor", Vector4.one); material.SetFloat("_UseVertexColor", 1); material.SetFloat("_VertexColorSrgb", 0);
                material.SetFloat("_UseBaseMap", 0); material.SetFloat("_UseControlMap", 0); material.SetFloat("_UseShadowMap", 0); material.SetFloat("_ClosedWeight", 0);
                material.SetVector("_ControlFallback", new Vector4(1, 0, 0, 1)); material.SetFloat("_FaceMode", .9f); material.SetFloat("_HighlightStrength", 0); material.SetFloat("_RimStrength", 0); material.SetFloat("_Cull", 0); material.SetFloat("_Unlit", 0);
                foreach (var property in new[] { "_BaseMap", "_ControlMap", "_ShadowMap", "_ClosedBaseMap", "_ClosedShadowMap" }) if (material.HasProperty(property)) material.SetTexture(property, null);
                var materialPath = Root + "/MatchedSkin.mat"; var saved = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (saved) { saved.CopyPropertiesFromMaterial(material); Object.DestroyImmediate(material); material = saved; } else AssetDatabase.CreateAsset(material, materialPath);
                renderer.sharedMaterials = Enumerable.Repeat(material, mesh.subMeshCount).ToArray(); renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                audit.sourceUnchanged = Hash(source) == FbxHash && Hash(contract) == audit.sourceContractSha256;
                if (!audit.sourceUnchanged) throw new InvalidDataException("Frozen patch source changed during scratch import.");
                audit.status = "PASS_ACTUAL_PATCH_IMPORT_NATIVE_CORNER_COLOR_UV_STATIC_ENDPOINTS"; AssetDatabase.SaveAssets();
                return renderer; // The caller owns Review.Materials and capture-state membership.
            }
            catch (Exception e) { audit.status = "FAIL: " + e.Message; throw; }
            finally { File.WriteAllText(Root + "/PatchImportAudit.json", JsonUtility.ToJson(audit, true)); AssetDatabase.Refresh(); }
        }
        static Corner[] ReadCorners(byte[] bytes)
        {
            var rows = new Corner[CornerCount]; using (var reader = new BinaryReader(new MemoryStream(bytes))) for (int i = 0; i < rows.Length; i++) rows[i] = new Corner { position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), uv = new Vector2(reader.ReadSingle(), reader.ReadSingle()), color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()) }; return rows;
        }
        static float ColorError(Color a, Color b) => Enumerable.Range(0, 4).Max(i => Mathf.Abs(a[i] - b[i]));
        static Matrix4x4 Matrix(float[] values) { var m = Matrix4x4.identity; for (int i = 0; i < 16; i++) m[i / 4, i % 4] = values[i]; return m; }
        static Matrix4x4 MarkerMatrix(Transform root) { var origin = Find(root, "Ren_H_NativeAxis_Origin"); var m = Matrix4x4.identity; for (int i = 0; i < 3; i++) m.SetColumn(i, (Vector4)((Find(root, "Ren_H_NativeAxis_" + "XYZ"[i]).position - origin.position) / .01f)); m.SetColumn(3, new Vector4(origin.position.x, origin.position.y, origin.position.z, 1)); return m; }
        static float MarkerError(Transform root, Matrix4x4 nativeToUnity) { float error = 0; foreach (var item in new[] { ("Origin", Vector3.zero), ("X", Vector3.right * .01f), ("Y", Vector3.up * .01f), ("Z", Vector3.forward * .01f) }) error = Mathf.Max(error, Vector3.Distance(Find(root, "Ren_H_NativeAxis_" + item.Item1).position, nativeToUnity.MultiplyPoint3x4(item.Item2))); return error; }
        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        static void SetMatrix(Transform node, Matrix4x4 m) { var x = (Vector3)m.GetColumn(0); var y = (Vector3)m.GetColumn(1); var z = (Vector3)m.GetColumn(2); float sx = x.magnitude; if (Vector3.Dot(Vector3.Cross(x, y), z) < 0) sx = -sx; node.localPosition = m.GetColumn(3); node.localRotation = Quaternion.LookRotation(z, y); node.localScale = new Vector3(sx, y.magnitude, z.magnitude); var actual = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale); if (Enumerable.Range(0, 16).Any(i => Mathf.Abs(actual[i] - m[i]) > 1e-4f)) throw new InvalidDataException("Patch registration contains unsupported shear."); }
        static string Hash(string path) => Hash(File.ReadAllBytes(path));
        static string Hash(byte[] bytes) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void Verify(string path, string expected) { if (Hash(path) != expected) throw new InvalidDataException("Frozen input mismatch: " + path); }
    }
}
