import time
import unittest

from chat_socket import chat
from group_purchase_smoke import checkout, register, request


class UpgradeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        status, profile, _ = request("POST", "/api/auth/login", {
            "email": "upgrade-member@example.test", "password": "upgrade-test-password"
        })
        if status != 200:
            raise AssertionError((status, profile))
        cls.token = profile["accessToken"]

    def test_existing_and_expiring_payments_are_refunded_once(self):
        for code in ("legacy-cancelled", "legacy-expiring"):
            with self.subTest(code=code):
                for _ in range(60):
                    status, payment, _ = request("GET", f"/api/groups/{code}/payment", token=self.token)
                    self.assertEqual(status, 200, payment)
                    if payment["refundedKopecks"]:
                        break
                    time.sleep(0.2)
                self.assertEqual((payment["paidKopecks"], payment["refundedKopecks"]), (99000, 99000))
                history = request("GET", "/api/me/payments", token=self.token)[1]["orders"]
                order, = [item for item in history if item["groupId"] == code]
                self.assertTrue(order["refundedAt"])
                notices = request("GET", "/api/me/notifications", token=self.token)[1]
                notice, = [item for item in notices if item["groupId"] == code]
                self.assertEqual((notice["kind"], notice["amountKopecks"]), ("refund", 99000))

    def test_successful_payment_and_existing_receipt_are_preserved(self):
        for code, amount in (("legacy-success", 99000), ("legacy-receipt", 85000)):
            with self.subTest(code=code):
                status, payment, _ = request("GET", f"/api/groups/{code}/payment", token=self.token)
                self.assertEqual(status, 200, payment)
                self.assertEqual((payment["paidKopecks"], payment["refundedKopecks"]), (amount, 0))
                if code == "legacy-receipt":
                    self.assertEqual(payment["deliveryAddress"], "Сохранённый адрес")

    def test_existing_direct_order_gets_its_price_difference(self):
        path = "/api/groups/legacy-price-drop/payment"
        status, receipt, _ = request("GET", path, token=self.token)
        self.assertEqual((status, receipt["paidKopecks"], receipt["refundedKopecks"]), (200, 99000, 14000))
        self.assertEqual(request("GET", path, token=self.token)[1], receipt)

    def test_membership_without_a_payment_still_grants_chat_access(self):
        with chat("/api/groups/legacy-member", self.token) as member:
            member.post("Мы на связи")
            msg = member.receive("sent")["data"]
            member.receive("message")
            self.assertIn(msg, member.history())
        self.assertEqual(request("GET", "/api/groups/legacy-member/payment", token=self.token)[0], 404)
        with chat("/api/groups/legacy-revoked", self.token) as revoked:
            self.assertIsInstance(revoked.initial, list)
            revoked.post("Здравствуйте")
            self.assertEqual(revoked.receive("error")["status"], 403)

    def test_open_cashback_group_awards_only_the_product_gift(self):
        status, profile, _ = request("POST", "/api/auth/login", {
            "email": "upgrade-organizer@example.test", "password": "upgrade-test-password"
        })
        self.assertEqual(status, 200)
        organizer = profile["accessToken"]
        last_buyer = register("Последний участник")
        path = "/api/groups/legacy-gift"
        status, group, _ = checkout("POST", path + "/join", token=last_buyer)
        self.assertEqual((status, group["status"], group["rewardKind"]), (200, "successful", "freeProduct"))
        mine = next(g for g in request("GET", "/api/me/groups", token=organizer)[1] if g["id"] == "legacy-gift")
        self.assertEqual(mine["myBonus"], {"quantity": 1, "unit": "шт."})
        self.assertEqual((mine["myPayment"]["paidKopecks"], mine["myPayment"]["cashbackKopecks"]), (99000, 0))
