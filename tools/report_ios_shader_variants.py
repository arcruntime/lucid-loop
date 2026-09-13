"""Summarize a completed IosBuild export without publishing its full Editor log.

Unity logs localized grouping separators in integer counts; these are NOT decimals.
Uses only the most recent IOS_CONFIGURED ... Active target: iOS export segment.
"""
import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path


def number(value):
    return int(re.sub(r"[.,\s]", "", value))


def parse_log(text):
    starts = list(re.finditer(r"^IOS_CONFIGURED:.*Active target: iOS\s*$", text, re.M))
    if not starts:
        raise ValueError("No iOS export configuration marker in this log")
    start = starts[-1].start()
    end_match = re.search(r"^IOS_XCODE_EXPORT_OK:.*$", text[start:], re.M)
    if not end_match:
        raise ValueError("Latest iOS export has not completed successfully")
    end = start + end_match.end()
    segment = text[start:end]
    if "Build Finished, Result: Success." not in segment:
        raise ValueError("Export marker has no matching successful player build")
    records = []
    shader = None
    current = None
    fields = {"Full variant space": "full_variant_space", "After settings filtering": "after_settings",
              "After built-in stripping": "after_builtin", "After scriptable stripping": "after_scriptable"}
    for line in segment.splitlines():
        match = re.match(r'Compiling shader "(.+)"', line)
        if match:
            shader = match.group(1)
            current = None
            continue
        match = re.match(r'  Pass "(.*)" \(([^)]+)\)', line)
        if match and shader:
            current = {"shader": shader, "pass": match.group(1), "stage": match.group(2)}
            records.append(current)
            continue
        if current is None:
            continue
        match = re.match(r"    Target graphics API: (.+)", line)
        if match:
            current["graphics_api"] = match.group(1)
        for label, key in fields.items():
            match = re.match(r"    " + re.escape(label) + r":\s+([\d., ]+)\s*$", line)
            if match:
                current[key] = number(match.group(1))
    for record in records:
        if any(key not in record for key in fields.values()):
            raise ValueError("Incomplete shader pass record: " + str(record))
        counts = [record[key] for key in fields.values()]
        if counts != sorted(counts, reverse=True):
            raise ValueError("Non-monotonic filtering counts: " + str(record))
    times = re.findall(r'^##utp:(\{"type":"PlayerBuildInfo".*)$', segment, re.M)
    finished = None
    if times:
        finished = datetime.fromtimestamp(json.loads(times[-1])["time"] / 1000, timezone.utc).isoformat()
    return records, {"log": "Editor.log", "segment_start_line": text[:start].count("\n") + 1,
                     "segment_end_line": text[:end].count("\n") + 1,
                     "segment_sha256": hashlib.sha256(segment.encode()).hexdigest(),
                     "build_finished_utc": finished}


def make_report(log_text, stripping, selected):
    records, source = parse_log(log_text)
    if {record.get("graphics_api") for record in records} != {"metal"}:
        raise ValueError("Expected an iOS Metal-only shader log segment")
    shader_summaries = {shader["name"]: {"before_scriptable": shader["inputVariants"],
                        "after_scriptable": shader["outputVariants"]} for shader in stripping["shaders"]}
    details = {}
    for name in selected:
        passes = [{key: value for key, value in record.items() if key != "shader"}
                  for record in records if record["shader"] == name]
        if not passes or name not in shader_summaries:
            raise ValueError("Requested shader not present in both log and JSON: " + name)
        sums = {key: sum(row[key] for row in passes) for key in
                ("full_variant_space", "after_settings", "after_builtin", "after_scriptable")}
        if sums["after_builtin"] != shader_summaries[name]["before_scriptable"] or sums["after_scriptable"] != shader_summaries[name]["after_scriptable"]:
            raise ValueError("Log/JSON count mismatch; do not combine different builds: " + name)
        details[name] = {"totals_across_passes_and_stages": sums, "passes": passes}
    return {"schema_version": 1, "target": "iOS", "graphics_api": "Metal", "build_result": "successful Xcode export",
            "source": source, "unit": "Shader pass/stage variants, not unique shaders or draw calls",
            "interpretation": ["Full variant space is a theoretical keyword Cartesian product; it was not all compiled.",
                "JSON inputs are variants entering scriptable stripping AFTER settings and built-in filtering.",
                "This is one current-build measurement, not a before/after optimization or device-performance comparison.",
                "Compile inclusion does not prove that a required runtime keyword combination or visual effect is correct."],
            "scriptable_stripping": {"input_variants": stripping["totalVariantsIn"], "retained_variants": stripping["totalVariantsOut"],
                "shader_entries": len(stripping["shaders"])},
            "selected_shader_details": details, "all_shader_scriptable_counts": shader_summaries,
            "json_export_timing": "URP ShaderStrippingReportScope.OnPostprocessBuild -> ReportEnd -> DumpReport writes Temp/shader-stripping.json; absence during compilation is expected."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor-log", type=Path, required=True)
    parser.add_argument("--stripping-json", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--shader", action="append", help="Shader to detail; repeatable, defaults to URP Lit")
    args = parser.parse_args()
    raw = args.stripping_json.read_bytes()
    report = make_report(args.editor_log.read_text(encoding="utf-8", errors="replace"), json.loads(raw),
                         args.shader or ["Universal Render Pipeline/Lit"])
    report["source"]["stripping_json_sha256"] = hashlib.sha256(raw).hexdigest()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(args.output), **report["scriptable_stripping"],
                      "selected": {name: detail["totals_across_passes_and_stages"] for name, detail in report["selected_shader_details"].items()}}, indent=2))


if __name__ == "__main__":
    main()
