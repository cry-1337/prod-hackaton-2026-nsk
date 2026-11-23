import math
import subprocess
import time
import unittest
from datetime import datetime, timedelta, timezone
from urllib.request import urlopen
from uuid import uuid4

from auth_smoke import BASE_URL
from group_purchase_smoke import checkout, register, request


def snapshot():
    until = time.monotonic() + 40
    after = int(time.time())
    while time.monotonic() < until:
        with urlopen(BASE_URL + "/metrics", timeout=5) as response:
            values = {}
            for line in response.read().decode().splitlines():
                if line.startswith(("vmeste_refunds", "vmeste_limit_applications",
                                    "vmeste_group_time_to_success_seconds", "vmeste_group_snapshot_last_success_timestamp_seconds ")):
                    name, value = line.split()
                    values[name] = float(value)
        if values.get("vmeste_group_snapshot_last_success_timestamp_seconds", 0) > after:
            return values
        time.sleep(0.5)
    raise AssertionError("Checkout metrics snapshot did not refresh")


class CheckoutMetricsTests(unittest.TestCase):
    def test_outcomes_refund_reasons_and_duration(self):
        before = snapshot()
        product = request("GET", "/api/catalog/products?limit=100")[1]
        coffee = next(item for item in product if item["name"] == "Кофе в зёрнах")
        owner, buyer, third = [register("Метрики " + name) for name in ("Один", "Два", "Три")]

        def create_group(seconds):
            deadline = datetime.now(timezone.utc) + timedelta(seconds=seconds)
            status, group, _ = checkout("POST", "/api/groups", {
                "productId": coffee["id"], "deadline": deadline.isoformat(), "organizerLimitKopecks": 99_000
            }, owner)
            self.assertEqual(status, 201, group)
            return "/api/groups/" + group["id"], deadline

        path, _ = create_group(3600)
        limit = path + "/limit"
        self.assertEqual(checkout("PUT", limit, {"limitKopecks": 99_000}, buyer)[0], 200)
        key = uuid4().hex
        for _ in range(2):
            self.assertEqual(checkout("PUT", limit, {"limitKopecks": 85_000}, buyer, key)[0], 200)
        revoke_key = uuid4().hex
        for _ in range(2):
            self.assertEqual(request("DELETE", limit, token=buyer, key=revoke_key)[0], 200)
        self.assertEqual(checkout("PUT", limit, {"limitKopecks": 85_000, "quantity": 4}, third)[1]["status"], "purchased")

        expired, deadline = create_group(4)
        self.assertEqual(checkout("POST", expired + "/join", token=buyer)[0], 200)
        time.sleep(max(0, (deadline - datetime.now(timezone.utc)).total_seconds()) + 0.1)
        self.assertEqual(checkout("PUT", expired + "/limit", {"limitKopecks": 99_000}, third)[0], 409)
        self.assertEqual(request("GET", expired)[1]["status"], "cancelled")
        after = snapshot()

        for reason, count, rubles in (("price_drop", 1, 140), ("limit_changed", 1, 140),
                                      ("limit_revoked", 1, 850), ("group_cancelled", 2, 1980)):
            for name, delta in (("vmeste_refunds", count), ("vmeste_refunds_rubles", rubles)):
                series = f'{name}{{reason="{reason}"}}'
                self.assertEqual(after[series] - before[series], delta, series)
        for outcome, delta in (("purchased", 2), ("revoked", 1), ("refunded", 1)):
            series = f'vmeste_limit_applications{{outcome="{outcome}"}}'
            self.assertEqual(after[series] - before[series], delta, series)

        sql = "SELECT extract(epoch FROM successful_at-created_at) FROM group_purchases WHERE status=2 AND successful_at>=created_at ORDER BY 1"
        durations = [float(value) for value in subprocess.check_output(["psql", "-XAtqc", sql], text=True).splitlines()]
        self.assertTrue(durations)
        for q in (0.5, 0.95):
            index = q * (len(durations) - 1)
            low, high = math.floor(index), math.ceil(index)
            expected = durations[low] + (durations[high] - durations[low]) * (index - low)
            self.assertAlmostEqual(after[f'vmeste_group_time_to_success_seconds{{quantile="{q}"}}'], expected, places=5)
