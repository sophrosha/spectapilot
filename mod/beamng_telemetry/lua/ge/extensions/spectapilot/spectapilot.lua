local M = {}
local cfg = require('spectapilot.config')

local function notify(text, typpe)
    if typpe == "DEBUG" then
        log('I', 'DEBUG', tostring(text))
    elseif typpe == "INFO" then
        log('I', 'INFO', tostring(text))
    end
end

local function toggleAP()
    if cfg.enabled then
        cfg.enabled = false
        cfg.veh:queueLuaCommand('ai.setMode("disabled")')
        cfg.veh_id = 0
        cfg.veh = nil
    else
        if be:getPlayerVehicle(0) then
            cfg.veh_id = be:getPlayerVehicleID(0)
            cfg.veh = be:getPlayerVehicle(0)
            cfg.enabled = true
            cfg.veh:queueLuaCommand('ai.setMode("traffic")')
        else
            notify("Vehicle not found", "INFO")
        end
    end
end

_G.spectapilot_steering = 0
function _G.onSteeringReceived(value)
    _G.spectapilot_steering = value
    udpSendRotate()
end

local function getAPSteering()
    local vehicle = cfg.veh
    if vehicle == 0 or vehicle == nil then return nil end
    vehicle:queueLuaCommand([[
        local val = electrics.values.steering or 0
        obj:queueGameEngineLua("onSteeringReceived(" .. tostring(val) .. ")")
    ]])
end

local function udpSendRotate()
    if _G.udpSocket then
        _G.udpSocket:send(tostring(round(_G.spectapilot_steering)))
    end
end

local function onUpdate(dt)
    if not cfg.enabled then return end
    cfg.timer = cfg.timer + dt
    if cfg.timer >= cfg.update_telemetry then
        getAPSteering()
        if cfg.debug then notify("Rotate: " .. tostring(_G.spectapilot_steering), "DEBUG") end
        cfg.timer = cfg.timer - cfg.update_telemetry
    end
end

local function connectUDP()
    notify("Load UDP Connection to emulator", "INFO")
    local socket = require("socket")
    local udp = socket.udp()
    udp:setpeername("127.0.0.1", 7777)
    _G.udpSocket = udp
    notify("Loaded", "INFO")
end

local function onExtensionLoaded()
    notify("SpectAPilot Success loaded!", "INFO")
end


M.onUpdate = onUpdate
M.notify = notify
M.onExtensionLoaded = onExtensionLoaded
M.toggleAP = toggleAP
M.getAPSteering = getAPSteering
M.onSteeringReceived = _G.onSteeringReceived
M.connectUDP = connectUDP
M.udpSendRotate = udpSendRotate
M.cfg = cfg
return M