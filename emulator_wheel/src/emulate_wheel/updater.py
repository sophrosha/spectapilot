import pygame
import queue

from src.emulate_wheel.config import WHEEL_RANGE_GAME, WHEEL_RANGE_REAL

class UpdaterEmuWheel:
    def _draw(self):
        rotated_wheel = pygame.transform.rotate(self.steer_wheel, -self.visual_angle )
        self.text_rotate = self.font_main.render(f"Rotate: {self.output_angle}", True, (255, 255, 255))
        wheel_rect = rotated_wheel.get_rect()
        wheel_rect.center = self.screen_center
        self.screen.blit(rotated_wheel, wheel_rect)
        self.screen.blit(self.text_rotate, (20, 30))

    def _update(self): 
        try:
            while not self.data_queue.empty():
                network_angle = self.data_queue.get_nowait()
                self.game_angle = (network_angle / WHEEL_RANGE_REAL) * WHEEL_RANGE_GAME
        except queue.Empty:
            pass

        self.output_angle = round((self.game_angle / WHEEL_RANGE_GAME) * WHEEL_RANGE_REAL)
        
    def _update_wheel(self):
        target_angle = self.game_angle
        speed = 2.5
        if self.visual_angle < target_angle: self.visual_angle = min(self.visual_angle + speed, target_angle)
        elif self.visual_angle > target_angle: self.visual_angle = max(self.visual_angle - speed, target_angle)