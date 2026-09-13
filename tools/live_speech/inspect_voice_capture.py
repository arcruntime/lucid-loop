"""Verify a downloaded NPC voice capture; report clocks without inventing alignment."""
import argparse
import array
import hashlib
import json
from pathlib import Path
import sys
import wave


def inspect(directory):
    root = Path(directory).resolve()
    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
    summaries = []
    for result in manifest["results"]:
        npc = result["npcId"]
        if npc not in ("maya", "ren", "luca", "theo"):
            raise ValueError("Unknown NPC")
        folder = root / npc
        sample = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
        for record in sample["files"]:
            if record["path"] not in ("voice.wav", "events.json"):
                raise ValueError("Unexpected evidence file")
            data = (folder / record["path"]).read_bytes()
            if len(data) != record["bytes"] or hashlib.sha256(data).hexdigest() != record["sha256"]:
                raise ValueError(f"Evidence hash/size mismatch: {npc}/{record['path']}")
        with wave.open(str(folder / "voice.wav"), "rb") as wav:
            if (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) != (1, 2, 24000):
                raise ValueError("Unexpected PCM format")
            frames = wav.getnframes()
            pcm = array.array("h", wav.readframes(frames))
            if sys.byteorder != "little":
                pcm.byteswap()
        if frames != sample["samples"]:
            raise ValueError("Manifest sample count differs from WAV")
        events = json.loads((folder / "events.json").read_text(encoding="utf-8"))
        audio = events["audioReceipts"]
        captured = [a for a in audio if a["captured"]]
        cursor = 0
        for packet in captured:
            if packet["sampleStart"] != cursor or packet["sampleEnd"] - cursor != packet["receivedSamples"]:
                raise ValueError("Discontinuous PCM receipt spans")
            cursor = packet["sampleEnd"]
        if cursor != frames:
            raise ValueError("Packet spans differ from WAV")
        transcripts = [e for e in events["events"] if e["event"]["type"] == "session.output_transcript.delta"]
        summaries.append({
            "npcId": npc, "voice": sample["voice"], "status": sample["status"], "failure": sample["failure"],
            "samples": frames, "concatenatedAudioSeconds": frames / 24000,
            "audioPackets": len(audio), "packetsOutsideWindow": len(audio) - len(captured),
            "peakPcmMagnitude": max((abs(n) for n in pcm), default=0),
            "fullScaleSamples": sum(n in (-32768, 32767) for n in pcm),
            "nonzeroSamples": sum(n != 0 for n in pcm),
            "audioProviderFields": sorted({key for a in audio for key in a["providerFields"]}),
            "firstAudioReceiptMonoMs": audio[0]["receiptMonoMs"] if audio else None,
            "firstTranscriptReceiptMonoMs": transcripts[0]["receiptMonoMs"] if transcripts else None,
            "outputTranscriptDeltas": len(transcripts),
            "transcriptConcatenatedInReceiptOrder": "".join(e["event"]["delta"] for e in transcripts),
            "requestedText": sample["requestedText"], "finalUsagePresent": sample["finalUsage"] is not None,
            "hashesVerified": True,
        })
    return {"sourceSha": manifest["sourceSha"], "githubRunId": manifest["githubRunId"],
            "clockMappingEstablished": False, "audiblePlaybackMeasured": False,
            "note": "Receipt-order text is not a verified completed utterance. PCM concatenation may remove pauses.",
            "samples": summaries}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    report = json.dumps(inspect(args.directory), ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.write_text(report, encoding="utf-8")
    else:
        print(report, end="")
