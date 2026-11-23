import json
from contextlib import contextmanager
from urllib.parse import urlencode, urlsplit, urlunsplit

from websocket import create_connection

from auth_smoke import BASE_URL


class Chat:
    def __init__(self, socket):
        self.socket = socket

    def receive(self, kind):
        frame = json.loads(self.socket.recv())
        if frame["type"] != kind:
            raise AssertionError((kind, frame))
        return frame

    def send(self, kind, **fields):
        self.socket.send(json.dumps({"type": kind, **fields}))

    def history(self, **cursor):
        self.send("history", **cursor)
        return self.receive("history")["data"]

    def post(self, text, key=None):
        self.send("message", text=text, idempotencyKey=key)


@contextmanager
def chat(path, token=None, after_id=None):
    base = urlsplit(BASE_URL)
    query = "" if after_id is None else urlencode({"afterId": after_id})
    url = urlunsplit(("wss" if base.scheme == "https" else "ws", base.netloc, base.path + path + "/chat/ws", query, ""))
    socket = create_connection(url, timeout=10, suppress_origin=True)
    try:
        client = Chat(socket)
        client.initial = client.receive("ready")["data"]
        if token:
            client.send("auth", accessToken=token)
            client.receive("authenticated")
        yield client
    finally:
        socket.close()
