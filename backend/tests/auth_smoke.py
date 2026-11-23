import json
import os
import secrets
import unittest
import uuid
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from urllib.error import HTTPError
from urllib.request import Request, urlopen


BASE_URL = os.environ.get("API_BASE_URL", "http://127.0.0.1:5080").rstrip("/")


def request(method, path, body=None, token=None):
    headers = {"Accept": "application/json"}

    if token is not None:
        headers["Authorization"] = f"Bearer {token}"

    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"

    req = Request(f"{BASE_URL}/api/auth/{path}", data=data, headers=headers, method=method)

    try:
        response = urlopen(req, timeout=15)
    except HTTPError as error:
        response = error

    with response:
        payload = response.read()
        return response.status, json.loads(payload) if payload else None


def account(nickname):
    return {
        "email": f"{uuid.uuid4().hex}@example.test",
        "nickname": nickname,
        "password": secrets.token_urlsafe(24),
    }


class AuthTests(unittest.TestCase):
    def assert_token(self, response, user):
        self.assertEqual(response["tokenType"], "Bearer")
        self.assertEqual(set(response["user"]), {"id", "email", "nickname"})
        self.assertEqual(response["user"]["email"], user["email"])
        self.assertEqual(response["user"]["nickname"], user["nickname"])

        expires = datetime.fromisoformat(response["expiresAt"].replace("Z", "+00:00"))
        remaining = expires - datetime.now(timezone.utc)
        self.assertGreater(remaining, timedelta(minutes=58))
        self.assertLess(remaining, timedelta(minutes=61))

        status, profile = request("GET", "me", token=response["accessToken"])
        self.assertEqual(status, 200)
        self.assertEqual(profile, response["user"])

    def test_registration_and_login_both_grant_access(self):
        self.assertEqual(request("GET", "me")[0], 401)
        first = account("Организатор")
        second = account("Участник")

        for user in (first, second):
            with self.subTest(nickname=user["nickname"]):
                status, registered = request("POST", "register", user)
                self.assertEqual(status, 201)
                self.assert_token(registered, user)

                credentials = {"email": f" {user['email'].upper()} ", "password": user["password"]}
                status, logged_in = request("POST", "login", credentials)
                self.assertEqual(status, 200)
                self.assert_token(logged_in, user)
                self.assertEqual(logged_in["user"], registered["user"])

    def test_registration_conflict_and_invalid_credentials(self):
        user = account("Покупатель")
        self.assertEqual(request("POST", "register", {**user, "password": "short"})[0], 400)
        self.assertEqual(request("POST", "register", {**user, "email": "user@ "})[0], 400)

        variants = [user, {**user, "email": user["email"].upper()}]
        with ThreadPoolExecutor(max_workers=2) as pool:
            responses = list(pool.map(lambda item: request("POST", "register", item), variants))

        self.assertEqual(sorted(status for status, _ in responses), [201, 409])
        conflict = next(body for status, body in responses if status == 409)
        self.assertEqual(conflict["title"], "EMAIL_ALREADY_EXISTS")

        for email in (user["email"], account("Другой")["email"]):
            status, response = request("POST", "login", {"email": email, "password": "wrong-password"})
            self.assertEqual(status, 401)
            self.assertEqual(response["title"], "INVALID_CREDENTIALS")

    def test_long_unicode_password_uses_the_entire_value(self):
        user = account("Длинный пароль")
        prefix = "Ж" * 40
        user["password"] = prefix + "A"

        self.assertEqual(request("POST", "register", user)[0], 201)

        credentials = {"email": user["email"], "password": prefix + "B"}
        self.assertEqual(request("POST", "login", credentials)[0], 401)

        credentials["password"] = user["password"]
        self.assertEqual(request("POST", "login", credentials)[0], 200)


if __name__ == "__main__":
    unittest.main()
