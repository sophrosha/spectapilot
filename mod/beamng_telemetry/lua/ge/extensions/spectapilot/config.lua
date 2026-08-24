local M = {}

M.enabled = false
_G.host = "127.0.0.1"
_G.port = 7777
M.disable_wheel = true
M.debug = false

M.update_telemetry = 0.10
M.veh_id = 0
M.veh = nil
M.timer = 0

return M