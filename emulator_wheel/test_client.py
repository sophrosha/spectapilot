import socket

class TestEmulatorWheel:
    def __init__(self, address="127.0.0.1", port=7777):
        self.address = address
        self.port = port
        self.setup()

    def setup(self):
        self.client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.server_address = (self.address, self.port)
        print("-> Server started")

    def run(self):
        while True:
            try:
                rotate = input("Enter rotate > ")
                self.client.sendto(str(rotate).encode(), self.server_address)
            except KeyboardInterrupt:
                print("\n!> Exiting")
                break

if __name__ == "__main__":
    run = TestEmulatorWheel()
    run.run()
