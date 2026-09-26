local M = {}

M.enabled = false
M.host = "127.0.0.1"
M.port = 7777
M.udpSocket = nil
M.disable_wheel = true
M.debug = false

M.update_telemetry = 0.008
M.veh_id = 0
M.veh = nil
M.timer = 0

return M