"""Experimental, provisional Japanese reading frontend; never generates audio or timings."""
import argparse
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import unicodedata

CAST = {"ren": "レン", "maya": "マヤ", "luca": "ルカ", "theo": "テオ"}
PUNCTUATION = set('、。！？,.!?・…「」『』（）()：:；;“”"')


def normalize_input(text):
    if not isinstance(text, str) or not text.strip() or len(text.encode("utf-8")) > 2048:
        raise ValueError("invalid_or_oversized_text")
    normalized = unicodedata.normalize("NFKC", text)
    if len(normalized.encode("utf-8")) > 2048:
        raise ValueError("oversized_normalized_text")
    for char in normalized:
        code = ord(char)
        allowed = (char.isspace() or char in PUNCTUATION or char in "々〆ー" or
                   0x3041 <= code <= 0x3096 or 0x30A1 <= code <= 0x30FA or
                   0x3400 <= code <= 0x9FFF or 0xF900 <= code <= 0xFAFF or
                   0x20000 <= code <= 0x323AF or char.isascii() and char.isalnum())
        if not allowed:
            raise ValueError(f"unsupported_source_character:U+{code:04X}")
    return normalized


def checked_tokens(original, normalized, tokens):
    output, readings = [], []
    for token in tokens:
        surface = unicodedata.normalize("NFKC", token["string"])
        pron = token["pron"]
        entry = dict(token)
        if surface.casefold() in CAST:
            reading = CAST[surface.casefold()]
            entry["readingSource"] = "authored_cast_alias"
        elif any(ch.isascii() and ch.isalpha() for ch in surface):
            raise ValueError("unsupported_latin_word:" + surface)
        elif surface and all(ch.isspace() or ch in PUNCTUATION for ch in surface):
            reading = "。" if any(ch in surface for ch in "。.!?！？") else "、"
            entry["readingSource"] = "explicit_punctuation"
        else:
            if token["mora_size"] <= 0 or not pron or pron == "、":
                raise ValueError("unknown_spoken_token:" + surface)
            # Keep raw token.pron (including OpenJTalk devoicing annotations).
            # The kana handoff cannot parse these annotations; record their removal.
            reading = pron.replace("’", "").replace("'", "")
            if not reading or any(not (0x3041 <= ord(ch) <= 0x3096 or
                    0x30A1 <= ord(ch) <= 0x30FA or ch == "ー") for ch in reading):
                raise ValueError("unsupported_pronunciation:" + surface)
            entry["readingSource"] = "openjtalk_pronunciation_provisional"
        entry["checkedReading"] = reading
        entry["devoicingAnnotations"] = pron.count("’") + pron.count("'")
        output.append(entry)
        readings.append(reading)
    if not any(e["readingSource"] != "explicit_punctuation" for e in output):
        raise ValueError("empty_spoken_reading")
    return {"input": original, "normalizedInput": normalized, "reading": "".join(readings),
            "tokens": output, "status": "provisional_reading_not_audio_verified",
            "sourceOffsetsAvailable": False, "timingEstablished": False}


def read_text(text, dictionary):
    normalized = normalize_input(text)
    lock = json.loads(Path(__file__).with_name("japanese_reading_lock.json").read_text(encoding="utf-8"))
    if importlib.metadata.version("pyopenjtalk") != lock["pyopenjtalk"]:
        raise ValueError("unreviewed_frontend_version")
    dictionary = Path(dictionary).resolve()
    for record in lock["dictionaryFiles"]:
        data = (dictionary / record["name"]).read_bytes()
        if len(data) != record["bytes"] or hashlib.sha256(data).hexdigest() != record["sha256"]:
            raise ValueError("unreviewed_dictionary:" + record["name"])
    os.environ["OPEN_JTALK_DICT_DIR"] = str(dictionary)
    import pyopenjtalk
    # Use this verified dictionary even if another caller initialized the global
    # convenience frontend earlier in the Python process.
    frontend = pyopenjtalk.OpenJTalk(dn_mecab=str(dictionary).encode("utf-8"))
    return checked_tokens(text, normalized, frontend.run_frontend(normalized))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dictionary", required=True)
    parser.add_argument("--input", type=Path, required=True, help="UTF-8 JSON with a text field")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = read_text(json.loads(args.input.read_text(encoding="utf-8"))["text"], args.dictionary)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
