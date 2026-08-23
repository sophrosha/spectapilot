import pygame
from threading import Thread
import queue

from src.emulate_wheel.events import EventsEmuWheel
from src.emulate_wheel.run import RunEmuWheel
from src.emulate_wheel.setup import SetupEmuWheel
from src.emulate_wheel.updater import UpdaterEmuWheel
from src.socket.listen_loop import ListenLoopSocketEmuWheel
from src.socket.setup import SetupSocketEmuWheel

class EmuSWheel(SetupEmuWheel, RunEmuWheel, UpdaterEmuWheel, EventsEmuWheel):
    def __init__(self, data_queue, width=550, height=550, title="Emulator steering wheel"):
        self.screen_width = width
        self.screen_height = height
        self.title = title
        self.data_queue = data_queue
        pygame.init()
        self._setup_app()

class SocketEmuSWheel(ListenLoopSocketEmuWheel, SetupSocketEmuWheel):
    def __init__(self, data_queue, host="127.0.0.1", port=7777):
        self.host = host
        self.port = port
        self.data_queue = data_queue
        self._setup()

if __name__ == "__main__":
    shared_queue = queue.Queue()
    sock_serv = SocketEmuSWheel(shared_queue)
    thr = Thread(target=sock_serv.listen_loop, daemon=True)
    thr.start()
    app = EmuSWheel(shared_queue)
    app.run()