import tempfile
import unittest
from pathlib import Path

import main


class ResolveUploadedAudioPathTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.external_temp_dir = tempfile.TemporaryDirectory()
        self.upload_dir = Path(self.temp_dir.name)
        self.external_dir = Path(self.external_temp_dir.name)
        self.original_upload_dir = main.UPLOAD_DIR
        main.UPLOAD_DIR = str(self.upload_dir)
        self.uploaded_file = self.upload_dir / "track.mp3"
        self.uploaded_file.touch()

    def tearDown(self):
        main.UPLOAD_DIR = self.original_upload_dir
        self.temp_dir.cleanup()
        self.external_temp_dir.cleanup()

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
        self.assertIsNone(main.resolve_uploaded_audio_path("/tmp/track.mp3"))
        self.assertIsNone(main.resolve_uploaded_audio_path(r"C:\uploads\track.mp3"))
        self.assertIsNone(main.resolve_uploaded_audio_path("."))
        self.assertIsNone(main.resolve_uploaded_audio_path(".."))
        self.assertIsNone(main.resolve_uploaded_audio_path("track name.mp3"))

    def test_returns_none_for_missing_upload(self):
        self.assertIsNone(main.resolve_uploaded_audio_path("missing.mp3"))

    def test_rejects_symlink_that_escapes_upload_directory(self):
        external_file = self.external_dir / "external.mp3"
        external_file.touch()
        symlink = self.upload_dir / "linked.mp3"
        try:
            symlink.symlink_to(external_file)
        except OSError as error:
            self.skipTest(f"Symlink creation unavailable: {error}")

        self.assertIsNone(main.resolve_uploaded_audio_path("linked.mp3"))
