import pygame

class RunEmuWheel:
    def run(self):
        while self.is_running:
            self._handle_events()

            self._update()
            self._update_wheel()

            self.screen.fill((30, 30, 30))
            self._draw()

            pygame.display.flip()
            self.clock.tick(self.fps)