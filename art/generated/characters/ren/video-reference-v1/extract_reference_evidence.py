"""Regenerate contact sheets and decoded samples from the immutable local MP4.

Requires local ffmpeg/ffprobe and Pillow. Does not upload, download, or modify video.
Full-resolution PNG and WAV intermediates go to .local, not the commit package.
"""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import argparse
import hashlib
import json
import subprocess

ROOT=Path(__file__).resolve().parent
REPO=ROOT.parents[4]
SHA='d479c1eeb5eab7a15fa0c489766ef1e22983289d33a5ef51ab32398dc73ea2f1'
DEFAULT_BIN='C:/Users/jetha/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-8.0.1-full_build/bin'

def digest(path):return hashlib.file_digest(path.open('rb'),'sha256').hexdigest()
def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,default=REPO/'DanielDuguay87_2093375826557296673.mp4')
    parser.add_argument('--bin',type=Path,default=Path(DEFAULT_BIN))
    parser.add_argument('--intermediates',type=Path,default=REPO/'.local/ren-video-reference/reproduced')
    args=parser.parse_args()
    if digest(args.source)!=SHA:raise RuntimeError('Source SHA mismatch; do not overwrite reference evidence for another video.')
    ffmpeg=str(args.bin/'ffmpeg.exe'); ffprobe=str(args.bin/'ffprobe.exe')
    work=args.intermediates.resolve();work.mkdir(parents=True,exist_ok=True)
    samples=work/'samples'; samples.mkdir(exist_ok=True)
    evidence=ROOT/'evidence'; evidence.mkdir(exist_ok=True)
    probe=subprocess.run([ffprobe,'-v','error','-show_format','-show_streams','-of','json',str(args.source)],check=True,capture_output=True,text=True)
    meta=json.loads(probe.stdout);meta.update(source_sha256=SHA,source_path=str(args.source))
    (ROOT/'source-metadata.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8')
    probe=subprocess.run([ffprobe,'-v','error','-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time','-of','json',str(args.source)],check=True,capture_output=True,text=True)
    frames=json.loads(probe.stdout)['frames']
    times=[{'sourceFrame':i,'time':float(f['best_effort_timestamp_time'])} for i,f in enumerate(frames)]
    (ROOT/'source-frame-times.json').write_text(json.dumps({'sourceSha256':SHA,'method':'ffprobe best_effort_timestamp_time, decoded video stream v:0','frames':times},indent=2)+'\n',encoding='utf-8')
    subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-i',str(args.source),'-vf',r'select=not(mod(n\,3))','-vsync','0',str(samples/'source-%03d.png')],check=True)
    subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-i',str(args.source),'-vn','-ac','1','-ar','16000',str(work/'reference-mono16k.wav')],check=True)
    font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',20)
    paths=sorted(samples.glob('source-*.png'))
    records=[]
    for i,path in enumerate(paths):
        records.append({'sourceFrame':i*3,'time':times[i*3]['time'],'file':str(path.relative_to(work)),'sha256':digest(path),'dimensions':list(Image.open(path).size)})
    (work/'decoded-sample-index.json').write_text(json.dumps(records,indent=2)+'\n',encoding='utf-8')
    overview=records[::5]
    for batch in range(2):
        out=Image.new('RGB',(960,1840),(27,29,33)); draw=ImageDraw.Draw(out)
        for i,r in enumerate(overview[batch*16:(batch+1)*16]):
            x=(i%4)*240;y=(i//4)*460
            im=Image.open(work/r['file']).convert('RGB');im.thumbnail((240,427))
            out.paste(im,(x,y));draw.text((x+5,y+430),f"{r['time']:05.2f}s  f{r['sourceFrame']:03}",font=font,fill='white')
        out.save(evidence/f'overview-{batch+1:02}.jpg',quality=94)
    for batch in range(10):
        out=Image.new('RGB',(1200,888),(27,29,33)); draw=ImageDraw.Draw(out)
        for i,r in enumerate(records[batch*15:(batch+1)*15]):
            x=(i%5)*240;y=(i//5)*296
            im=Image.open(work/r['file']).convert('RGB').crop((0,0,720,800)).resize((240,266),Image.Resampling.BICUBIC)
            out.paste(im,(x,y));draw.text((x+4,y+267),f"{r['time']:05.2f}s  f{r['sourceFrame']:03}",font=font,fill='white')
        out.save(evidence/f'face-timing-{batch+1:02}.jpg',quality=94)
    evidence_index={'sourceSha256':SHA,'pixelMethod':'Actual decoded video frames, only fixed crop/downsample and timestamp labels; no synthesis or retouching.',
        'overview':'Every15 source frames, 4 columns, 16 panels per sheet. 31 total panels.',
        'faceTiming':'Every3 source frames; fixed crop x0 y0 w720 h800 to240x266; 5 columns and3 rows,15 panels per sheet. 0.0--14.9 s; final15.0 s on overview02.',
        'timestamps':'Label rounded to .01 s; exact PTS in source-frame-times.json.',
        'ffmpegVersion':subprocess.run([ffmpeg,'-version'],check=True,capture_output=True,text=True).stdout.splitlines()[0],
        'files':[{'file':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':digest(p),'dimensions':list(Image.open(p).size)} for p in sorted(evidence.glob('*.jpg'))]}
    (ROOT/'evidence-index.json').write_text(json.dumps(evidence_index,indent=2)+'\n',encoding='utf-8')
    print(f'Extracted {len(paths)} actual 10 Hz samples; wrote {len(evidence_index["files"])} labeled contact sheets.')

if __name__=='__main__':main()
