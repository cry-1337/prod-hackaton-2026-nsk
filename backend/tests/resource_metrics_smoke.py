import os
import re
import subprocess
import time
import unittest
from urllib.request import urlopen


BASE_URL = os.environ.get("API_BASE_URL", "http://127.0.0.1:5080").rstrip("/")


class ResourceMetricsTests(unittest.TestCase):
    def test_http_red_and_postgres_use(self):
        with urlopen(BASE_URL + "/api/catalog/products", timeout=5) as response:
            self.assertEqual(200, response.status)

        deadline = time.monotonic() + 35
        while True:
            with urlopen(BASE_URL + "/metrics", timeout=5) as response:
                metrics = response.read().decode()

            values = {key: float(value) for key, value in re.findall(
                r"^(vmeste_postgres_\w+) ([\d.e+-]+)$", metrics, re.MULTILINE)}
            if values.get("vmeste_postgres_stats_up") == 1:
                break
            self.assertLess(time.monotonic(), deadline, "PostgreSQL metrics snapshot did not become ready")
            time.sleep(0.25)

        limit = int(subprocess.check_output(["psql", "-XAtc", "SHOW max_connections"], text=True))
        self.assertEqual(limit, values["vmeste_postgres_connection_limit"])
        self.assertGreaterEqual(values["vmeste_postgres_client_connections"], 1)
        self.assertLessEqual(values["vmeste_postgres_client_connections"], limit)
        self.assertGreaterEqual(values["vmeste_postgres_lock_waiting_sessions"], 0)
        self.assertGreaterEqual(values["vmeste_postgres_deadlocks_since_reset"], 0)
        for name in ("http_requests_received_total", "http_request_duration_seconds_count"):
            samples = re.findall(r"^" + name + r"\{([^}]+)\} ([\d.e+-]+)$", metrics, re.MULTILINE)
            self.assertTrue(any('method="GET"' in labels and 'code="2xx"' in labels
                                and 'controller="Catalog"' in labels and float(value) >= 1
                                for labels, value in samples), name)
