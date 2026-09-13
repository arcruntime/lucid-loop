"""Original Lucid Loop demo music. Synthesized from oscillators/noise, no samples.

Usage: python tools/generate_club_loops.py --output Unity/Assets/Gyms/Audio
Requires numpy. Deterministic 48 kHz stereo 16-bit PCM, circular event mixing.
"""
import argparse
import hashlib
import json
import wave
from pathlib import Path
import numpy as np

RATE = 48000


def tone(note):
    return 440 * 2 ** ((note - 69) / 12)


def make_loop(mood):
    aggressive = mood == "aggressive"
    bpm = 120 if aggressive else 96
    beat = 60 / bpm
    count = round(32 * beat * RATE)
    mix = np.zeros((count, 2), dtype=np.float64)
    rng = np.random.default_rng(314159 if aggressive else 271828)

    def add(sound, position, gain=1, pan=0):
        # Circular tails carry over the boundary instead of fading the entire track out.
        idx = (np.arange(len(sound)) + round(position * RATE)) % count
        mix[idx, 0] += sound * gain * np.sqrt((1-pan) / 2)
        mix[idx, 1] += sound * gain * np.sqrt((1+pan) / 2)

    def grid(duration):
        return np.arange(round(duration * RATE)) / RATE

    def envelope(t, duration, attack=.01, release=.04):
        return np.minimum(1, t / attack) * np.minimum(1, np.maximum(0, duration-t) / release)

    for step in range(32):
        # Pitch-falling sine kick; phase is integral of instantaneous frequency.
        t = grid(.35)
        phase = 2*np.pi*(48*t + 100*.018*(1-np.exp(-t/.018)))
        kick = np.sin(phase)*np.exp(-t/ .075)*envelope(t, .35, .001, .015)
        add(kick, step*beat, .52 if aggressive else .33)
        if step % 4 in (1, 3):
            t = grid(.17)
            noise = rng.normal(0, 1, len(t))
            noise = noise - np.convolve(noise, np.ones(15)/15, mode="same")
            clap = np.tanh(noise)*np.exp(-t/.035)*envelope(t,.17,.002,.015)
            add(clap, step*beat, .105 if aggressive else .055, .08)
        for off in (0, .5):
            t = grid(.09)
            noise = rng.normal(0, 1, len(t))
            noise = np.diff(noise, prepend=noise[0])
            hat = np.tanh(noise)*np.exp(-t/.018)*envelope(t,.09,.001,.01)
            add(hat, (step+off)*beat, (.036 if off == 0 else .064)*(1 if aggressive else .6), -.3 if off == 0 else .3)

        root = [38, 34, 41, 36][step//8]  # D minor, Bb, F, C.
        for off in ((.5,) if not aggressive else (.0, .5, .75)):
            duration = beat * (.38 if aggressive else .65)
            t = grid(duration)
            freq = tone(root)
            bass = (np.sin(2*np.pi*freq*t) + .19*np.sin(4*np.pi*freq*t))
            bass *= envelope(t, duration, .006, .035)*np.exp(-t/(beat*.42))
            add(bass, (step+off)*beat, .21 if aggressive else .16)

    chords = [[62,65,69,72], [58,62,65,69], [60,65,69,72], [60,64,67,70]]
    for chord_index, chord in enumerate(chords):
        duration = 9*beat
        t = grid(duration)
        env = envelope(t, duration, .7*beat, 1.3*beat)
        for voice, note in enumerate(chord):
            freq = tone(note)
            pad = (np.sin(2*np.pi*freq*t) + .11*np.sin(4*np.pi*freq*t)) * env
            add(pad, chord_index*8*beat, .022 if aggressive else .042, (voice-1.5)/2)
        if aggressive:
            for step in (0, 1.5, 3, 4, 5.5, 7):
                duration = .29
                t = grid(duration)
                note = chord[int(step)%4]+12
                stab = (np.sin(2*np.pi*tone(note)*t)+.2*np.sin(6*np.pi*tone(note)*t))
                stab *= envelope(t,duration,.003,.035)*np.exp(-t/.085)
                add(stab, (chord_index*8+step)*beat,.07, -.2 if step%2==0 else .2)

    # Whole-loop DC removal retains periodicity. Common peak ceiling leaves mix headroom.
    mix -= mix.mean(axis=0)
    mix *= .72 / np.max(np.abs(mix))
    pcm = np.round(mix * 32767).astype("<i2")
    delta = np.diff(pcm.astype(np.float64), axis=0)
    seam = np.abs(pcm[0].astype(np.float64)-pcm[-1].astype(np.float64))
    metrics = {"mood":mood,"bpm":bpm,"bars":8,"sample_rate":RATE,"channels":2,
        "samples_per_channel":count,"duration_seconds":count/RATE,
        "peak_dbfs":float(20*np.log10(np.max(np.abs(mix)))),
        "rms_dbfs":float(20*np.log10(np.sqrt(np.mean(mix**2)))),
        "boundary_delta_pcm":seam.tolist(),"interior_delta_p99_pcm":float(np.quantile(np.abs(delta),.99)),
        "clipped_samples":int(np.sum(np.abs(pcm.astype(np.int32))>=32767)),
        "seam_check":bool(np.max(seam)<np.quantile(np.abs(delta),.99)),
        "audition":"Not auditioned; numerical checks do not establish musical quality."}
    assert metrics["seam_check"] and metrics["clipped_samples"] == 0
    return pcm, metrics


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    results=[]
    for mood in ("aggressive","intimate"):
        pcm,metrics=make_loop(mood)
        path=args.output/f"club_{mood}_120bpm.wav" if mood=="aggressive" else args.output/f"club_{mood}_96bpm.wav"
        with wave.open(str(path),"wb") as output:
            output.setnchannels(2);output.setsampwidth(2);output.setframerate(RATE);output.writeframes(pcm.tobytes())
        metrics.update(file=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        results.append(metrics)
    manifest={"provenance":"Original procedural composition and synthesized audio created for Lucid Loop. No third-party audio samples.","loops":results}
    (args.output/"music-provenance.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(manifest,indent=2))


if __name__=="__main__":
    main()
