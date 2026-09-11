import tempfile
import unittest
from pathlib import Path

import main


class ResolveUploadedAudioPathTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.upload_dir = Path(self.temp_dir.name)
        self.original_upload_dir = main.UPLOAD_DIR
        main.UPLOAD_DIR = str(self.upload_dir)
        self.uploaded_file = self.upload_dir / "track.mp3"
        self.uploaded_file.touch()

    def tearDown(self):
        main.UPLOAD_DIR = self.original_upload_dir
        self.temp_dir.cleanup()

    def test_resolves_uploaded_basename(self):
        self.assertEqual(
            main.resolve_uploaded_audio_path("track.mp3"),
            str(self.uploaded_file.resolve()),
        )

    def test_rejects_traversal_and_directory_components(self):
        self.assertIsNone(main.resolve_uploaded_audio_path("../track.mp3"))
        self.assertIsNone(main.resolve_uploaded_audio_path("nested/track.mp3"))
        self.assertIsNone(main.resolve_uploaded_audio_path("nested\\track.mp3"))

    def test_rejects_absolute_and_invalid_references(self):
        self.assertIsNone(main.resolve_uploaded_audio_path(str(self.uploaded_file.resolve())))
        self.assertIsNone(main.resolve_uploaded_audio_path("track name.mp3"))

    def test_returns_none_for_missing_upload(self):
        self.assertIsNone(main.resolve_uploaded_audio_path("missing.mp3"))
