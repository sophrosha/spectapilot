class ListenLoopSocketEmuWheel:
    def listen_loop(self):
        while True:
            try:
                data, _ = self.server_socket.recvfrom(1024)
                message = data.decode().strip()
                if message.replace('-', '', 1).isdigit():
                    angle = int(message)
                    self.data_queue.put(angle)
            except Exception as e:
                print(f"[UDP ERROR] {e}")
                break