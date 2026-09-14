"""Finish existing corrected LOD1; never reduce hair or uniformly reduce visible face."""
from pathlib import Path
import sys,shutil,json
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_ren_lod1_final as build
out=ROOT/'art/generated/characters/ren/lod1-final-v1'
private=ROOT/'.local/ren-lod1-final-preserved';private.mkdir(parents=True,exist_ok=True)
snapshot=private/'Ren_LOD1_corrected_13418.blend'
if not snapshot.exists():shutil.copyfile(out/'Ren_LOD1.blend',snapshot)
build.SRC=snapshot
build.PROTECT_VISIBLE_HEAD=True
build.BUDGET={'RenBody_LOD0':1400,'RenNeckChest_LOD0':400,'RenHeadSkin':2200,'RenLiveHair':5651,'RenCap_Static':240,'RenHeadphones_Static':280,'RenEyesShallow':398,'RenEarJewelry':179,'RenInkBrowsLashes':250,'RenUpperTeeth':70,'RenLowerTeeth':70,'RenUpperGums':60,'RenLowerGums':60,'RenTongue':140,'RenNeckJoin':293}
build.main()
p=out/'manifest.json';d=json.loads(p.read_text());d['root_lod0_source']='art/generated/characters/ren/lod0-final-v1/Ren_LOD0.blend';d['root_lod0_sha256']=build.sha(ROOT/d['root_lod0_source']);d['status']='FINAL_BUDGET_CHECK_REQUIRED';p.write_text(json.dumps(d,indent=2))
