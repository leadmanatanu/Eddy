import os

configs = [
    'App.config',
    r'bin\Debug\net8.0-windows\8F.dll.config',
    r'bin\Release\net8.0-windows\win-x64\8F.dll.config'
]

modbus_block = """		<!-- ============================================================ -->
		<!-- MODBUS TCP SLAVE / SERVER TOGGLES -->
		<!-- ============================================================ -->
		<add key="IsModbusServerEnable" value="true" />
		<add key="ModbusServerPort" value="5020" />
		<add key="ModbusHeartbeatIntervalMs" value="1000" />
		<add key="ModbusBindAddress" value="0.0.0.0" />
		<add key="ModbusUnitIdMode" value="Any" />
		<add key="ModbusFixedUnitId" value="1" />
		<add key="ModbusEnableRawFrameLogging" value="false" />
		<add key="ModbusAutoAddFirewallRule" value="false" />"""

for path in configs:
    if not os.path.exists(path):
        continue
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    # If Modbus section exists, replace it
    if '<add key="IsModbusServerEnable"' in content:
        import re
        content = re.sub(
            r'(\t*<!-- =+ -->\r?\n)?\t*<!-- MODBUS TCP SLAVE / SERVER TOGGLES -->\r?\n(\t*<!-- =+ -->\r?\n)?(\t*<add key="[^\"]+" value="[^\"]*" />\r?\n)+',
            modbus_block + '\n',
            content
        )
    else:
        # Insert before </appSettings>
        content = content.replace('</appSettings>', modbus_block + '\n\t</appSettings>')

    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated {path}")
