# English and Japanese speech rig coverage

User requirement updated 2026-09-12: **full English and Japanese speech blendshape coverage** for all five main characters. This supersedes English-only acceptance. The body/art/viewer objective continues unchanged.

## What coverage means

The rig must represent the visible articulations of both languages and expose them to a real-time driver. A list of named targets or five vowel poses alone is insufficient. Phonemes with the same visible articulation may share geometry; duration, voicing, pitch and kana spelling do not each require a separate mesh target. Distinct articulations must not be collapsed merely to preserve a fixed target count.

Retain the existing 15-target interface (`viseme_sil`, `PP`, `FF`, `TH`, `DD`, `kk`, `CH`, `SS`, `nn`, `RR`, `aa`, `E`, `I`, `O`, `U`, with `viseme_` on every target). Add authored controls or calibrated target poses where that interface cannot reproduce the bilingual distinction. Existing English model compatibility remains useful but does not prove Japanese support.

## Articulation acceptance matrix

| Coverage | Required visible behavior |
| --- | --- |
| English vowels | Calibrated open, spread, central/relaxed and rounded positions spanning the spoken vowel inventory; diphthongs transition between positions without resetting to silence. |
| English P/B/M | Actual lip seal, including corners, retained through expression and transitions. |
| English F/V | Lower lip contacts upper incisors; teeth stay anatomically placed. |
| English TH | Visible, controlled tongue-tip dental/interdental articulation. |
| English T/D/N/L | Alveolar tongue/contact family, with a distinct usable L pose rather than substituting English R. |
| English R | Rhotic articulation distinct from Japanese tap; speaker-specific tongue choice may be calibrated. |
| English S/Z/SH/ZH/CH/J | Narrow/fricative and affricate families with appropriate tongue/lip posture and timed release. |
| English K/G/NG, H, Y, W | Posterior contact or vowel-conditioned articulation, glide transitions and appropriate rounding. H does not force a spurious mouth closure. |
| Japanese あ・い・う・え・お | Five individually reviewable vowel presets. Japanese U must allow calibrated lip compression with less protrusion than the English rounded U pose. |
| Japanese ぱ・ば・ま families | Bilabial seal and vowel-conditioned release. |
| Japanese ふ and related loanword FU family | Bilabial frication pose; do not substitute the English lower-lip/upper-teeth F/V contact. |
| Japanese た・だ・な families | Alveolar contact and release, conditioned by the following vowel. |
| Japanese ら・り・る・れ・ろ | Brief tongue-tap articulation, not a held English rhotic pose. |
| Japanese か・が, medial nasal realizations | Posterior contact family with natural neighboring-vowel shape. |
| Japanese さ・ざ, し・じ, ち, つ | Review alveolar versus palatalized fricative/affricate postures and release; do not collapse all to one open vowel. |
| Japanese は・ひ, や・ゆ・よ, わ | Vowel-conditioned glottal/palatal articulation and glides; calibrate lip compression/rounding appropriately. |
| Japanese 拗音 (きゃ・しゅ・ちょ・にゃ etc.) | Coarticulated consonant/glide/vowel transition with palatal tongue shaping where visible. |
| Japanese ん | Context-sensitive nasal articulation; not always closed lips. Bilabial context may use the P/B/M seal while other contexts use non-bilabial poses. |
| Japanese っ, long vowels and devoiced vowels | Correct pose duration, held consonant contact/frication, and release. Quiet/devoiced vowels must not automatically snap the mouth to silence. |
| Japanese loanword sequences and code switching | Cover vowel combinations and sequences such as ファ/フィ/フェ/フォ, ティ/ディ, トゥ/ドゥ, ヴ and ウィ/ウェ/ウォ using calibrated profiles and the pronunciation actually spoken. |

The matrix is a project acceptance design. Its Japanese articulation distinctions are informed by Vance's phonological sketch, including compressed U, bilabial frication, tap R, contextual moraic nasals and length/devoicing. [NINJAL-hosted phonological sketch](https://www2.ninjal.ac.jp/past-events/labphon14/PhonSketch.pdf).

## Required interface additions

Reserve `viseme_ja_U`, `viseme_ja_FU`, `viseme_ja_R` and `viseme_en_L` for authored, independently inspectable poses where the baseline does not provide the distinction. These are production requirements, not claims that the targets already exist. Additional primitives/correctives are allowed when the matrix requires them.

Japanese A/I/E/O may reuse properly calibrated baseline geometry through language presets; they still need independent viewer entries and visual verification. Do not create zero-delta targets or duplicate names simply to pass a count check. Every language preset must declare its contributing targets and contact behavior.

Use a language-aware pose/profile interface alongside the legacy 15-target adapter. Unknown language/pose inputs must be reported, not silently remapped to an English approximation. The viewer must select English or Japanese pose sets and display all required isolated articulations, transitions and timing test sequences.

Morphs own jaw motion. Speech owns contact-critical mouth deformation; expression, blinks and gaze must compose without breaking seal/contact. Retain corresponding teeth/tongue/lash deformation and fit corrections for every character. The player's canonical mask stays on by default; a technical inspection option may reveal the inferred hidden rig for validation without claiming a source-drawn unmasked identity.

## Validation fixtures

English: isolated vowel inventory, diphthongs, P/B/M closure phrases, F/V, TH, L-versus-R, fricatives/affricates, fast and quiet connected speech, and a held-out conversation.

Japanese: あいうえお; ぱぴぷぺぽ／ばびぶべぼ／まみむめも; ふ and ファフィフェフォ; らりるれろ; し・ち・つ; きゃきゅきょ／にゃにゅにょ; contextual ん in さんぽ／さんだ／さんか; geminates in きって／きっぷ／がっこう; long-vowel pairs such as おばさん／おばあさん and おじさん／おじいさん; quiet/devoiced contexts such as です／すき; loanwords and mixed English/Japanese speech.

Test recorded or synthesized spoken audio with known text and timing, plus native-speaker visual/audio review when available. Kana strings alone do not establish audio timing. Compare front, three-quarter and profile, sample intermediate weights, and test speech with every source expression and independent blinks. Document the dialect/speaker profile used rather than claiming one exact articulation for every Japanese speaker.

## Analyzer handoff boundary

The existing HeadAudio English classifier and 15-label contract are not evidence of Japanese acoustic accuracy. The separate lip-sync implementation must supply a validated Japanese or multilingual analysis/alignment route that drives these poses with audible-time synchronization. This requirement does not authorize substituting commercial libraries. Model coverage and analyzer coverage must be reported separately.
