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
        if extensions.livingAutopilot_core then
            extensions.livingAutopilot_core.toggleAutopilot()
        else
            cfg.veh:queueLuaCommand('ai.setMode("disabled")')
        end
        cfg.veh_id = 0
        cfg.veh = nil
    else
        if be:getPlayerVehicle(0) then
            cfg.veh_id = be:getPlayerVehicleID(0)
            cfg.veh = be:getPlayerVehicle(0)
            cfg.enabled = true
            if extensions.livingAutopilot_core then
                notify("Finded AliveAutopilot, enabling!", "INFO")
                if extensions.livingAutopilot_core.isAutopilotActive(cfg.veh_id) then
                    notify("AliveAutopilot is enabled! Disabling")
                    extensions.livingAutopilot_core.stopAutopilot(cfg.veh_id)
                end
                extensions.livingAutopilot_core.toggleAutopilot()
            else
                cfg.veh:queueLuaCommand('ai.setMode("traffic")')
            end
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
    if vehicle == nil then return nil end
    vehicle:queueLuaCommand([[
        local val = electrics.values.steering or 0
        obj:queueGameEngineLua("onSteeringReceived(" .. tostring(val) .. ")")
    ]])
end

function udpSendRotate()
    if cfg.udpSocket then
        cfg.udpSocket:send(tostring(round(_G.spectapilot_steering)))
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
    if cfg.udpSocket then
        cfg.udpSocket:close()
        cfg.udpSocket = nil
    end

    notify("Load UDP Connection to emulator", "INFO")
    local socket = require("socket")
    local udp = socket.udp()
    udp:settimeout(0) 
    udp:setpeername(cfg.host, cfg.port)
    cfg.udpSocket = udp
    notify("Loaded", "INFO")
end

local function disconnectUDP()
    if cfg.udpSocket then
        notify("Close UDP Connection to emulator", "INFO")
        cfg.udpSocket:close()
        cfg.udpSocket = nil
    end
end

local function restartUDP()
    disconnectUDP()
    connectUDP()
end

local function onExtensionLoaded()
    notify("SpectAPilot Success loaded!", "INFO")
    connectUDP()
end

local function vehCheck(idCar)
    if cfg.enabled then
        toggleAP()
    end
end

local function onVehicleResetted(vehicleId) vehCheck(vehicleId) end

local function onVehicleSwitched(oldId, newId, player) vehCheck(oldId) end

M.onUpdate = onUpdate
M.notify = notify
M.onExtensionLoaded = onExtensionLoaded
M.toggleAP = toggleAP
M.getAPSteering = getAPSteering
M.onSteeringReceived = _G.onSteeringReceived
M.connectUDP = connectUDP
M.udpSendRotate = udpSendRotate
M.onVehicleResetted = onVehicleResetted
M.disconnectUDP = disconnectUDP
M.restartUDP = restartUDP
M.onVehicleSwitched = onVehicleSwitched
M.vehCheck = vehCheck
M.cfg = cfg
return M