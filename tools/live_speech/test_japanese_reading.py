import os
import unittest
from japanese_reading import read_text


@unittest.skipUnless(os.environ.get("OPEN_JTALK_DICT_DIR"), "Requires the pinned isolated frontend/dictionary")
class ReadingTests(unittest.TestCase):
    def reading(self, text):
        return read_text(text, os.environ["OPEN_JTALK_DICT_DIR"])

    def test_cast_readings_override_letter_spelling(self):
        result = self.reading("Ren、Maya、Luca、Theo。")
        self.assertEqual(result["reading"], "レン、マヤ、ルカ、テオ。")
        self.assertEqual(sum(t["readingSource"] == "authored_cast_alias" for t in result["tokens"]), 4)
        self.assertFalse(result["timingEstablished"])

    def test_unknown_spoken_tokens_are_not_silently_punctuation(self):
        for text in ("髙橋", "﨑田", "𠮷田", "レン😊", "Ж", "Qzxvpl", "Ren's"):
            with self.subTest(text=text), self.assertRaises(ValueError):
                self.reading(text)

    def test_particles_and_devoicing_metadata_survive(self):
        result = self.reading("レンは元気です。")
        self.assertIn("ワ", result["reading"])
        self.assertNotIn("’", result["reading"])
        self.assertTrue(any(t["devoicingAnnotations"] for t in result["tokens"]))

    def test_input_bound_before_native_frontend(self):
        for text in ("", "ア" * 1000, "㌖" * 200, "。"):
            with self.assertRaises(ValueError):
                self.reading(text)


if __name__ == "__main__":
    unittest.main()
