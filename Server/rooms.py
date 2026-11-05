"""Run behind a public HTTPS reverse proxy. Set DRIFT_ROOM_URL on clients."""
import json
import math
import os
import secrets
import string
import time
from http.server import BaseHTTPRequestHandler, HTTPServer

rooms = {}
alphabet = string.ascii_letters + string.digits


def advance_ship(room, now):
    ship = room["ship"]
    dt = min(1, now - ship["time"])
    ship["time"] = now
    driver = room["members"].get(ship["driver"], {})
    rudder, throttle = driver.get("controls", (0, 0)) if now - driver.get("seen", 0) < 1 else (0, 0)
    target = throttle * 3.6
    step = dt * (0.9 if abs(target) > abs(ship["speed"]) else 1.35)
    ship["speed"] += max(-step, min(step, target - ship["speed"]))
    ship["angle"] += rudder * 12 * max(-1, min(1, ship["speed"] / 1.4)) * dt
    heading = math.radians(ship["angle"])
    ship["sx"] -= math.sin(heading) * ship["speed"] * dt
    ship["sz"] -= math.cos(heading) * ship["speed"] * dt


def handle(action, data):
    now = time.monotonic()
    for code, room in list(rooms.items()):
        members = room["members"]
        for key in list(members):
            if now - members[key]["seen"] > 60:
                del members[key]
        if room["host"] not in members:
            del rooms[code]
    code = data.get("code", "")
    token = data.get("token", "")
    if action == "create":
        while True:
            code = "".join(secrets.choice(alphabet) for _ in range(5))
            if code not in rooms and any(c.isdigit() for c in code) and any(c.isalpha() for c in code):
                break
        rooms[code] = {"harbor": bool(data.get("harbor")), "members": {}, "host": "", "departed": False, "tasks": 0, "stage": 0,
                       "ship": dict(time=now, driver="", speed=0., sx=0., sz=0., angle=0.),
                       "taken": {}, "drops": {}, "water": 0, "purify": 0, "rainFilled": False, "part": False}
    room = rooms.get(code)
    if room is None:
        raise ValueError("ROOM")
    members = room["members"]
    if action in ("create", "join"):
        if bool(data.get("harbor")) != room["harbor"]:
            raise ValueError("VERSION")
        if room["departed"] or len(members) >= 6:
            raise ValueError("CLOSED")
        token = secrets.token_urlsafe(24)
        members[token] = {"crew": "", "seen": now, "id": secrets.token_hex(8)}
        if action == "create":
            room["host"] = token
    if token not in members:
        raise ValueError("ROOM")
    members[token]["seen"] = now
    if action == "ready":
        crew = data.get("crew")
        if crew not in ("Daniel", "Charles", "Ellis", "Alice", "Karl", "Carl", "Emma", "Mina"):
            raise ValueError("CREW")
        members[token]["crew"] = crew
    if action == "dock" and not room["departed"]:
        pose = {key: float(data[key]) for key in ("x", "y", "z", "yaw")}
        if not all(math.isfinite(v) for v in pose.values()):
            raise ValueError("POSE")
        member = members[token]
        member["aboard"] = bool(data.get("aboard")) and bool(member["crew"])
        member["dockPose"] = dict(pose, id=member["id"], crew=member["crew"], held=0)
    aboard = sum(bool(m.get("aboard")) and now - m["seen"] < 5 for m in members.values())
    if action == "depart" and not room["departed"]:
        if room["harbor"]:
            pose = members[token].get("dockPose", {})
            if aboard != len(members) or abs(pose.get("x", 100) + .81) > 3 or abs(pose.get("z", 100) + .35) > 3 or not 2 < pose.get("y", 0) < 5:
                raise ValueError("WAIT")
        if not all(member["crew"] for member in members.values()):
            raise ValueError("WAIT")
        room["departed"] = True
    if action == "sync":
        if not room["departed"]:
            raise ValueError("WAIT")
        pose = {key: float(data[key]) for key in ("x", "y", "z", "yaw")}
        if not all(math.isfinite(value) for value in pose.values()):
            raise ValueError("POSE")
        ship = room["ship"]
        advance_ship(room, now)
        if ship["driver"] not in members or now - members[ship["driver"]]["seen"] > 3:
            ship["driver"] = ""
        if data.get("helm") and not ship["driver"]:
            ship["driver"] = token
        elif not data.get("helm") and ship["driver"] == token:
            ship["driver"] = ""
        members[token]["controls"] = tuple(max(-1, min(1, int(data.get(k, 0)))) for k in ("rudder", "throttle"))
        if data.get("part") and room["stage"] == 5:
            room["part"] = True
        if data.get("rainFilled") and not room["rainFilled"]:
            room.update(rainFilled=True, water=30, purify=0)
        if room["purify"] and now >= room["purify"]:
            room.update(water=min(30, room["water"] + 1), purify=0)
        op = data.get("op", 0)
        member = members[token]
        if op == member.get("op", 0) + 1:
            key = data.get("itemKey", "")
            won = False
            if key == "purify":
                if room["stage"] not in (5, 6) and not room["purify"] and room["water"] < 30:
                    room["purify"] = now + 60
                    won = True
            elif key == "drink":
                if room["water"] and room["stage"] not in (5, 6):
                    room["water"] -= 1
                    won = True
            elif data.get("dropping"):
                if 1 <= data["item"] <= 7 and len(room["drops"]) < 64:
                    key = member["id"] + "-" + str(op)
                    room["drops"][key] = dict(key=key, item=data["item"], pos=data["dropPos"])
                    won = True
            elif key in room["drops"]:
                del room["drops"][key]
                won = True
            elif key in ("Fuel", "Scrap", "FishingRod", "PurifierPart") and key not in room["taken"]:
                room["taken"][key] = True
                won = True
            member.update(op=op, won=won)
        held = int(data.get("held", 0))
        members[token]["pose"] = dict(pose, held=held if 0 <= held <= 7 else 0, id=members[token]["id"], crew=members[token]["crew"])
        room["tasks"] |= int(data["tasks"]) & 15
        room["stage"] = max(room["stage"], min(7, int(data["stage"])))
        if room["tasks"] == 15:
            room["stage"] = max(2, room["stage"])
    if action == "leave":
        del members[token]
    ship = room["ship"]
    member = members.get(token, {})
    return dict(boarded=aboard, total=len(members), dockPeers=[m["dockPose"] for k,m in members.items() if k != token and "dockPose" in m and not room["departed"]], water=room["water"], remaining=max(0, room["purify"] - now), part=room["part"], helm=ship["driver"] == token, sx=ship["sx"], sz=ship["sz"], angle=ship["angle"], speed=ship["speed"],
                taken=list(room["taken"]), drops=list(room["drops"].values()), op=member.get("op", 0), won=member.get("won", False),
                code=code, token=token, departed=room["departed"],
                tasks=room["tasks"], stage=room["stage"],
                peers=[m["pose"] for key, m in members.items() if key != token and "pose" in m],
                roster=", ".join(m["crew"] or "..." for m in members.values()))


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200 if self.path == "/health" else 404)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def do_POST(self):
        try:
            length = int(self.headers.get("Content-Length", 0))
            if not 0 < length <= 2048:
                raise ValueError("SIZE")
            result = handle(self.path.strip("/"), json.loads(self.rfile.read(length)))
        except (ValueError, TypeError, AttributeError, KeyError, OverflowError) as exc:
            result = {"error": str(exc)}
        payload = json.dumps(result).encode()
        self.send_response(200)
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)


if __name__ == "__main__":
    HTTPServer(("0.0.0.0", int(os.environ.get("PORT", "8787"))), Handler).serve_forever()
