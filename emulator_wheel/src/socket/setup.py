import socket as sock

class SetupSocketEmuWheel:
    def setup(self):
        self.server_socket = sock.socket(sock.AF_INET, sock.SOCK_DGRAM)
        self.server_socket.bind((self.host, self.port))
        print(f"Socket started: {self.host}:{self.port}")