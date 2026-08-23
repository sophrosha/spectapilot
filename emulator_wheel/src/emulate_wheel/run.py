import pygame

class RunEmuWheel:
    def run(self):
        while self.is_running:
            self.handle_events()

            self.update()
            self.update_wheel()

            self.screen.fill((30, 30, 30))
            self.draw()

            pygame.display.flip()
            self.clock.tick(self.fps)