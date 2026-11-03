with open('Views/ModbusTestPanel.xaml', 'r', encoding='utf-8') as f:
    xaml = f.read()

target_xaml = """        <!-- Server Status -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,12">
            <TextBlock Text="Status: " FontSize="14" FontWeight="SemiBold" Foreground="#333333"/>
            <TextBlock Name="lblStatus" Text="Stopped" FontSize="14" FontWeight="Bold" Foreground="Red"/>
        </StackPanel>"""

replacement_xaml = """        <!-- Server Status & Diagnostics -->
        <Border Grid.Row="1" Background="White" BorderBrush="#CCCCCC" BorderThickness="1" CornerRadius="6" Padding="12" Margin="0,0,0,12">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <!-- Status Line -->
                <StackPanel Grid.Row="0" Grid.Column="0" Grid.ColumnSpan="2" Orientation="Horizontal" Margin="0,0,0,6">
                    <TextBlock Text="Status: " FontSize="13" FontWeight="SemiBold" Foreground="#333333"/>
                    <TextBlock Name="lblStatus" Text="Stopped" FontSize="13" FontWeight="Bold" Foreground="Red"/>
                </StackPanel>

                <!-- Listening Endpoints -->
                <StackPanel Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="2" Margin="0,0,0,6">
                    <TextBlock Text="Endpoints:" FontSize="11" FontWeight="SemiBold" Foreground="#666666"/>
                    <TextBlock Name="lblEndpoints" Text="None" FontSize="12" FontWeight="Bold" Foreground="#305371" TextWrapping="Wrap"/>
                </StackPanel>

                <!-- Unit ID Mode & Connected Clients -->
                <StackPanel Grid.Row="2" Grid.Column="0" Margin="0,0,0,6">
                    <TextBlock Text="Unit ID Mode:" FontSize="11" FontWeight="SemiBold" Foreground="#666666"/>
                    <TextBlock Name="lblUnitIdMode" Text="Any (0..255)" FontSize="12" FontWeight="Bold" Foreground="#28A745"/>
                </StackPanel>

                <StackPanel Grid.Row="2" Grid.Column="1" Margin="0,0,0,6">
                    <TextBlock Text="Active Clients:" FontSize="11" FontWeight="SemiBold" Foreground="#666666"/>
                    <TextBlock Name="lblClientCount" Text="0" FontSize="12" FontWeight="Bold" Foreground="#005A9E"/>
                </StackPanel>

                <!-- Last Request Time -->
                <StackPanel Grid.Row="3" Grid.Column="0" Grid.ColumnSpan="2" Margin="0,0,0,6">
                    <TextBlock Text="Last Request:" FontSize="11" FontWeight="SemiBold" Foreground="#666666"/>
                    <TextBlock Name="lblLastRequestTime" Text="None" FontSize="12" FontWeight="SemiBold" Foreground="#333333"/>
                </StackPanel>

                <!-- Counters -->
                <StackPanel Grid.Row="4" Grid.Column="0" Grid.ColumnSpan="2">
                    <TextBlock Text="Traffic Counters:" FontSize="11" FontWeight="SemiBold" Foreground="#666666"/>
                    <TextBlock Name="lblCounters" Text="Requests: 0 | Exceptions: 0 | Unknown IDs: 0" FontSize="11" Foreground="#555555"/>
                </StackPanel>
            </Grid>
        </Border>"""

if target_xaml in xaml:
    xaml = xaml.replace(target_xaml, replacement_xaml)
elif target_xaml.replace('\r\n', '\n') in xaml.replace('\r\n', '\n'):
    xaml = xaml.replace('\r\n', '\n').replace(target_xaml.replace('\r\n', '\n'), replacement_xaml.replace('\r\n', '\n')).replace('\n', '\r\n')
else:
    print("Warning: target_xaml not found")

with open('Views/ModbusTestPanel.xaml', 'w', encoding='utf-8') as f:
    f.write(xaml)
print("Updated ModbusTestPanel.xaml")
