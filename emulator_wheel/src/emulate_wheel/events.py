import pygame
from src.emulate_wheel.config import WHEEL_RANGE_GAME, WHEEL_RANGE_REAL, MOZA_ROTATE_MAX, MOZA_ROTATE_MIN

class EventsEmuWheel:
    def _handle_events(self):
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                self.is_running = False

        keys = pygame.key.get_pressed()
        if keys[self.binds["left"]]:
            self._change_rotate(-self.change_rotate_default)
        if keys[self.binds["right"]]:
            self._change_rotate(self.change_rotate_default)

    def _change_rotate(self, rotate):
        new_game_angle = self.game_angle + rotate
        new_output_angle = (new_game_angle / WHEEL_RANGE_GAME) * WHEEL_RANGE_REAL
        if MOZA_ROTATE_MIN <= new_output_angle <= MOZA_ROTATE_MAX:
            self.game_angle = new_game_angle