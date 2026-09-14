from pathlib import Path
import hashlib,json,subprocess

root=Path(__file__).resolve().parent
ffmpeg=Path('C:/Users/jetha/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-8.0.1-full_build/bin/ffmpeg.exe')
video=root/'Ren-Designer-Blink-and-Mouth-5s.mp4'
assert not video.exists(), 'Preserve existing output; use a new review folder for another encoding.'
args=[str(ffmpeg),'-hide_banner','-loglevel','warning','-framerate','30','-start_number','0','-i',str(root/'live-review/motion-frames/%04d.png'),'-frames:v','150','-an','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-movflags','+faststart',str(video)]
subprocess.run(args,check=True)
probe=subprocess.run([str(ffmpeg.with_name('ffprobe.exe')),'-v','error','-count_frames','-show_streams','-show_format','-of','json',str(video)],check=True,capture_output=True,text=True)
data=json.loads(probe.stdout);stream=data['streams'][0]
assert len(data['streams'])==1 and stream['codec_type']=='video' and stream['nb_read_frames']=='150' and stream['r_frame_rate']=='30/1'
assert abs(float(data['format']['duration'])-5)<1e-6
metadata={'status':'ENCODED_DETERMINISTIC_GPU_FRAMES_NOT_REALTIME_PERFORMANCE','video':video.name,'sha256':hashlib.sha256(video.read_bytes()).hexdigest(),'frames':150,'fps':30,'duration':5,'audioIncluded':False,'dimensions':[960,960],'encodingArguments':args,'probe':data,'sourceEvidenceSha256':hashlib.sha256((root/'live-review/BlinkLiveEvidence.json').read_bytes()).hexdigest(),'limitation':'No source audio or source-video tracking; fixed output cadence encodes separately rendered live Unity frames. Simultaneous basic A/seal and blink only. Source PNG intermediates stay local.'}
(root/'motion-encoding.json').write_text(json.dumps(metadata,indent=2)+'\n')
print(json.dumps({k:metadata[k] for k in ['video','sha256','frames','fps','duration','audioIncluded']},indent=2))
