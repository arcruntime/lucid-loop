"""Read-only appearance landmark estimates; writes JSON, never edited images."""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def roi(a, bounds):
    scale = a.shape[0] / 1024
    x0, y0, x1, y1 = [round(v * scale) for v in bounds]
    return a[y0:y1, x0:x1], x0, y0, scale


def measure(path):
    a = np.asarray(Image.open(path).convert("RGB")).astype(float)
    if a.shape[0] != a.shape[1]:
        raise ValueError("This trial uses square images only")
    result = dict(file=str(path.relative_to(ROOT)), width=a.shape[1], height=a.shape[0], sha256=sha(path))
    landmarks = {}
    for name, bounds in {"left_iris": (380, 375, 425, 460), "right_iris": (599, 375, 645, 460)}.items():
        z, x0, y0, scale = roi(a, bounds)
        mask = (z[..., 2] - z[..., 0] > 8) & (z[..., 1] - z[..., 0] > 2) & (z[..., 0] < 190)
        y, x = np.where(mask)
        landmarks[name] = dict(x=float((x.mean() + x0) / scale),
                               y=float((y.mean() + y0) / scale), supporting_pixels=len(x))
    for name, bounds in {"left_nostril": (477, 515, 505, 572), "right_nostril": (525, 515, 550, 572)}.items():
        z, x0, y0, scale = roi(a, bounds)
        light = z.mean(axis=2)
        y, x = np.where(light <= np.quantile(light, .07))
        landmarks[name] = dict(x=float((x.mean() + x0) / scale),
                               y=float((y.mean() + y0) / scale), supporting_pixels=len(x))
    z, x0, y0, scale = roi(a, (480, 600, 544, 668))
    darkest_rows = z.mean(axis=2).argmin(axis=0)
    landmarks["mouth_seam"] = dict(y=float((np.median(darkest_rows) + y0) / scale))
    result["appearance_landmarks_normalized_1024"] = landmarks
    mask = a[..., 0] - a[..., 2] > 10
    labels, _ = ndimage.label(mask)
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    mask = ndimage.binary_fill_holes(labels == sizes.argmax())
    # Coordinate sampling is measurement only; no resampled image is saved.
    index = np.minimum(((np.arange(1024) + .5) * a.shape[0] / 1024).astype(int), a.shape[0] - 1)
    sample = mask[np.ix_(index, index)]
    yy, xx = np.where(sample)
    result["estimated_silhouette_bbox_normalized_1024"] = [int(xx.min()), int(yy.min()), int(xx.max()), int(yy.max())]
    return result, sample


paths = [ROOT / "guides/front-neutral-skin-with-eyes.png", ROOT / "painted-views/front-paint-v1.png", ROOT / "painted-views/front-paint-v2.png"]
reports = []
base, base_mask = measure(paths[0])
reports.append(base)
for path in paths[1:]:
    result, mask = measure(path)
    result["estimated_silhouette_iou_to_guide"] = float((mask & base_mask).sum() / (mask | base_mask).sum())
    result["appearance_landmark_delta_to_guide"] = {
        name: {axis: value[axis] - base["appearance_landmarks_normalized_1024"][name][axis]
               for axis in ("x", "y") if axis in value}
        for name, value in result["appearance_landmarks_normalized_1024"].items()
    }
    reports.append(result)
output = dict(status="DIAGNOSTIC_ESTIMATES_NOT_GEOMETRIC_REGISTRATION", coordinate_system="top-left origin, normalized to guide's 1024-square camera framing",
              method={"iris": "centroid of blue-biased pixels inside manually fixed eye ROIs",
                      "nostril": "centroid of darkest 7 percent of pixels inside fixed nostril ROIs",
                      "mouth": "median darkest row across central mouth columns",
                      "silhouette": "largest warm-color component, filled internal holes; nearest coordinate sampling for comparison only"},
              limitations=["Changes to pigment, liner, light and highlights bias appearance centroids; these are estimates, not exact mesh landmarks.",
                           "No image pixels or original PNG bytes are modified by this analysis.",
                           "Similar silhouette overlap does not establish facial feature alignment or projection approval."], images=reports)
(ROOT / "alignment-review.json").write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")
for row in reports:
    print(row["file"], row["estimated_silhouette_bbox_normalized_1024"], row.get("estimated_silhouette_iou_to_guide"), row.get("appearance_landmark_delta_to_guide"))
