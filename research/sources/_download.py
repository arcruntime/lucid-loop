"""Download Arcane/Fortiche research snapshots into research/sources/."""
from __future__ import annotations

import ssl
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
ARTICLES = ROOT / "articles"
TRANSCRIPTS = ROOT / "transcripts"
ARTICLES.mkdir(parents=True, exist_ok=True)
TRANSCRIPTS.mkdir(parents=True, exist_ok=True)

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
)

PAGES = [
    ("redshark-arcane-pipeline.html", "https://www.redsharknews.com/why-netflixs-arcane-looks-so-good-how-fortiche-ramped-up-the-animation-pipeline"),
    ("syncsketch-wanneroy.html", "https://blog.syncsketch.com/creator-stories/arcane-fortiche/"),
    ("animationxpress-fortiche.html", "https://www.animationxpress.com/animation/talent-experimentation-originality-how-fortiche-revolutionised-animated-storytelling-with-arcane/"),
    ("yahoo-creativebloq-s2.html", "https://www.yahoo.com/entertainment/tv/articles/creative-team-behind-netflixs-arcane-090000492.html"),
    ("fortiche-fmx-2025.html", "https://forticheprod.com/blog/projects-events/fortiche-rocks-fmx-2025-with-arcane-season-2-deep-dive/"),
    ("80lv-backgrounds-3d-matte.html", "https://80.lv/articles/arcane-artists-show-how-they-combine-traditional-art-3d-for-backgrounds"),
    ("80lv-texturing-dump.html", "https://80.lv/articles/a-closer-look-at-texturing-in-arcane"),
    ("80lv-nabirenkov-cannon.html", "https://80.lv/articles/creating-a-game-ready-cannon-in-an-arcane-inspired-style"),
    ("80lv-lima-handpaint.html", "https://80.lv/articles/how-to-enhance-hand-painting-with-blender-shaders-for-stylized-character-art"),
    ("80lv-baudry-piltover-zaun.html", "https://80.lv/articles/riot-games-on-designing-arcane-s-piltover-zaun/"),
    ("80lv-zaun-fps.html", "https://80.lv/articles/artist-created-stylish-arcane-inspired-fps-set-in-zaun-using-ue5"),
    ("80lv-firelights-ue5.html", "https://80.lv/articles/firelights-base-from-arcane-recreated-in-unreal-engine-5"),
    ("80lv-klimov-lighting.html", "https://80.lv/articles/recreating-lighting-from-arcane-league-of-legends-in-unreal"),
    ("80lv-finger-lighting.html", "https://80.lv/articles/mesmerizing-arcane-inspired-day-night-lighting-in-unreal-engine-5"),
    ("80lv-fortiche-animation-refs.html", "https://80.lv/articles/see-how-fortiche-made-arcane-animations-look-so-realistic"),
    ("incg-siggraph-asia-2024.html", "https://www.incgmedia.com/makingof/siggraph-asia-2024-arcane-season-2"),
    ("jettelly-unity-painterly.html", "https://jettelly.com/blog/recreating-a-painterly-shader-in-unity-urp/"),
    ("ue-forum-arcane-shader.html", "https://forums.unrealengine.com/t/arcane-shader/2225784/2"),
    ("acm-crafting-the-bridge.html", "https://dl.acm.org/doi/10.1145/3577023.3585274"),
    ("acm-crafting-the-bridge.pdf", "https://dl.acm.org/doi/pdf/10.1145/3577023.3585274"),
    ("riotupdates-production.html", "https://riotupdates.com/arcane/production/"),
    ("riotupdates-timeline.csv", "https://riotupdates.com/data/arcane-production-timeline.csv"),
    ("riotupdates-pipeline.csv", "https://riotupdates.com/data/arcane-production-pipeline.csv"),
    ("autodesk-golaem-crowds.html", "https://blogs.autodesk.com/media-and-entertainment/2025/03/26/crafting-crowds-fortiche-golaem-and-the-magic-of-arcane-season-2/"),
    ("vfxvoice-s2.html", "https://vfxvoice.com/riot-games-and-fortiche-get-revolutionary-with-arcane-season-2/"),
    ("awn-s2.html", "https://www.awn.com/animationworld/riot-games-and-fortiche-push-every-possible-boundary-arcane-season-2"),
    ("indiewire-arcane.html", "https://www.indiewire.com/2022/06/arcane-netflix-league-of-legends-1234733820/"),
]

ctx = ssl.create_default_context()


def fetch(url: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "*/*"})
    with urllib.request.urlopen(req, context=ctx, timeout=45) as resp:
        return resp.read()


def main() -> None:
    ok = 0
    fail = 0
    for name, url in PAGES:
        dest = ARTICLES / name
        try:
            data = fetch(url)
            dest.write_bytes(data)
            print(f"OK  {len(data):8d}  {name}")
            ok += 1
        except Exception as exc:
            print(f"FAIL {name}: {exc}")
            fail += 1
    print(f"done pages ok={ok} fail={fail}")


if __name__ == "__main__":
    main()
