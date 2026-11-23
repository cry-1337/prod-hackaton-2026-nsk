import json
import os
import time
import unittest
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from decimal import Decimal
from urllib.error import HTTPError
from urllib.parse import urlencode, urlsplit
from urllib.request import Request, urlopen
from uuid import uuid4

from auth_smoke import BASE_URL, account
from chat_socket import chat


def request(method, path, body=None, token=None, key=None):
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if key:
        headers["Idempotency-Key"] = key
    data = None
    if body is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(body).encode()

    url = f"{BASE_URL}{path}" if path.startswith("/") else path
    try:
        response = urlopen(Request(url, data=data, headers=headers, method=method), timeout=15)
    except HTTPError as error:
        response = error
    with response:
        payload = response.read()
        return response.status, json.loads(payload) if payload else None, response.headers


ADDRESS = {"city": "Казань", "street": "8 Марта", "house": "12А"}
OTHER_ADDRESS = {"city": "Тула", "street": "Мира", "house": "3/1", "apartment": "20"}


def checkout(method, path, body=None, token=None, key=None):
    return request(method, path, {"address": ADDRESS, **(body or {})}, token, key)


def register(nickname, initial_balance=10_000_000):
    status, profile, _ = request("POST", "/api/auth/register", account(nickname))
    if status != 201:
        raise AssertionError((status, profile))
    token = profile["accessToken"]
    if initial_balance:
        status, top_up, _ = request("POST", "/api/me/balance/top-ups", {"amountKopecks": initial_balance}, token, uuid4().hex)
        if status != 201:
            raise AssertionError((status, top_up))
    return token


class GroupPurchaseTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        status, coffee, _ = request("GET", "/api/catalog/products?" + urlencode({"search": "Кофе в зёрнах"}))
        if status != 200 or len(coffee) != 1:
            raise AssertionError((status, coffee))
        cls.coffee = coffee[0]
        cls.organizer = register("Организатор закупки")
        cls.buyer = register("Первый покупатель")
        cls.second_buyer = register("Второй покупатель")
        cls.third_buyer = register("Третий покупатель")
        cls.fourth_buyer = register("Четвёртый покупатель")
        cls.wallet_buyer = register("Покупатель с пустым кошельком", initial_balance=0)

    def create_limit_group(self, seconds=3600):
        product = self.coffee
        deadline = datetime.now(timezone.utc) + timedelta(seconds=seconds)
        status, group, _ = checkout("POST", "/api/groups", {
            "productId": product["id"], "deadline": deadline.isoformat(), "organizerLimitKopecks": 99_000
        }, self.organizer)
        self.assertEqual(status, 201, group)
        return f"/api/groups/{group['id']}", group, deadline

    def attach_card(self, token, last_four="4242"):
        status, card, _ = request("POST", "/api/me/cards", {"lastFour": last_four}, token)
        self.assertEqual(status, 201, card)
        return card

    def order_notifications(self, token, group_id, query=""):
        status, notifications, _ = request("GET", "/api/me/notifications" + query, token=token)
        self.assertEqual(status, 200, notifications)
        return [item for item in notifications if item["groupId"] == group_id]

    def test_weighted_prepaid_limits_sum_volume_and_refund_the_price_difference(self):
        apple, = request("GET", "/api/catalog/products?" + urlencode({"search": "Яблоки Гала"}))[1]
        tiers = request("GET", "/api/catalog/products/" + apple["id"])[1]["priceTiers"]
        high, low = [t["priceKopecks"] for t in tiers]
        owner = register("Организатор весовой покупки", initial_balance=high * 2)
        buyer = self.buyer
        body = {"productId": apple["id"], "organizerLimitKopecks": high, "quantity": 2,
                "deadline": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat()}
        status, group, _ = checkout("POST", "/api/groups", body, owner)
        self.assertEqual(status, 201, group)
        path = "/api/groups/" + group["id"]
        wallet = lambda: request("GET", "/api/me/balance", token=owner)[1]
        self.assertEqual(wallet()["balanceKopecks"], 0)
        pending = request("GET", path + "/limit", token=owner)[1]
        self.assertEqual((pending["chargedKopecks"], pending["remainingQuantity"]), (high * 2, 1))
        update = {"limitKopecks": low, "quantity": 2.999}
        key = uuid4().hex
        first = checkout("PUT", path + "/limit", update, buyer, key)
        self.assertEqual((first[0], first[1]["status"], first[1]["remainingQuantity"]), (200, "pending", 0.001))
        self.assertEqual(checkout("PUT", path + "/limit", update, buyer, key)[:2], first[:2])
        self.assertEqual(checkout("PUT", path + "/limit", {**update, "quantity": 3}, buyer, key)[0], 409)
        demand = request("GET", path)[1]["priceTiers"]
        self.assertEqual([(t["waitingBuyers"], t["eligibleQuantity"]) for t in demand], [(1, 2), (1, 4.999)])
        quote = request("GET", path + "/limit/quote?limitKopecks=" + str(low) + "&quantity=3", token=buyer)[1]
        self.assertEqual(quote["reachablePriceKopecks"], low)
        self.assertEqual(checkout("PUT", path + "/limit", {**update, "quantity": 3}, buyer)[1]["status"], "purchased")
        result = request("GET", path)[1]
        self.assertEqual((result["participantsCount"], result["eligibleQuantity"], result["settlementPriceKopecks"]), (2, 5, low))
        receipt = request("GET", path + "/limit", token=owner)[1]
        self.assertEqual((receipt["chargedKopecks"], receipt["refundedKopecks"]), (low * 2, (high - low) * 2))
        self.assertEqual((wallet()["balanceKopecks"], wallet()["paidKopecks"]), ((high - low) * 2, high * 2))

    def test_piece_quantity_is_whole_and_counts_as_one_buyer(self):
        path, _, _ = self.create_limit_group()
        for quantity in (0, -1, 0.5):
            self.assertEqual(checkout("PUT", path + "/limit", {"limitKopecks": 99000, "quantity": quantity}, self.buyer)[0], 400)
        quote = request("GET", path + "/payment/quote?quantity=2", token=self.buyer)[1]
        self.assertEqual((quote["priceKopecks"], quote["totalKopecks"]), (99000, 198000))
        body = {"confirmedPriceKopecks": 99000, "address": ADDRESS, "quantity": 2}
        key = uuid4().hex
        status, result, _ = checkout("POST", path + "/payment", body, self.buyer, key)
        self.assertEqual((status, result["participantsCount"], result["myPayment"]["paidKopecks"]), (200, 2, 198000))
        self.assertEqual(checkout("POST", path + "/payment", {**body, "quantity": 3}, self.buyer, key)[0], 409)
        self.assertEqual(request("GET", path + "/payment", token=self.buyer)[1]["quantity"], 2)

    def test_wallet_uses_available_funds_at_settlement(self):
        path, group, _ = self.create_limit_group()
        payment_paths = [self.create_limit_group()[0] for _ in range(2)]
        token = self.wallet_buyer
        balance_path = "/api/me/balance"
        top_ups = balance_path + "/top-ups"
        self.assertEqual(request("GET", balance_path)[0], 401)
        self.assertEqual(request("GET", balance_path, token=token)[1]["balanceKopecks"], 0)
        self.assertEqual(request("POST", top_ups, {"amountKopecks": 99_000}, token)[0], 400)
        self.assertEqual(request("POST", top_ups, {"amountKopecks": 0}, token, uuid4().hex)[0], 400)
        status, error, _ = checkout("POST", path + "/join", token=token)
        self.assertEqual((status, error["title"]), (409, "INSUFFICIENT_BALANCE"))

        limit = path + "/limit"
        status, pending, _ = checkout("PUT", limit, {"limitKopecks": 99_000}, token)
        self.assertEqual((status, pending["title"]), (409, "INSUFFICIENT_BALANCE"))
        self.assertEqual(request("GET", path)[1]["eligibleParticipantsCount"], 1)

        key = uuid4().hex
        with ThreadPoolExecutor(max_workers=2) as pool:
            repeats = list(pool.map(lambda _: request("POST", top_ups, {"amountKopecks": 99_000}, token, key), range(2)))
        self.assertEqual(repeats[0][:2], repeats[1][:2])
        self.assertEqual(repeats[0][0], 201, repeats[0][1])
        self.assertEqual(request("POST", top_ups, {"amountKopecks": 1}, token, key)[1]["title"], "IDEMPOTENCY_KEY_REUSED")
        self.assertEqual(request("GET", balance_path, token=token)[1]["balanceKopecks"], 99_000)
        self.assertEqual(request("GET", path)[1]["eligibleParticipantsCount"], 1)

        payment = {"confirmedPriceKopecks": 99_000, "address": ADDRESS}
        with ThreadPoolExecutor(max_workers=2) as pool:
            charges = list(pool.map(lambda group_path: checkout("POST", group_path + "/payment", payment, token, uuid4().hex), payment_paths))
        self.assertEqual(sorted(result[0] for result in charges), [200, 409])
        self.assertEqual(next(result[1]["title"] for result in charges if result[0] == 409), "INSUFFICIENT_BALANCE")
        self.assertEqual(request("GET", balance_path, token=token)[1]["balanceKopecks"], 0)
        self.assertEqual(request("GET", path)[1]["eligibleParticipantsCount"], 1)

        self.assertEqual(checkout("POST", path + "/join", token=self.buyer)[1]["status"], "open")
        settled = checkout("PUT", limit, {"limitKopecks": 99_000}, self.second_buyer)[1]
        self.assertEqual(settled["status"], "purchased")
        self.assertEqual(request("GET", limit, token=token)[0], 404)
        self.assertEqual(request("GET", path)[1]["settlementPriceKopecks"], 99_000)
        self.assertEqual(request("GET", balance_path, token=token)[1], {
            "balanceKopecks": 0, "toppedUpKopecks": 99_000, "paidKopecks": 99_000,
            "refundedKopecks": 0, "cashbackKopecks": 0, "referralBonusKopecks": 0
        })

    def test_prepaid_limit_edits_withdrawals_and_direct_conversion(self):
        path, _, _ = self.create_limit_group()
        token = self.buyer
        balance = lambda: request("GET", "/api/me/balance", token=token)[1]["balanceKopecks"]
        original = balance()
        limit = path + "/limit"
        key = uuid4().hex
        body = {"limitKopecks": 85_000, "quantity": 2}
        with ThreadPoolExecutor(max_workers=2) as pool:
            replies = list(pool.map(lambda _: checkout("PUT", limit, body, token, key), range(2)))
        self.assertEqual(replies[0][:2], replies[1][:2])
        self.assertEqual((balance(), replies[0][1]["remainingQuantity"]), (original - 170_000, 2))
        self.assertEqual(checkout("PUT", limit, {**body, "quantity": 1}, token)[1]["refundedKopecks"], 85_000)
        self.assertEqual(balance(), original - 85_000)
        withdrawal = uuid4().hex
        for _ in range(2):
            result = request("DELETE", limit, token=token, key=withdrawal)[1]
            self.assertEqual((result["chargedKopecks"], result["refundedKopecks"]), (0, 170_000))
        self.assertEqual(balance(), original)
        demand = request("GET", path)[1]["priceTiers"]
        self.assertEqual([t["waitingBuyers"] for t in demand], [1, 0])
        self.assertEqual(checkout("PUT", limit, {"limitKopecks": 99_000}, token)[0], 200)
        self.assertEqual(checkout("POST", path + "/payment", {"confirmedPriceKopecks": 99_000,
            "address": ADDRESS}, token, uuid4().hex)[0], 200)
        self.assertEqual(balance(), original - 99_000)

    def test_public_roster_links_real_profiles_and_omits_withdrawn_buyers(self):
        path, group, _ = self.create_limit_group()
        roster = path + "/participants"
        owner = {"userId": group["organizerId"], "nickname": group["organizerNickname"], "unitPriceKopecks": 99_000, "isLimit": True}
        self.assertEqual(request("GET", roster)[:2], (200, [owner]))
        profile = request("GET", "/api/auth/me", token=self.buyer)[1]
        buyer = {"userId": profile["id"], "nickname": profile["nickname"], "unitPriceKopecks": 85_000, "isLimit": True}
        self.assertEqual(checkout("PUT", path + "/limit", {"limitKopecks": 85_000}, self.buyer)[0], 200)
        self.assertEqual(request("GET", roster)[1], [owner, buyer])
        self.assertEqual(request("GET", roster + "?offset=1&limit=1")[1], [buyer])
        self.assertEqual(request("GET", roster + "?limit=0")[0], 400)
        self.assertEqual(request("GET", "/api/groups/missing-code/participants")[0], 404)
        self.assertEqual(request("GET", f"/api/users/{buyer['userId']}/organizer-profile")[1]["nickname"], buyer["nickname"])
        self.assertEqual(request("DELETE", path + "/limit", token=self.buyer)[0], 200)
        self.assertEqual(request("GET", roster)[1], [owner])

    def test_ordinary_invites_work_without_roles_or_new_referral_bonuses(self):
        path, group, _ = self.create_limit_group()
        buyer_id = request("GET", "/api/auth/me", token=self.buyer)[1]["id"]
        self.assertEqual(request("POST", path + "/referral-invite", token=self.organizer)[0], 404)
        self.assertEqual(request("PUT", path + "/co-organizers/" + buyer_id, token=self.organizer)[0], 404)
        self.assertEqual(request("GET", path)[1]["inviteCode"], group["id"])
        initial = request("GET", "/api/me/balance", token=self.organizer)[1]["referralBonusKopecks"]
        self.assertEqual(checkout("POST", path + "/join?referralCode=" + str(uuid4()), token=self.buyer)[0], 200)
        self.assertEqual(checkout("POST", path + "/join", token=self.second_buyer)[1]["status"], "successful")
        self.assertEqual(request("GET", "/api/me/balance", token=self.organizer)[1]["referralBonusKopecks"], initial)
        self.assertNotIn(group["id"], [g["id"] for g in request("GET", "/api/groups?organizerId=" + buyer_id)[1]])

    def test_address_is_required_for_all_checkouts_and_private_on_read(self):
        path, group, deadline = self.create_limit_group()
        endpoints = [
            ("POST", "/api/groups", {"productId": self.coffee["id"], "deadline": deadline.isoformat(), "organizerLimitKopecks": 99_000}),
            ("PUT", path + "/limit", {"limitKopecks": 85_000}),
            ("POST", path + "/payment", {"confirmedPriceKopecks": 99_000}),
            ("POST", path + "/join", {})
        ]
        for method, url, body in endpoints:
            for change in ({}, {"deliveryAddress": "Любая строка"}, {"address": {**ADDRESS, "house": "дом"}},
                           {"address": {**ADDRESS, "street": " "}}, {"address": {**ADDRESS, "city": "123"}}):
                with self.subTest(method=method, url=url, change=change):
                    self.assertEqual(request(method, url, {**body, **change}, self.buyer)[0], 400)
        self.assertEqual(request("GET", path)[1]["participantsCount"], 1)
        body = {"limitKopecks": 85_000, "address": {**OTHER_ADDRESS, "city": "  Тула  "}}
        self.assertEqual(request("PUT", path + "/limit", body, self.buyer)[0], 200)
        mine = request("GET", path + "/limit", token=self.buyer)[1]
        self.assertEqual((mine["address"]["city"], mine["address"]["house"], mine["address"]["apartment"]), ("Тула", "3/1", "20"))
        self.assertEqual(request("PUT", path + "/limit", {**body, "address": None}, self.buyer)[0], 400)
        self.assertEqual(request("GET", path + "/limit", token=self.buyer)[1], mine)
        self.assertEqual(request("GET", path)[1]["participantsCount"], 2)
        feed = request("GET", "/api/groups?" + urlencode({"search": group["id"]}))[1]
        self.assertEqual(feed[0]["participantsCount"], 2)
        for public in (feed, request("GET", path)[1], request("GET", path + "/participants")[1]):
            self.assertNotIn("address", json.dumps(public).lower())
        self.assertEqual(checkout("POST", path + "/payment", {"confirmedPriceKopecks": 99_000}, self.buyer)[0], 200)
        roster = request("GET", path + "/participants")[1]
        self.assertEqual([(p["unitPriceKopecks"], p["isLimit"]) for p in roster], [(99_000, True), (99_000, False)])

    def test_three_buyers_pool_six_pieces_and_refund_the_higher_price(self):
        for direct in (False, True):
            with self.subTest(direct=direct):
                owner = register("Организатор", 170_000)
                higher = register("Две пачки по 990", 198_000)
                lower = register("Две пачки по 850", 170_000)
                status, group, _ = checkout("POST", "/api/groups", {
                    "productId": self.coffee["id"], "quantity": 2, "organizerLimitKopecks": 85_000,
                    "deadline": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat()
                }, owner)
                self.assertEqual(status, 201, group)
                path = "/api/groups/" + group["id"]
                if direct:
                    status, receipt, _ = checkout("POST", path + "/payment", {
                        "quantity": 2, "confirmedPriceKopecks": 99_000, "address": ADDRESS
                    }, higher, uuid4().hex)
                    self.assertEqual((status, receipt["myPayment"]["paidKopecks"]), (200, 198_000))
                else:
                    status, receipt, _ = checkout("PUT", path + "/limit", {"quantity": 2, "limitKopecks": 99_000}, higher)
                    self.assertEqual((status, receipt["chargedKopecks"]), (200, 198_000))
                self.assertEqual(request("GET", path)[1]["status"], "open")
                self.assertEqual(request("GET", "/api/me/balance", token=higher)[1]["balanceKopecks"], 0)
                key = uuid4().hex
                body = {"quantity": 2, "limitKopecks": 85_000}
                with ThreadPoolExecutor(max_workers=2) as pool:
                    confirmations = list(pool.map(lambda _: checkout("PUT", path + "/limit", body, lower, key)[:2], range(2)))
                self.assertEqual(confirmations[0], confirmations[1])
                self.assertEqual(confirmations[0][0], 200)
                result = request("GET", path)[1]
                self.assertEqual((result["status"], result["settlementPriceKopecks"], result["eligibleQuantity"],
                                  result["participantsCount"]), ("successful", 85_000, 6, 3))
                wallet = request("GET", "/api/me/balance", token=higher)[1]
                self.assertEqual((wallet["paidKopecks"], wallet["refundedKopecks"], wallet["balanceKopecks"]),
                                 (198_000, 28_000, 28_000))
                for token in (lower, owner):
                    self.assertEqual(request("GET", "/api/me/balance", token=token)[1]["balanceKopecks"], 0)
                if direct:
                    receipt = request("GET", path + "/payment", token=higher)[1]
                    self.assertEqual((receipt["paidKopecks"], receipt["refundedKopecks"]), (198_000, 28_000))
                    history = request("GET", "/api/me/payments", token=higher)[1]
                    self.assertEqual(history["netChangeKopecks"], -170_000)
                else:
                    receipt = request("GET", path + "/limit", token=higher)[1]
                    self.assertEqual((receipt["chargedKopecks"], receipt["refundedKopecks"]), (170_000, 28_000))
                self.assertEqual(self.order_notifications(higher, group["id"])[0]["amountKopecks"], 170_000)

    def test_profile_counts_match_owned_groups_after_settlement(self):
        path, group, _ = self.create_limit_group()
        owner = group["organizerId"]

        def check(state, counter):
            profile = request("GET", f"/api/users/{owner}/organizer-profile")[1]
            query = urlencode({"organizerId": owner, "status": state, "sortByOrganizerLikes": "false"})
            groups = request("GET", "/api/groups?" + query + "&limit=100")[1]
            self.assertEqual(profile[counter], len(groups))
            self.assertEqual(len(groups), len({item["id"] for item in groups}))
            self.assertEqual(request("GET", "/api/groups?" + query + "&offset=1&limit=1")[1], groups[1:2])
            return {item["id"] for item in groups}

        self.assertIn(group["id"], check("open", "openGroupsCount"))
        for token in (self.buyer, self.second_buyer):
            self.assertEqual(checkout("POST", path + "/join", token=token)[0], 200)
        self.assertIn(group["id"], check("successful", "successfulGroupsCount"))
        self.assertNotIn(group["id"], check("open", "openGroupsCount"))

    def test_group_chat_membership_paging_and_retries(self):
        path, group, _ = self.create_limit_group()
        limit = path + "/limit"
        self.assertEqual(request("GET", path + "/chat/messages")[0], 404)
        self.assertEqual(request("POST", path + "/chat/messages", {"text": "Старый маршрут"}, self.organizer)[0], 404)
        with chat(path) as guest, chat(path, self.buyer) as buyer, chat(path, self.organizer) as owner:
            self.assertEqual(guest.initial, [])
            guest.post("От гостя")
            self.assertEqual(guest.receive("error")["status"], 401)
            buyer.post("Привет")
            forbidden = buyer.receive("error")
            self.assertEqual((forbidden["status"], forbidden["title"]), (403, "GROUP_CHAT_FORBIDDEN"))
            for text in ("   ", "a" * 1001, "a\u0000b"):
                owner.post(text)
                self.assertEqual(owner.receive("error")["status"], 400)
            owner.send("history", beforeId=2, afterId=1)
            self.assertEqual(owner.receive("error")["status"], 400)

            self.assertEqual(checkout("PUT", limit, {"limitKopecks": 85_000}, self.buyer)[0], 200)
            key = uuid4().hex
            buyer.post("  Первый вопрос  ", key)
            first = buyer.receive("sent")["data"]
            self.assertEqual(first["text"], "Первый вопрос")
            for client in (guest, buyer, owner):
                self.assertEqual(client.receive("message")["data"], first)
            buyer.post("Первый вопрос", key)
            self.assertEqual(buyer.receive("sent")["data"], first)
            buyer.post("Другой вопрос", key)
            self.assertEqual(buyer.receive("error")["title"], "IDEMPOTENCY_KEY_REUSED")

            owner.post("Ответ", key)
            second = owner.receive("sent")["data"]
            self.assertEqual((second["authorId"], second["authorNickname"]), (group["organizerId"], group["organizerNickname"]))
            for client in (guest, buyer, owner):
                self.assertEqual(client.receive("message")["data"], second)
            self.assertEqual(guest.history(), [first, second])
            self.assertEqual(guest.history(limit=1), [second])
            self.assertEqual(guest.history(beforeId=second["id"]), [first])
            self.assertEqual(guest.history(afterId=first["id"]), [second])
            with chat(path, after_id=first["id"]) as reconnected:
                self.assertEqual(reconnected.initial, [second])
            other_path, _, _ = self.create_limit_group()
            with chat(other_path) as other:
                self.assertEqual(other.initial, [])

            self.assertEqual(request("DELETE", limit, token=self.buyer)[0], 200)
            self.assertEqual(buyer.history(), [first, second])
            buyer.post("Первый вопрос", key)
            self.assertEqual(buyer.receive("error")["status"], 403)
            for token in (self.buyer, self.second_buyer):
                self.assertEqual(checkout("POST", path + "/join", token=token)[0], 200)
            self.assertEqual(request("GET", path)[1]["status"], "successful")
            buyer.post("Сбор завершён")
            last = buyer.receive("sent")["data"]
            self.assertEqual(guest.receive("message")["data"], last)
            self.assertEqual(guest.history(limit=1), [last])

    def test_test_card_can_be_attached_and_removed_only_by_owner(self):
        self.assertEqual(request("GET", "/api/me/cards")[0], 401)
        self.assertEqual(request("POST", "/api/me/cards", {"lastFour": "12345"}, self.buyer)[0], 400)
        card = self.attach_card(self.buyer)
        self.assertEqual(self.attach_card(self.buyer), card)
        path = "/api/me/cards/" + card["id"]
        self.assertEqual(request("DELETE", path, token=self.third_buyer)[0], 204)
        self.assertIn(card, request("GET", "/api/me/cards", token=self.buyer)[1])
        self.assertNotIn(card, request("GET", "/api/me/cards", token=self.third_buyer)[1])
        self.assertEqual(request("DELETE", path, token=self.buyer)[0], 204)
        self.assertNotIn(card, request("GET", "/api/me/cards", token=self.buyer)[1])
        self.assertEqual(self.attach_card(self.buyer), card)

    def test_payments_preserve_prices_and_award_catalog_gift_once(self):
        product = self.coffee
        organizer_card = self.attach_card(self.organizer)["id"]
        buyer_card = self.attach_card(self.buyer)["id"]
        creation = {"productId": product["id"], "organizerLimitKopecks": 99_000, "cardId": organizer_card,
                    "address": ADDRESS, "confirmedPriceKopecks": 99_000,
                    "deadline": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat()}
        key = uuid4().hex
        status, group, _ = checkout("POST", "/api/groups", creation, self.organizer, key)
        self.assertEqual(status, 201, group)
        path = f"/api/groups/{group['id']}"
        payment_path = path + "/payment"
        self.assertEqual(checkout("POST", "/api/groups", creation, self.organizer, key)[:2], (201, group))
        self.assertEqual(request("GET", payment_path, token=self.organizer)[0], 404)
        self.assertEqual(request("GET", payment_path, token=self.buyer)[0], 404)
        quote = request("GET", payment_path + "/quote", token=self.buyer)[1]
        self.assertEqual((quote["priceKopecks"], quote["savingsKopecks"], quote["fullRefundOnCancellation"]), (99_000, 21_000, True))

        body = {"cardId": buyer_card, "address": ADDRESS, "confirmedPriceKopecks": 99_000}
        self.assertEqual(checkout("POST", payment_path, {"cardId": buyer_card, "address": ADDRESS}, self.buyer)[0], 400)
        result = checkout("POST", payment_path, {**body, "cardId": organizer_card}, self.buyer)
        self.assertEqual((result[0], result[1]["title"]), (404, "TEST_CARD_NOT_FOUND"))
        result = checkout("POST", payment_path, {**body, "confirmedPriceKopecks": 85_000}, self.buyer)
        self.assertEqual((result[0], result[1]["title"], result[1]["quote"]["priceKopecks"]), (409, "PRICE_CONFIRMATION_REQUIRED", 99_000))
        self.assertEqual(request("GET", path)[1]["participantsCount"], 1)
        payment_key = uuid4().hex
        with ThreadPoolExecutor(max_workers=2) as pool:
            replies = list(pool.map(lambda _: checkout("POST", payment_path, body, self.buyer, payment_key), range(2)))
        self.assertEqual(replies[0][:2], replies[1][:2])
        self.assertEqual(replies[0][0], 200, replies[0][1])
        self.assertEqual(replies[0][1]["myPayment"]["paidKopecks"], 99_000)
        self.assertEqual(request("GET", "/api/me/notifications")[0], 401)
        self.assertEqual(request("GET", "/api/me/notifications?limit=0", token=self.buyer)[0], 400)
        self.assertEqual(self.order_notifications(self.buyer, group["id"]), [])
        self.assertEqual(checkout("POST", payment_path, {**body, "address": OTHER_ADDRESS}, self.buyer, payment_key)[1]["title"],
                         "IDEMPOTENCY_KEY_REUSED")

        for token, limit in ((self.second_buyer, 85_000), (self.third_buyer, 85_000), (self.fourth_buyer, 99_000)):
            card = self.attach_card(token)["id"]
            status, limit_result, _ = checkout("PUT", path + "/limit", {"limitKopecks": limit, "cardId": card,
                "address": OTHER_ADDRESS}, token)
            self.assertEqual(status, 200, limit_result)
        self.assertEqual(request("GET", path)[1]["status"], "successful")
        self.assertEqual(request("GET", payment_path, token=self.buyer)[1]["paidKopecks"], 99_000)
        result = request("GET", payment_path, token=self.fourth_buyer)[1]
        self.assertEqual((result["paidKopecks"], result["address"]["apartment"], result["cardId"]), (85_000, "20", card))
        reward = request("GET", payment_path, token=self.organizer)[1]
        self.assertEqual((reward["paidKopecks"], reward["cashbackKopecks"], reward["refundedKopecks"]), (85_000, 0, 0))
        self.assertEqual(request("GET", "/api/me/balance", token=self.organizer)[1]["cashbackKopecks"], 0)
        self.assertNotIn("myPayment", request("GET", path)[1])
        history = request("GET", "/api/me/payments", token=self.organizer)[1]["orders"]
        earned = [order for order in history if order["groupId"] == group["id"]]
        self.assertEqual(len(earned), 1)
        self.assertIsNone(earned[0]["cashbackAt"])
        mine = next(item for item in request("GET", "/api/me/groups", token=self.organizer)[1] if item["id"] == group["id"])
        self.assertEqual(mine["myBonus"], {"quantity": 1, "unit": "шт."})

        buyer_notifications = self.order_notifications(self.buyer, group["id"])
        limit_notifications = self.order_notifications(self.fourth_buyer, group["id"])
        self.assertEqual((len(buyer_notifications), len(limit_notifications)), (1, 1))
        notice, = buyer_notifications
        self.assertEqual((notice["kind"], notice["amountKopecks"], notice["productId"], notice["productName"], notice["readAt"]),
                         ("purchase", 85_000, product["id"], product["name"], None))
        self.assertEqual(notice["createdAt"], limit_notifications[0]["createdAt"])
        self.assertEqual((limit_notifications[0]["kind"], limit_notifications[0]["amountKopecks"]), ("purchase", 85_000))

        read_path = f"/api/me/notifications/{notice['id']}/read"
        self.assertEqual(request("PUT", read_path)[0], 401)
        status, error, _ = request("PUT", read_path, token=self.fourth_buyer)
        self.assertEqual((status, error["title"]), (404, "NOTIFICATION_NOT_FOUND"))
        self.assertEqual(request("PUT", read_path, token=self.buyer)[0], 204)
        read_notice, = self.order_notifications(self.buyer, group["id"])
        self.assertTrue(read_notice["readAt"])
        self.assertEqual(request("PUT", read_path, token=self.buyer)[0], 204)
        self.assertEqual(self.order_notifications(self.buyer, group["id"]), [read_notice])
        self.assertEqual(self.order_notifications(self.buyer, group["id"], "?unreadOnly=true"), [])

    def test_direct_payment_refunds_on_deadline_without_awarding_gift(self):
        balance_path = "/api/me/balance"
        before = request("GET", balance_path, token=self.buyer)[1]
        deadline = datetime.now(timezone.utc) + timedelta(seconds=3)
        status, group, _ = checkout("POST", "/api/groups", {"productId": self.coffee["id"], "organizerLimitKopecks": 85_000,
            "deadline": deadline.isoformat()}, self.organizer)
        self.assertEqual(status, 201, group)
        path = f"/api/groups/{group['id']}"
        self.assertEqual(checkout("POST", path + "/join", token=self.buyer)[0], 200)
        self.assertEqual(request("GET", balance_path, token=self.buyer)[1]["balanceKopecks"], before["balanceKopecks"] - 99_000)
        self.assertEqual(checkout("PUT", path + "/limit", {"limitKopecks": 85_000}, self.second_buyer)[0], 200)
        time.sleep(max(0, (deadline - datetime.now(timezone.utc)).total_seconds()))
        for _ in range(50):
            refund = request("GET", path + "/payment", token=self.buyer)[1]
            if refund["refundedKopecks"]:
                break
            time.sleep(0.2)
        self.assertEqual((refund["paidKopecks"], refund["refundedKopecks"]), (99_000, 99_000))
        notice, = self.order_notifications(self.buyer, group["id"], "?unreadOnly=true")
        self.assertEqual((notice["kind"], notice["amountKopecks"]), ("refund", 99_000))
        self.assertEqual(self.order_notifications(self.second_buyer, group["id"]), [])
        self.assertEqual(request("GET", path)[1]["status"], "cancelled")
        after = request("GET", balance_path, token=self.buyer)[1]
        self.assertEqual(after["balanceKopecks"], before["balanceKopecks"])
        self.assertEqual(after["refundedKopecks"] - before["refundedKopecks"], 99_000)
        self.assertEqual(request("GET", path + "/payment", token=self.organizer)[0], 404)
        mine = next(item for item in request("GET", "/api/me/groups", token=self.organizer)[1] if item["id"] == group["id"])
        self.assertEqual(mine["status"], "cancelled")
        self.assertNotIn("myBonus", mine)

    def test_invited_buyer_can_pay_chat_and_find_the_completed_order(self):
        path, group, _ = self.create_limit_group()
        status, invitation, _ = request("GET", f"/api/groups/invite/{group['inviteCode']}")
        self.assertEqual((status, invitation["id"]), (200, group["id"]))
        quote = request("GET", path + "/payment/quote", token=self.buyer)[1]
        card = self.attach_card(self.buyer)["id"]
        status, joined, _ = checkout("POST", path + "/payment", {
            "confirmedPriceKopecks": quote["priceKopecks"], "cardId": card, "address": ADDRESS
        }, self.buyer)
        self.assertEqual((status, joined["participantsCount"]), (200, 2))
        with chat(path, self.buyer) as sender, chat(path, self.organizer) as reader:
            sender.post("До встречи")
            message = sender.receive("sent")["data"]
            self.assertEqual(reader.receive("message")["data"], message)
        self.assertEqual(checkout("POST", path + "/join", token=self.second_buyer)[1]["status"], "successful")
        mine = next(item for item in request("GET", "/api/me/groups", token=self.buyer)[1] if item["id"] == group["id"])
        self.assertEqual((mine["status"], mine["myPayment"]["paidKopecks"]), ("successful", quote["priceKopecks"]))
        self.assertEqual(self.order_notifications(self.buyer, group["id"])[0]["kind"], "purchase")

    def test_creation_retry_uses_original_address_and_card(self):
        first = self.attach_card(self.organizer, "3001")["id"]
        second = self.attach_card(self.organizer, "3002")["id"]
        key = uuid4().hex
        body = {"productId": self.coffee["id"], "deadline": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(),
                "organizerLimitKopecks": 99_000, "cardId": first, "address": ADDRESS}
        status, group, _ = checkout("POST", "/api/groups", body, self.organizer, key)
        self.assertEqual(status, 201, group)
        changed = {"limitKopecks": 85_000, "cardId": second, "address": OTHER_ADDRESS}
        self.assertEqual(checkout("PUT", f"/api/groups/{group['id']}/limit", changed, self.organizer)[0], 200)
        self.assertEqual(checkout("POST", "/api/groups", body, self.organizer, key)[:2], (201, group))
        for change in ({"cardId": second}, {"address": OTHER_ADDRESS}):
            status, error, _ = checkout("POST", "/api/groups", {**body, **change}, self.organizer, key)
            self.assertEqual((status, error["title"]), (409, "IDEMPOTENCY_KEY_REUSED"))

    def test_organizer_likes_are_idempotent_and_rank_open_groups(self):
        search = "/api/catalog/products?" + urlencode({"search": "КОФЕ", "sortByOrganizerLikes": "true"})
        coffee, = request("GET", search)[1]
        now = datetime.now(timezone.utc)
        groups = []
        for token, hours in ((self.second_buyer, 1), (self.third_buyer, 2), (self.third_buyer, 3)):
            status, group, _ = checkout("POST", "/api/groups", {
                "productId": coffee["id"], "deadline": (now + timedelta(hours=hours)).isoformat(), "organizerLimitKopecks": 99_000
            }, token)
            self.assertEqual(status, 201, group)
            groups.append(group)

        profile_path = f"/api/users/{groups[1]['organizerId']}/organizer-profile"
        like_path = profile_path + "/like"
        self.assertEqual(request("PUT", like_path)[0], 401)
        self.assertEqual(request("PUT", like_path, token=self.third_buyer)[1]["title"], "CANNOT_LIKE_SELF")
        baseline = request("GET", profile_path)[1]["likesCount"]

        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(lambda _: request("PUT", like_path, token=self.fourth_buyer), range(2)))
        for status, profile, _ in results:
            self.assertEqual((status, profile["likesCount"], profile["likedByMe"]), (200, baseline + 1, True))
        self.assertTrue(request("GET", profile_path, token=self.fourth_buyer)[1]["likedByMe"])
        self.assertFalse(request("GET", profile_path)[1]["likedByMe"])
        self.assertFalse(request("GET", profile_path, token=self.buyer)[1]["likedByMe"])

        ids = [group["id"] for group in groups]
        product_path = f"/api/catalog/products/{coffee['id']}"
        product = request("GET", product_path)[1]
        self.assertEqual([item["id"] for item in product["openGroups"] if item["id"] in ids], [ids[1], ids[2], ids[0]])
        self.assertEqual([item["organizerLikesCount"] for item in product["openGroups"] if item["id"] in ids],
                         [baseline + 1, baseline + 1, 0])
        ranked = request("GET", "/api/groups?status=open&limit=100")[1]
        self.assertEqual([item["id"] for item in ranked if item["id"] in ids], [ids[1], ids[2], ids[0]])
        newest = request("GET", "/api/groups?status=open&sortByOrganizerLikes=false&limit=100")[1]
        self.assertEqual([item["id"] for item in newest if item["id"] in ids], list(reversed(ids)))
        self.assertEqual(request("GET", search)[1][0]["topOrganizerLikesCount"], baseline + 1)

        for _ in range(2):
            status, profile, _ = request("DELETE", like_path, token=self.fourth_buyer)
            self.assertEqual((status, profile["likesCount"], profile["likedByMe"]), (200, baseline, False))
        product = request("GET", product_path)[1]
        self.assertEqual([item["id"] for item in product["openGroups"] if item["id"] in ids], ids)

    def test_buy_up_to_uses_cheapest_reachable_tier_and_replays_confirmation(self):
        path, group, _ = self.create_limit_group()
        limit_path = path + "/limit"
        self.assertEqual(checkout("POST", path + "/join", token=self.buyer)[0], 200)
        for token in (self.second_buyer, self.third_buyer):
            status, pending, _ = checkout("PUT", limit_path, {"limitKopecks": 85_000}, token)
            self.assertEqual((status, pending["status"], pending["chargedKopecks"]), (200, "pending", 85_000))

        for invalid in (80000, 90000, 120000):
            for reply in (request("GET", limit_path + "/quote?limitKopecks=" + str(invalid), token=self.fourth_buyer),
                          checkout("PUT", limit_path, {"limitKopecks": invalid}, self.fourth_buyer)):
                self.assertEqual(reply[0], 400, reply[1])
                self.assertIn("PRICE_TIER_UNAVAILABLE", reply[1]["errors"]["LimitKopecks"])
        status, quote, _ = request("GET", limit_path + "/quote?limitKopecks=99000", token=self.fourth_buyer)
        self.assertEqual(quote["refundNowKopecks"], 14_000)
        self.assertEqual((status, quote["groupId"], quote["chargeNowKopecks"], quote["otherLimitsPurchased"]),
                         (200, group["id"], 99_000, 3))
        self.assertEqual(request("GET", path)[1]["participantsCount"], 4)

        key = uuid4().hex
        with ThreadPoolExecutor(max_workers=2) as pool:
            retries = list(pool.map(lambda _: checkout("PUT", limit_path, {"limitKopecks": 99_000}, self.fourth_buyer, key), range(2)))
        for status, purchased, _ in retries:
            self.assertEqual((status, purchased["status"], purchased["chargedKopecks"]), (200, "purchased", 85_000))
        self.assertEqual(retries[0][1], retries[1][1])
        self.assertEqual(checkout("PUT", limit_path, {"limitKopecks": 91_000}, self.fourth_buyer, key)[1]["title"],
                         "IDEMPOTENCY_KEY_REUSED")
        _, closed, _ = request("GET", path)
        self.assertEqual((closed["status"], closed["settlementPriceKopecks"], closed["purchasedLimitsCount"]),
                         ("successful", 85_000, 4))
        orders = []
        for token in (self.organizer, self.second_buyer, self.third_buyer, self.fourth_buyer):
            status, result, _ = request("GET", limit_path, token=token)
            self.assertEqual((status, result["status"], result["chargedKopecks"]), (200, "purchased", 85_000))
            orders.append(result["orderId"])
            history = request("GET", "/api/me/groups", token=token)[1]
            self.assertEqual(next(item for item in history if item["id"] == group["id"])["myLimit"], result)
        self.assertEqual(len(set(orders)), 4)
        self.assertTrue(all(orders))
        self.assertNotIn("myLimit", closed)

    def test_buy_up_to_updates_and_revokes_before_direct_join(self):
        path, _, _ = self.create_limit_group()
        limit_path = path + "/limit"
        key = uuid4().hex
        initial = checkout("PUT", limit_path, {"limitKopecks": 99_000}, self.buyer, key)
        self.assertEqual(initial[0], 200)
        self.assertEqual(checkout("PUT", limit_path, {"limitKopecks": 85_000}, self.buyer)[1]["limitKopecks"], 85_000)
        self.assertEqual(checkout("PUT", limit_path, {"limitKopecks": 99_000}, self.buyer, key)[:2], initial[:2])
        self.assertEqual(request("GET", limit_path, token=self.buyer)[1]["limitKopecks"], 85_000)
        self.assertEqual(request("DELETE", limit_path, token=self.buyer)[1]["status"], "revoked")
        self.assertEqual(checkout("PUT", limit_path, {"limitKopecks": 99_000}, self.second_buyer)[0], 200)
        self.assertEqual(checkout("PUT", limit_path, {"limitKopecks": 85_000}, self.fourth_buyer)[0], 200)
        status, closed, _ = checkout("POST", path + "/join", token=self.third_buyer)
        self.assertEqual((status, closed["status"], closed["participantsCount"], closed["purchasedLimitsCount"]),
                         (200, "successful", 4, 2))
        self.assertEqual(request("GET", limit_path, token=self.buyer)[1]["chargedKopecks"], 0)
        self.assertEqual(request("GET", limit_path, token=self.second_buyer)[1]["chargedKopecks"], 99_000)
        result = request("GET", limit_path, token=self.fourth_buyer)[1]
        self.assertEqual((result["status"], result["chargedKopecks"], result["orderId"]), ("closed", 0, None))

    def test_buy_up_to_closes_with_zero_charge_after_deadline(self):
        path, _, deadline = self.create_limit_group(seconds=10)
        limit_path = path + "/limit"
        self.assertEqual(checkout("PUT", limit_path, {"limitKopecks": 85_000}, self.buyer)[0], 200)
        time.sleep(max(0, (deadline - datetime.now(timezone.utc)).total_seconds()) + 0.2)
        status, result, _ = checkout("PUT", limit_path, {"limitKopecks": 99_000}, self.second_buyer)
        self.assertEqual((status, result["title"]), (409, "GROUP_CLOSED"))
        self.assertEqual(request("GET", limit_path, token=self.buyer)[:2], (200, {
            "limitKopecks": 85_000, "status": "closed", "chargedKopecks": 0, "orderId": None, "quantity": 1,
            "remainingQuantity": None, "refundedKopecks": 85_000,
            "address": {**ADDRESS, "apartment": None, "building": None}, "deliveryAddress": "Казань, 8 Марта, д. 12А"
        }))

    def test_create_and_open_invitation_in_another_session(self):
        organizer = register("Создатель приглашения")
        visitor = self.fourth_buyer
        search_path = "/api/catalog/products?" + urlencode({"search": "КОФЕ"})
        status, products, _ = request("GET", search_path)
        self.assertEqual(status, 200, products)
        coffee = [product for product in products if product["name"] == "Кофе в зёрнах"]
        self.assertEqual(len(coffee), 1)
        product = coffee[0]
        self.assertEqual(product["regularPriceKopecks"], 120_000)
        self.assertNotIn("regularPriceCents", product)
        self.assertEqual(product["startingTier"], {"requiredParticipants": 3, "requiredQuantity": 3, "priceKopecks": 99_000})
        self.assertNotIn("imageKey", product)

        product_path = f"/api/catalog/products/{product['id']}"
        status, details, _ = request("GET", product_path)
        self.assertEqual(status, 200, details)
        self.assertEqual(details["imageUrl"], product["imageUrl"])
        self.assertEqual(details["priceTiers"], [
            {"requiredParticipants": 3, "requiredQuantity": 3, "priceKopecks": 99_000},
            {"requiredParticipants": 5, "requiredQuantity": 5, "priceKopecks": 85_000},
        ])

        deadline = datetime.now(timezone.utc) + timedelta(hours=1)
        body = {"productId": product["id"], "deadline": deadline.isoformat(), "organizerLimitKopecks": 99_000}
        key = uuid4().hex
        self.assertEqual(checkout("POST", "/api/groups", body)[0], 401)
        with ThreadPoolExecutor(max_workers=2) as pool:
            attempts = [pool.submit(checkout, "POST", "/api/groups", body, organizer, key) for _ in range(2)]
            created, repeated = [attempt.result() for attempt in attempts]
        status, group, headers = created
        self.assertEqual(status, 201, group)
        self.assertEqual(repeated[:2], (201, group))
        self.assertEqual(group["status"], "open")
        self.assertEqual(group["participantsCount"], 1)
        self.assertEqual(group["priceTiers"], [
            {**tier, "waitingBuyers": int(tier["priceKopecks"] == 99_000), "eligibleQuantity": 1}
            for tier in details["priceTiers"]
        ])
        self.assertEqual(group["rewardKind"], "freeProduct")
        self.assertEqual(details["rewardOptions"], ["freeProduct"])
        self.assertEqual(details["organizerBonus"], {"quantity": 1, "unit": "шт."})
        self.assertEqual(group["imageUrl"], product["imageUrl"])
        group_id = group["id"]
        self.assertRegex(group_id, r"^[2-9a-hjkmnp-z]{8}$")
        self.assertEqual(group["inviteCode"], group_id)
        self.assertEqual(urlsplit(headers["Location"]).path, f"/api/groups/{group_id}")
        self.assertEqual(request("GET", headers["Location"])[:2], (200, group))
        self.assertEqual(request("GET", f"/api/groups/{group_id.upper()}")[:2], (200, group))
        self.assertEqual(group["regularPriceKopecks"], product["regularPriceKopecks"])
        self.assertIn(group, request("GET", "/api/groups?status=open")[1])

        invite = group["inviteUrl"]
        template = os.environ.get("INVITATION_URL_TEMPLATE")
        if template:
            self.assertEqual(invite, template.replace("{code}", group_id))
        elif urlsplit(invite).netloc == urlsplit(BASE_URL).netloc:
            self.assertEqual(urlsplit(invite).path, f"/api/groups/{group_id}")
            self.assertEqual(request("GET", invite, token=visitor)[:2], (200, group))
        self.assertEqual(request("GET", f"/invite/{group_id}")[:2], (200, group))
        code_path = f"/api/groups/invite/{group['inviteCode'].upper()}"
        self.assertEqual(request("GET", code_path, token=visitor)[:2], (200, group))
        self.assertEqual(request("GET", code_path)[:2], (200, group))

        personal = {**group, "myLimit": request("GET", f"/api/groups/{group_id}/limit", token=organizer)[1]}
        self.assertIn(personal, request("GET", "/api/account/organizing", token=organizer)[1])
        for account_path in ("/api/account/organizing", "/api/account/participating", "/api/me/groups"):
            status, groups, _ = request("GET", account_path, token=visitor)
            self.assertEqual(status, 200, groups)
            self.assertFalse(any(item["id"] == group_id for item in groups))
        self.assertEqual(request("GET", "/api/account/organizing")[0], 401)
        self.assertEqual(request("GET", "/api/me/groups")[0], 401)
        self.assertEqual(request("GET", "/api/me/groups", token=organizer)[:2], (200, [{**personal, "myRole": "organizer"}]))
        self.assertTrue(any(item["id"] == group["id"] for item in request("GET", product_path)[1]["openGroups"]))

    def test_create_rejects_invalid_deadline_and_reused_key(self):
        organizer = self.organizer
        product_id = self.coffee["id"]
        now = datetime.now(timezone.utc)
        body = {"productId": product_id, "deadline": now.isoformat(), "organizerLimitKopecks": 99_000}
        status, result, _ = checkout("POST", "/api/groups", body, organizer)
        self.assertEqual(status, 400, result)
        self.assertIn("DEADLINE_MUST_BE_IN_FUTURE", result["errors"]["Deadline"])

        body["deadline"] = (now + timedelta(hours=1)).isoformat()
        key = uuid4().hex
        self.assertEqual(checkout("POST", "/api/groups", body, organizer, key)[0], 201)
        status, result, _ = checkout("POST", "/api/groups", {**body, "organizerLimitKopecks": 85_000}, organizer, key)
        self.assertEqual(status, 409, result)
        self.assertEqual(result["title"], "IDEMPOTENCY_KEY_REUSED")

    def test_expired_group_stays_available_by_invitation(self):
        def refund_metrics():
            with urlopen(f"{BASE_URL}/metrics", timeout=5) as response:
                samples = dict(line.split() for line in response.read().decode().splitlines()
                               if line.startswith(("vmeste_test_refunds ", "vmeste_test_refunds_rubles ")))
            return int(samples["vmeste_test_refunds"]), Decimal(samples["vmeste_test_refunds_rubles"])

        initial_count, initial_rubles = refund_metrics()
        organizer = self.organizer
        product_id = self.coffee["id"]
        deadline = datetime.now(timezone.utc) + timedelta(seconds=3)
        body = {"productId": product_id, "deadline": deadline.isoformat(), "organizerLimitKopecks": 99_000}
        status, group, _ = checkout("POST", "/api/groups", body, organizer)
        self.assertEqual(status, 201, group)
        self.assertEqual(group["status"], "open")
        self.assertEqual(checkout("POST", f"/api/groups/{group['id']}/join", token=self.buyer)[0], 200)

        time.sleep(max(0, (deadline - datetime.now(timezone.utc)).total_seconds()) + 0.2)
        status, problem, _ = checkout("POST", f"/api/groups/{group['id']}/join", token=self.second_buyer)
        self.assertEqual((status, problem["title"]), (409, "GROUP_CLOSED"))

        code_path = f"/api/groups/invite/{group['inviteCode']}"
        status, closed, _ = request("GET", code_path)
        self.assertEqual(status, 200, closed)
        self.assertEqual(closed["status"], "cancelled")
        self.assertEqual(closed["participantsCount"], 2)
        self.assertIn(closed, request("GET", "/api/groups?status=cancelled")[1])
        self.assertIn(closed, request("GET", "/api/groups")[1])
        self.assertNotIn(group["id"], [g["id"] for g in request("GET", "/api/groups?status=open")[1]])

        product_path = f"/api/catalog/products/{product_id}"
        self.assertFalse(any(item["id"] == group["id"] for item in request("GET", product_path)[1]["openGroups"]))
        history = next(item for item in request("GET", "/api/account/organizing", token=organizer)[1] if item["id"] == closed["id"])
        self.assertEqual(history["myLimit"]["status"], "closed")
        self.assertNotIn("myBonus", history)
        self.assertEqual(request("GET", f"/api/groups/{group['id']}")[:2], (200, closed))

        paid = next(item for item in request("GET", "/api/me/groups", token=self.buyer)[1] if item["id"] == group["id"])
        expected = initial_count + 1, initial_rubles + Decimal(paid["myDirectPaidKopecks"]) / 100
        until = time.monotonic() + 35
        observed = refund_metrics()
        while (observed[0] < expected[0] or observed[1] < expected[1]) and time.monotonic() < until:
            time.sleep(0.5)
            observed = refund_metrics()
        self.assertGreaterEqual(observed[0], expected[0])
        self.assertGreaterEqual(observed[1], expected[1])

    def test_feeds_show_groups_and_roles_for_different_organizers(self):
        product = self.coffee
        body = {"productId": product["id"], "deadline": (datetime.now(timezone.utc) + timedelta(days=1)).isoformat(), "organizerLimitKopecks": 99_000}
        ids = []
        owners = []
        for token in (self.second_buyer, self.third_buyer):
            status, group, _ = checkout("POST", "/api/groups", body, token)
            self.assertEqual(status, 201)
            ids.append(group["id"])
            owners.append(group["organizerId"])

        for offset, expected in enumerate(reversed(ids)):
            status, page, _ = request("GET", f"/api/groups?sortByOrganizerLikes=false&limit=1&offset={offset}")
            self.assertEqual(status, 200)
            self.assertEqual([item["id"] for item in page], [expected])
            self.assertNotIn("myRole", page[0])
        self.assertEqual(request("GET", "/api/groups?offset=2147483647")[:2], (200, []))

        for owner, expected in zip(owners, ids):
            status, page, _ = request("GET", f"/api/groups?organizerId={owner}&status=open&sortByOrganizerLikes=false&limit=1")
            self.assertEqual(status, 200)
            self.assertEqual([(item["id"], item["organizerId"]) for item in page], [(expected, owner)])

        self.assertEqual(request("GET", f"/api/groups?organizerId={owners[0]}&search={ids[1]}")[:2], (200, []))
        self.assertEqual(checkout("POST", f"/api/groups/{ids[0]}/join", token=self.third_buyer)[0], 200)
        history = request("GET", "/api/me/groups", token=self.third_buyer)[1]
        roles = {item["id"]: item["myRole"] for item in history}
        self.assertEqual(roles[ids[0]], "participant")
        self.assertEqual(roles[ids[1]], "organizer")

    def test_join_counts_once_and_completes_at_first_threshold(self):
        product = self.coffee
        body = {
            "productId": product["id"],
            "deadline": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(),
            "organizerLimitKopecks": 99_000
        }
        status, created, _ = checkout("POST", "/api/groups", body, self.organizer)
        self.assertEqual(status, 201, created)

        group_id = created["id"]
        group_path = f"/api/groups/{group_id}"
        join_path = f"/api/groups/{group_id.upper()}/join"
        self.assertEqual(checkout("POST", join_path)[0], 401)
        status, problem, _ = checkout("POST", "/api/groups/missing-group/join", token=self.buyer)
        self.assertEqual((status, problem["title"]), (404, "GROUP_NOT_FOUND"))
        self.assertEqual(created["pendingLimitsCount"], 1)

        with ThreadPoolExecutor(max_workers=2) as pool:
            repeats = list(pool.map(lambda token: checkout("POST", join_path, token=token), [self.buyer, self.buyer]))

        joined = {**created, "participantsCount": 2, "directPaymentsCount": 1, "eligibleParticipantsCount": 2, "eligibleQuantity": 2}
        joined["priceTiers"] = [{**tier, "eligibleQuantity": 2} for tier in created["priceTiers"]]
        for result in repeats:
            self.assertEqual(result[:2], (200, joined))
        history = request("GET", "/api/me/groups", token=self.buyer)[1]
        payment = request("GET", group_path + "/payment", token=self.buyer)[1]
        self.assertEqual([item for item in history if item["id"] == group_id],
                         [{**joined, "myRole": "participant", "myDirectPaidKopecks": 99_000, "myPayment": payment}])
        self.assertIn({**joined, "myDirectPaidKopecks": 99_000}, request("GET", "/api/account/participating", token=self.buyer)[1])

        contenders = [self.second_buyer, self.third_buyer]
        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(lambda token: checkout("POST", join_path, token=token), contenders))

        self.assertEqual(sorted(result[0] for result in results), [200, 409])
        successful = {**joined, "participantsCount": 3, "eligibleParticipantsCount": 3, "eligibleQuantity": 3, "directPaymentsCount": 2,
                      "purchasedLimitsCount": 1, "settlementPriceKopecks": 99_000, "status": "successful"}
        successful["priceTiers"] = [{k: v for k, v in tier.items() if k not in ("eligibleQuantity", "waitingBuyers")}
                                    for tier in joined["priceTiers"]]
        successful.pop("pendingLimitsCount")
        for token, (status, result, _) in zip(contenders, results):
            history = request("GET", "/api/me/groups", token=token)[1]
            entries = [item for item in history if item["id"] == group_id]
            if status == 200:
                self.assertEqual(result, successful)
                payment = request("GET", group_path + "/payment", token=token)[1]
                self.assertEqual(entries, [{**successful, "myRole": "participant",
                                            "myDirectPaidKopecks": 99_000, "myPayment": payment}])
                self.assertEqual(checkout("POST", join_path, token=token)[:2], (200, successful))
            else:
                self.assertEqual(result["title"], "GROUP_CLOSED")
                self.assertEqual(entries, [])

        self.assertEqual(request("GET", group_path)[:2], (200, successful))
        self.assertIn(successful, request("GET", "/api/groups?status=successful")[1])
        self.assertIn({**successful, "myDirectPaidKopecks": 99_000}, request("GET", "/api/account/participating", token=self.buyer)[1])
        organizer = next(item for item in request("GET", "/api/me/groups", token=self.organizer)[1] if item["id"] == group_id)
        self.assertEqual((organizer["myRole"], organizer["myLimit"]["chargedKopecks"]), ("organizer", 99_000))
        self.assertEqual(organizer["myBonus"], {"quantity": 1, "unit": "шт."})
        product_details = request("GET", f"/api/catalog/products/{product['id']}")[1]
        self.assertNotIn(group_id, [group["id"] for group in product_details["openGroups"]])

    def test_organizer_cap_is_personal_and_gift_is_separate(self):
        product = self.coffee
        body = {"productId": product["id"], "deadline": (datetime.now(timezone.utc) + timedelta(hours=8)).isoformat()}
        self.assertEqual(checkout("POST", "/api/groups", body, self.organizer)[0], 400)
        self.assertEqual(checkout("POST", "/api/groups", {**body, "targetParticipants": 4}, self.organizer)[0], 400)
        for reward in ("cashback", "freeProduct"):
            self.assertEqual(checkout("POST", "/api/groups", {**body, "rewardKind": reward}, self.organizer)[0], 400)
        body["organizerLimitKopecks"] = 85_000
        self.assertEqual(checkout("POST", "/api/groups", {**body, "rewardKind": "cashback"}, self.organizer)[0], 400)
        key = uuid4().hex
        status, group, _ = checkout("POST", "/api/groups", body, self.organizer, key)
        self.assertEqual(status, 201, group)
        self.assertEqual(len(group["priceTiers"]), 2)
        path = f"/api/groups/{group['id']}"
        self.assertEqual(request("GET", path + "/limit", token=self.organizer)[1]["limitKopecks"], 85_000)
        for buyer in (self.buyer, self.second_buyer, self.third_buyer):
            status, result, _ = checkout("PUT", path + "/limit", {"limitKopecks": 99_000}, buyer)
            self.assertEqual(status, 200, result)
        closed = request("GET", path)[1]
        self.assertEqual((closed["status"], closed["participantsCount"], closed["eligibleParticipantsCount"],
                          closed["settlementPriceKopecks"]), ("successful", 4, 3, 99_000))
        self.assertNotIn("myBonus", closed)
        mine = next(item for item in request("GET", "/api/me/groups", token=self.organizer)[1] if item["id"] == group["id"])
        self.assertEqual((mine["myLimit"]["status"], mine["myLimit"]["chargedKopecks"]), ("closed", 0))
        self.assertEqual(mine["myBonus"], {"quantity": 1, "unit": "шт."})
        self.assertEqual(self.order_notifications(self.organizer, group["id"]), [])
        self.assertEqual(checkout("POST", "/api/groups", body, self.organizer, key)[:2], (201, group))
        self.assertEqual(checkout("POST", "/api/groups", {**body, "organizerLimitKopecks": 99_000}, self.organizer, key)[0], 409)

    def test_seed_catalog_prices_use_piece_and_kilogram(self):
        status, cards, _ = request("GET", "/api/catalog/products")
        self.assertEqual(status, 200)
        by_name = {card["name"]: card for card in cards}
        cases = [
            ("Яблоки Гала", "kilogram", 18_900, 15_900, 13_900, {"quantity": 5, "unit": "кг"}),
            ("Груши Конференция", "kilogram", 26_900, 21_900, 18_900, {"quantity": 2, "unit": "кг"}),
            ("Мёд цветочный", "piece", 59_000, 49_000, 42_000, {"quantity": 1, "unit": "шт."}),
        ]
        self.assertEqual(self.coffee["saleUnit"], "piece")
        for name, unit, regular, first, lowest, gift in cases:
            with self.subTest(product=name):
                card = by_name[name]
                self.assertEqual((card["saleUnit"], card["regularPriceKopecks"]), (unit, regular))
                status, product, _ = request("GET", f"/api/catalog/products/{card['id']}")
                self.assertEqual((status, product["saleUnit"], product["organizerBonus"]), (200, unit, gift))
                self.assertEqual(product["priceTiers"], [
                    {"requiredParticipants": 3, "requiredQuantity": 3, "priceKopecks": first}, {"requiredParticipants": 5, "requiredQuantity": 5, "priceKopecks": lowest}
                ])
                status, group, _ = checkout("POST", "/api/groups", {
                    "productId": card["id"], "organizerLimitKopecks": first,
                    "deadline": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat()
                }, self.organizer)
                self.assertEqual((status, group["saleUnit"]), (201, unit))
                self.assertEqual(request("GET", f"/api/groups/{group['id']}")[1]["saleUnit"], unit)

    def test_feed_search_price_deadline_and_pagination(self):
        path, group, _ = self.create_limit_group()
        filters = {"status": "open", "search": group["id"].upper(), "maxPriceKopecks": 85_000,
                   "endsWithinHours": 24, "sortByOrganizerLikes": "true", "limit": 1}
        def feed(**overrides):
            return request("GET", "/api/groups?" + urlencode({**filters, **overrides}))[:2]
        self.assertEqual(feed(), (200, [group]))
        self.assertEqual(feed(offset=1), (200, []))
        self.assertEqual(feed(maxPriceKopecks=84_999), (200, []))
        self.assertEqual(feed(search="несуществующий товар"), (200, []))
        for term in ("КОФЕ", group["organizerNickname"].upper()):
            status, items = feed(search=term, limit=100)
            self.assertEqual(status, 200, items)
            self.assertIn(group["id"], [item["id"] for item in items])

    def test_problem_titles(self):
        cases = [
            ("/api/groups?limit=0", 400, "VALIDATION_FAILED"),
            ("/api/groups?limit=101", 400, "VALIDATION_FAILED"),
            ("/api/groups?offset=-1", 400, "VALIDATION_FAILED"),
            ("/api/groups?status=invalid", 400, "VALIDATION_FAILED"),
            ("/api/groups?organizerId=invalid", 400, "VALIDATION_FAILED"),
            ("/api/me/groups", 401, "UNAUTHORIZED"),
            ("/api/groups/missing-group", 404, "GROUP_NOT_FOUND"),
            (f"/api/catalog/products/{uuid4()}", 404, "PRODUCT_NOT_FOUND"),
            ("/api/catalog/products/invalid", 400, "VALIDATION_FAILED"),
        ]
        for path, expected_status, expected_title in cases:
            with self.subTest(path=path):
                status, problem, headers = request("GET", path)
                self.assertEqual(headers.get_content_type(), "application/problem+json")
                self.assertEqual((status, problem["status"], problem["title"]), (expected_status, expected_status, expected_title))


if __name__ == "__main__":
    unittest.main()
