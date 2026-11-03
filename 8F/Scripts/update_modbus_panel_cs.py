with open('Views/ModbusTestPanel.xaml.cs', 'r', encoding='utf-8') as f:
    cs = f.read()

target_status = """        private void UpdateServerStatus()
        {
            if (_modbusService != null && _modbusService.IsRunning)
            {
                string fallbackSuffix = _modbusService.IsChannelCountFallback
                    ? $" (Fallback: {_modbusService.ChannelCountConfigError})"
                    : "";
                lblStatus.Text = $"Active (Port {_modbusService.Port}, Heartbeat {_modbusService.HeartbeatIntervalMs}ms, Channels: {_modbusService.ChannelCount}{fallbackSuffix})";
                lblStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                lblStatus.Text = "Disabled / Stopped";
                lblStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }"""

replacement_status = """        private void UpdateServerStatus()
        {
            if (_modbusService != null && _modbusService.IsRunning)
            {
                string fallbackSuffix = _modbusService.IsChannelCountFallback
                    ? $" (Fallback: {_modbusService.ChannelCountConfigError})"
                    : "";
                lblStatus.Text = $"Running · Port {_modbusService.Port}, Ch: {_modbusService.ChannelCount}{fallbackSuffix}";
                lblStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                lblStatus.Text = _modbusService?.StatusDescription ?? "Disabled / Stopped";
                lblStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }"""

target_refresh = """        private void RefreshRegisterDisplays()
        {
            if (_modbusService != null)
            {
                // Address 0: Heartbeat"""

replacement_refresh = """        private void RefreshRegisterDisplays()
        {
            if (_modbusService != null)
            {
                UpdateServerStatus();
                lblEndpoints.Text = _modbusService.ListeningEndpointsDescription;
                lblUnitIdMode.Text = _modbusService.UnitIdModeDescription;
                lblClientCount.Text = _modbusService.ActiveClientCount.ToString();
                lblLastRequestTime.Text = _modbusService.LastRequestTime.HasValue
                    ? _modbusService.LastRequestTime.Value.ToString("HH:mm:ss.fff")
                    : "None";
                lblCounters.Text = $"Requests: {_modbusService.TotalRequestsCount} | Exceptions: {_modbusService.TotalExceptionsCount} | Unknown IDs: {_modbusService.UnknownUnitIdRequestsCount}";

                // Address 0: Heartbeat"""

def replace_text(source, target, replacement):
    if target in source:
        return source.replace(target, replacement)
    elif target.replace('\r\n', '\n') in source.replace('\r\n', '\n'):
        return source.replace('\r\n', '\n').replace(target.replace('\r\n', '\n'), replacement.replace('\r\n', '\n')).replace('\n', '\r\n')
    else:
        print("Warning: target not found")
        return source

cs = replace_text(cs, target_status, replacement_status)
cs = replace_text(cs, target_refresh, replacement_refresh)

with open('Views/ModbusTestPanel.xaml.cs', 'w', encoding='utf-8') as f:
    f.write(cs)
print("Updated ModbusTestPanel.xaml.cs")
