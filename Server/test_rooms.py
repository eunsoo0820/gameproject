"""Exercise two independent HTTP clients against the actual room handler."""
import json
import threading
import time
import unittest
from http.server import HTTPServer
from urllib.request import urlopen, Request
from rooms import Handler


class CrewSyncTest(unittest.TestCase):
    def test_two_clients(self):
        with HTTPServer(("127.0.0.1", 0), Handler) as server:
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                def post(action, credentials=None, **fields):
                    data = dict(credentials or {}, **fields)
                    req = Request(f"http://127.0.0.1:{server.server_port}/{action}",
                                  data=json.dumps(data).encode())
                    with urlopen(req, timeout=3) as response:
                        return json.load(response)

                lobby = post("create", harbor=True)
                mate = post("join", code=lobby["code"], harbor=True)
                post("ready", lobby, crew="Charles")
                post("ready", mate, crew="Emma")
                at_wheel = dict(x=-.8, y=3.2, z=-.4, yaw=0, aboard=True)
                on_pier = dict(x=0, y=1.2, z=23, yaw=180, aboard=False)
                post("dock", lobby, **at_wheel)
                post("dock", mate, **on_pier)
                self.assertEqual("WAIT", post("depart", lobby)["error"])
                on_deck = dict(x=0, y=1.1, z=9, yaw=0, aboard=True)
                dock = post("dock", mate, **on_deck)
                self.assertEqual((2, 2, 1), (dock["boarded"], dock["total"], len(dock["dockPeers"])))
                self.assertEqual("WAIT", post("depart", mate)["error"])
                self.assertTrue(post("depart", lobby)["departed"])
                sailed = post("dock", mate, **on_deck)
                self.assertTrue(sailed["departed"])
                self.assertEqual([], sailed["dockPeers"])

                host = post("create")
                self.assertRegex(host["code"], r"^[A-Za-z0-9]{5}$")
                self.assertIn("error", post("join", code=host["code"].swapcase()))
                guest = post("join", code=host["code"])
                post("ready", host, crew="Daniel")
                self.assertEqual("WAIT", post("depart", host)["error"])
                post("ready", guest, crew="Emma")
                self.assertTrue(post("depart", guest)["departed"])
                self.assertTrue(post("poll", host)["departed"])

                def sync(player, x, tasks, stage, **actions):
                    return post("sync", player, x=x, y=2, z=-3, yaw=90,
                                tasks=tasks, stage=stage, **actions)

                sync(host, 10, 5, 1)
                result = sync(guest, 20, 10, 1)
                self.assertEqual((15, 2), (result["tasks"], result["stage"]))
                self.assertEqual([10], [p["x"] for p in result["peers"]])
                self.assertEqual([20], [p["x"] for p in sync(host, 12, 0, 0)["peers"]])
                self.assertTrue(sync(host, 12, 0, 2, helm=True, throttle=1)["helm"])
                time.sleep(.06)
                passenger = sync(guest, 20, 0, 2, helm=True)
                self.assertFalse(passenger["helm"])
                self.assertLess(passenger["sz"], 0)
                self.assertTrue(sync(host, 12, 0, 2, op=1, itemKey="Fuel")["won"])
                self.assertFalse(sync(guest, 20, 0, 2, op=1, itemKey="Fuel")["won"])
                for held in range(8):
                    sync(host, 12, 0, 2, held=held)
                    guest_view = sync(guest, 20, 0, 2, helm=True, held=7-held)
                    self.assertTrue(guest_view["helm"])
                    self.assertEqual(held, guest_view["peers"][0]["held"])
                    host_view = sync(host, 12, 0, 2, helm=True)
                    self.assertFalse(host_view["helm"])
                    self.assertEqual(7-held, host_view["peers"][0]["held"])
                sync(guest, 20, 0, 2, helm=False)
                self.assertTrue(sync(host, 12, 0, 2, helm=True)["helm"])
                drop = dict(op=2, dropping=True, item=5, dropPos=dict(x=1, y=1, z=1))
                sync(host, 12, 0, 2, **drop)
                dropped = sync(host, 12, 0, 2, **drop)["drops"]
                self.assertEqual(1, len(dropped))
                self.assertTrue(sync(guest, 20, 0, 2, op=2, itemKey=dropped[0]["key"])["won"])
                self.assertEqual([], sync(host, 12, 0, 2)["drops"])
                self.assertTrue(sync(host, 12, 0, 2, op=3, itemKey="purify")["won"])
                busy = sync(guest, 20, 0, 2, op=3, itemKey="purify")
                self.assertFalse(busy["won"])
                self.assertGreater(busy["remaining"], 0)
                self.assertEqual(29, sync(guest, 20, 0, 2, op=4, itemKey="drink", rainFilled=True)["water"])
                self.assertEqual(29, sync(host, 12, 0, 2)["water"])
                for stage in range(3, 8):
                    sync(guest, 21, 15, stage)
                    snapshot = sync(host, 12, 0, 1)
                    self.assertEqual((15, stage), (snapshot["tasks"], snapshot["stage"]))
                self.assertEqual([12], [p["x"] for p in sync(guest, 21, 15, 7)["peers"]])
            finally:
                server.shutdown()
                thread.join()


if __name__ == "__main__":
    unittest.main()
