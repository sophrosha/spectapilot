import pygame
from pathlib import Path as p

class SetupEmuWheel:
    def _setup_app(self):
        self.screen = pygame.display.set_mode(
            (self.screen_width, self.screen_height)
        )
        pygame.display.set_caption(self.title)
        self.screen_center = (self.screen_width // 2, self.screen_height // 2)

        self.game_angle = 0
        self.visual_angle = 0
        self.change_rotate_default = 2
        self.font_main = pygame.font.Font(None, 26)
        
        self.clock = pygame.time.Clock()
        self.fps = 100
        self.is_running = True

        self.binds = {
            "left": pygame.K_h,
            "right": pygame.K_l,
            "test": pygame.K_j
        }

        self.steer_wheel = pygame.image.load(p("assets/steering_wheel.png")).convert_alpha()
        self.steer_wheel = pygame.transform.scale(self.steer_wheel, (400, 400))