with open('Views/MainWindow.xaml.cs', 'r', encoding='utf-8') as f:
    content = f.read()

target = """                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Modbus TCP Slave start notice: {ex.Message}");
                }
            }"""

replacement = """                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Modbus TCP Slave start notice: {ex.Message}");
                    ModbusLogger.Error($"Modbus TCP Slave start failure: {ex.Message}", ex);
                }
            }
            else
            {
                ModbusLogger.Info("[Modbus TCP Slave] Server is NOT started: 'IsModbusServerEnable' is set to false in App.config.");
            }"""

if target in content:
    content = content.replace(target, replacement)
    with open('Views/MainWindow.xaml.cs', 'w', encoding='utf-8') as f:
        f.write(content)
    print("Replaced successfully (CRLF)")
elif target.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace('\r\n', '\n').replace(target.replace('\r\n', '\n'), replacement.replace('\r\n', '\n'))
    with open('Views/MainWindow.xaml.cs', 'w', encoding='utf-8') as f:
        f.write(content)
    print("Replaced successfully (LF)")
else:
    print("Target not found")
