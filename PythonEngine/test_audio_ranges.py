import tempfile
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

import main


class AudioRangeTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.original_upload_dir = main.UPLOAD_DIR
        main.UPLOAD_DIR = self.temp_dir.name
        Path(main.UPLOAD_DIR, "track.mp3").write_bytes(bytes(range(10)))
        self.client = TestClient(main.app)

    def tearDown(self):
        self.client.close()
        main.UPLOAD_DIR = self.original_upload_dir
        self.temp_dir.cleanup()

    def test_valid_range_returns_partial_content_and_requested_slice(self):
        response = self.client.get("/audio/track.mp3", headers={"Range": "bytes=2-5"})

        self.assertEqual(response.status_code, 206)
        self.assertEqual(response.headers["content-range"], "bytes 2-5/10")
        self.assertEqual(response.headers["accept-ranges"], "bytes")
        self.assertEqual(response.headers["content-length"], "4")
        self.assertEqual(response.headers["content-type"], "audio/mpeg")
        self.assertEqual(response.content, bytes([2, 3, 4, 5]))

    def test_out_of_range_request_returns_416(self):
        response = self.client.get("/audio/track.mp3", headers={"Range": "bytes=10-"})

        self.assertEqual(response.status_code, 416)
        self.assertEqual(response.headers["content-range"], "bytes */10")

    def test_full_request_returns_all_bytes(self):
        response = self.client.get("/audio/track.mp3")

        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.headers["accept-ranges"], "bytes")
        self.assertEqual(response.headers["content-length"], "10")
        self.assertEqual(response.headers["content-type"], "audio/mpeg")
        self.assertEqual(response.content, bytes(range(10)))
