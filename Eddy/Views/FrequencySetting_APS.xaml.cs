using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Eddy
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class FrequencySetting_APS : Window
    {
        public bool IsSaved = false;
        private DispatcherTimer? clearLabelTimer;
        public DeviceCOM? deviceCOM;
        bool IsEddyAdvance = false;
        public FrequencySetting_APS()
        {
            InitializeComponent();

            if (DeviceCOM.IsAttRequired)
            {
                txtGain1.PreviewTextInput += PreviewTextInput_DecimalOnly;
            }
            else
            {
                txtGain1.PreviewTextInput += PreviewTextInput_NumericOnly;
            }


            IsEddyAdvance = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["IsEddyAdvance"]);

            if (IsEddyAdvance)
            {
                lblX.Visibility = Visibility.Visible;
                lblY.Visibility = Visibility.Visible;
                txtPostAMPX.Visibility = Visibility.Visible;
                txtPostAMPY.Visibility = Visibility.Visible;
            }

            if (DeviceCOM.Configuration.Frequency.FD.Count > 0)
            {
                foreach (var item in DeviceCOM.Configuration.Frequency.FD)
                {
                    if (item.FN == 1)
                    {
                        txtPhase1.Text = (item.F / 1000).ToString();
                        txtGain1.Text = DeviceCOM.IsAttRequired ? item.G.ToString() : Convert.ToInt32(item.G).ToString();
                        txtUTH1.Text = item.UTH.ToString();
                        txtLTH1.Text = item.LTH.ToString();
                        txtPP1.Text = item.PP.ToString();
                        txtTH1.Text = item.TH.ToString();
                    }
                    //else if (item.FN == 2)
                    //{
                    //    chkD2.IsChecked = Convert.ToBoolean(item.E);
                    //    txtPhase2.Text = item.F.ToString();
                    //    txtGain2.Text = item.G.ToString();
                    //    txtUTH2.Text = item.UTH.ToString();
                    //    txtLTH2.Text = item.LTH.ToString();
                    //    txtPP2.Text = item.PP.ToString();
                    //    txtPM2.Text = item.PM.ToString();
                    //}
                    else if (item.FN == 3)
                    {

                        txtGain2.Text = DeviceCOM.IsAttRequired ? item.G.ToString() : Convert.ToInt32(item.G).ToString();
                        txtUTH2.Text = item.UTH.ToString();
                        txtLTH2.Text = item.LTH.ToString();
                        txtTH2.Text = item.TH.ToString();
                    }

                }


                foreach (var item in DeviceCOM.Configuration.Filter.FD)
                {
                    if (item.FN == 1)
                    {
                        txtH1.Text = item.H.ToString();
                        txtL1.Text = item.L.ToString();
                        txtPostAMPX.Text = item.X.ToString();
                        txtPostAMPY.Text = item.Y.ToString();
                    }



                    //else if (item.FN == 2)
                    //{
                    //    txtH2.Text = item.H.ToString();
                    //    txtL2.Text = item.L.ToString();
                    //}
                    else if (item.FN == 3)
                    {
                        txtH2.Text = item.G.ToString(); // 
                        txtL2.Text = item.L.ToString();                       
                    }
                }

            }


        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnConfigSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DeviceCOM.EnsureAbsoluteSettings();
                if (DeviceCOM.IsTubeSatart || DeviceCOM.IsCalibarationStart)
                {
                    lblMsg.Content = "The tube/calibration is in progress, no changes are allowed!";
                }
                else
                {
                    lblMsg.Content = "";
                    var msg = Validaton();

                    if (msg.Count == 0)
                    {
                        //DeviceCOM.Configuration.Marker.M3 = Convert.ToInt32(txtM3.Text);
                        if (DeviceCOM.Configuration.Frequency.FD.Count > 0)
                        {
                            foreach (var item in DeviceCOM.Configuration.Frequency.FD)
                            {
                                if (item.FN == 1)
                                {
                                    item.E = 1;
                                    item.F = Convert.ToInt32(txtPhase1.Text) * 1000;
                                    item.G = Convert.ToDecimal(txtGain1.Text);
                                    item.UTH = Convert.ToInt32(txtUTH1.Text);
                                    item.LTH = Convert.ToInt32(txtLTH1.Text);
                                    item.PP = Convert.ToInt32(txtPP1.Text);
                                    item.TH = Convert.ToInt32(txtTH1.Text);

                                    if (DeviceCOM.IsAttRequired)
                                    {
                                        if (item.AT == -1)
                                            item.AT = 0;
                                    }
                                    else
                                    {
                                        item.AT = -1;
                                    }
                                }
                                //else if (item.FN == 2)
                                //{
                                //    item.E = (Convert.ToBoolean(chkD2.IsChecked) ? 1 : 0);
                                //    item.F = Convert.ToInt32(txtPhase2.Text);
                                //    item.G = Convert.ToInt32(txtGain2.Text);
                                //    item.UTH = Convert.ToInt32(txtUTH2.Text);
                                //    item.LTH = Convert.ToInt32(txtLTH2.Text);
                                //    item.PP = Convert.ToInt32(txtPP2.Text);
                                //    item.PM = Convert.ToInt32(txtPM2.Text);
                                //}
                                else if (item.FN == 3)
                                {
                                    item.E = 0;
                                    item.F = Convert.ToInt32(txtPhase1.Text) * 1000;
                                    item.G = Convert.ToDecimal(txtGain2.Text);
                                    item.UTH = Convert.ToInt32(txtUTH2.Text);
                                    item.LTH = Convert.ToInt32(txtLTH2.Text);
                                    item.PP = Convert.ToInt32(txtPP1.Text);
                                    item.TH = Convert.ToInt32(txtTH2.Text);

                                    if (DeviceCOM.IsAttRequired)
                                    {
                                        if (item.AT == -1)
                                            item.AT = 0;
                                    }
                                    else
                                    {
                                        item.AT = -1;
                                    }

                                }
                            }

                            foreach (var item in DeviceCOM.Configuration.Filter.FD)
                            {
                                if (item.FN == 1)
                                {
                                    item.H = Convert.ToInt32(txtH1.Text);
                                    item.L = Convert.ToInt32(txtL1.Text);
                                    item.X = Convert.ToInt32(txtPostAMPX.Text);
                                    item.Y = Convert.ToInt32(txtPostAMPY.Text);
                                }
                                //else if (item.FN == 2)
                                //{
                                //    item.H = Convert.ToInt32(txtH2.Text);
                                //    item.L = Convert.ToInt32(txtL2.Text);
                                //}
                                else if (item.FN == 3)
                                {
                                    item.G = Convert.ToDecimal(txtH2.Text);
                                    item.L = Convert.ToInt32(txtL2.Text);

                                    item.X = Convert.ToInt32(txtPostAMPX.Text);
                                    item.Y = Convert.ToInt32(txtPostAMPY.Text);
                                }
                            }
                        }

                        var rat1 = true;

                        byte[] data1 = new byte[49];
                        data1[0] = Convert.ToByte(2);
                        data1[1] = Convert.ToByte(57);
                        data1[2] = Convert.ToByte(45);
                        data1[3] = Convert.ToByte(1);
                        data1[4] = Convert.ToByte(1);
                        data1[5] = Convert.ToByte(2);

                        int startBytes = 6;
                        foreach (var fd in DeviceCOM.Configuration.Frequency.FD)
                        {
                            data1[startBytes] = Convert.ToByte(fd.FN);

                            data1[startBytes + 1] = (byte)(fd.F & 0xFF);         // Lowest byte
                            data1[startBytes + 2] = (byte)((fd.F >> 8) & 0xFF);  // Byte 2
                            data1[startBytes + 3] = (byte)((fd.F >> 16) & 0xFF); // Byte 3
                            data1[startBytes + 4] = (byte)((fd.F >> 24) & 0xFF); // Highest byte

                            var gaint = Convert.ToInt16(fd.G * 10);
                            data1[startBytes + 5] = (byte)(gaint & 0xFF);
                            data1[startBytes + 6] = (byte)((gaint >> 8) & 0xFF);

                            data1[startBytes + 7] = (byte)(fd.LTH & 0xFF);
                            data1[startBytes + 8] = (byte)((fd.LTH >> 8) & 0xFF);

                            data1[startBytes + 9] = (byte)(fd.UTH & 0xFF);
                            data1[startBytes + 10] = (byte)((fd.UTH >> 8) & 0xFF);

                            startBytes = startBytes + 11;
                        }

                        foreach (var fd in DeviceCOM.Configuration.Filter.FD)
                        {
                            data1[startBytes] = Convert.ToByte(fd.FN);

                            var gaint = Convert.ToInt16(fd.G * 10);

                            ushort h = (fd.FN ==1? Convert.ToUInt16(fd.H): Convert.ToUInt16(gaint));
                            ushort l = Convert.ToUInt16(fd.L);
                            ushort x = Convert.ToUInt16(fd.X);
                            ushort y = Convert.ToUInt16(fd.Y);

                            // H
                            data1[startBytes + 1] = (byte)(h & 0xFF);         // Low byte
                            data1[startBytes + 2] = (byte)((h >> 8) & 0xFF);  // High byte

                            // L
                            data1[startBytes + 3] = (byte)(l & 0xFF);
                            data1[startBytes + 4] = (byte)((l >> 8) & 0xFF);

                            // X
                            data1[startBytes + 5] = (byte)(x & 0xFF);
                            data1[startBytes + 6] = (byte)((x >> 8) & 0xFF);

                            // Y
                            data1[startBytes + 7] = (byte)(y & 0xFF);
                            data1[startBytes + 8] = (byte)((y >> 8) & 0xFF);

                            startBytes = startBytes + 9;
                        }

                        data1[startBytes] = (byte)DeviceCOM.Configuration.Frequency.FD[0].AT;

                        rat1 = deviceCOM?.WriteDataInByte(data1) ?? false;

                        if (rat1)
                        {
                            lblMsg.Content = "Configuration Saved!!!";
                        }
                        else
                        {
                            lblMsg.Content = "Configuration Saved but no response from the ECT Instrument, please reboot it and write the configuration again!!!";
                        }

                        IsSaved = true;
                        System.IO.File.WriteAllText("Config.txt", JsonConvert.SerializeObject(DeviceCOM.Configuration));
                    }
                    else
                    {
                        lblMsg.Content = "Validatoin Error:-";
                        foreach (var m in msg)
                        {
                            lblMsg.Content = lblMsg.Content + "\r\n" + (msg.IndexOf(m) + 1).ToString() + ". " + m;
                        }
                    }
                }
            }
            catch (Exception)
            {
                lblMsg.Content = "Error while saving the Configuration!!!";
            }


            clearLabelTimer = new DispatcherTimer();
            clearLabelTimer.Interval = TimeSpan.FromSeconds(20);
            clearLabelTimer.Tick += ClearLabelTimer_Tick;
            clearLabelTimer.Start();


        }

        private void ClearLabelTimer_Tick(object? sender, EventArgs e)
        {
            lblMsg.Content = string.Empty;
            clearLabelTimer?.Stop(); // Stop the timer after clearing
        }

        public List<String> Validaton()
        {
            List<String> validationMsg = new List<string>();
            if (string.IsNullOrEmpty(txtPhase1.Text))
            {
                validationMsg.Add("Frequeny(KHz) is required and the range is 1 to 100.");
            }
            else
            {
                if (Convert.ToInt32(txtPhase1.Text) < 1 || Convert.ToInt32(txtPhase1.Text) > 100)
                {
                    validationMsg.Add("Frequeny(KHz) is required and the range is 1 to 100.");
                }
            }

            if (string.IsNullOrEmpty(txtGain1.Text))
            {
                validationMsg.Add("Gain(dB) is required and the range is 1 to 60.");
            }
            else
            {
                if (Convert.ToDecimal(txtGain1.Text) < 1 || Convert.ToDecimal(txtGain1.Text) > 60)
                {
                    validationMsg.Add("Gain(dB) is required and the range is 1 to 60.");
                }
            }

            if (string.IsNullOrEmpty(txtPP1.Text))
            {
                validationMsg.Add("Phase is required and the range is 0 to 360.");
            }
            else
            {
                if (Convert.ToInt32(txtPP1.Text) < 0 || Convert.ToInt32(txtPP1.Text) > 360)
                {
                    validationMsg.Add("Phase is required and the range is 0 to 360.");
                }
            }

            if (string.IsNullOrEmpty(txtUTH1.Text))
            {
                validationMsg.Add("High Thresold is required and the range is 0 to 100.");
            }
            else
            {
                if (Convert.ToInt32(txtUTH1.Text) < 0 || Convert.ToInt32(txtUTH1.Text) > 100)
                {
                    validationMsg.Add("High Thresold is required and the range is 0 to 100.");
                }
            }

            if (string.IsNullOrEmpty(txtLTH1.Text))
            {
                validationMsg.Add("Low Thresold is required and the range is 0 to 100.");
            }
            else
            {
                if (Convert.ToInt32(txtLTH1.Text) < 0 || Convert.ToInt32(txtLTH1.Text) > 100)
                {
                    validationMsg.Add("Low Thresold is required and the range is 0 to 100.");
                }
            }

            if (string.IsNullOrEmpty(txtTH1.Text))
            {
                validationMsg.Add("Thresold is required and the range is 0 to 100.");
            }
            else
            {
                if (Convert.ToInt32(txtTH1.Text) < 0 || Convert.ToInt32(txtTH1.Text) > 100)
                {
                    validationMsg.Add("Thresold is required and the range is 0 to 100.");
                }
            }

            if (string.IsNullOrEmpty(txtTH1.Text))
            {
                validationMsg.Add("Thresold is required and the range is 0 to 100.");
            }
            else
            {
                if (Convert.ToInt32(txtTH1.Text) < 0 || Convert.ToInt32(txtTH1.Text) > 100)
                {
                    validationMsg.Add("Thresold is required and the range is 0 to 100.");
                }
            }

            if (string.IsNullOrEmpty(txtH1.Text))
            {
                validationMsg.Add("High Pass Filter is required and the range is 1 to 1000.");
            }
            else
            {
                if (Convert.ToInt32(txtH1.Text) < 1 || Convert.ToInt32(txtH1.Text) > 1000)
                {
                    validationMsg.Add("High Pass Filter is required and the range is 1 to 1000.");
                }
            }

            if (string.IsNullOrEmpty(txtL1.Text))
            {
                validationMsg.Add("Low Pass Filter is required and the range is 1 to 1000.");
            }
            else
            {
                if (Convert.ToInt32(txtL1.Text) < 1 || Convert.ToInt32(txtL1.Text) > 1000)
                {
                    validationMsg.Add("Low Pass Filter is required and the range is 1 to 1000.");
                }
            }

            return validationMsg;
        }

        private void PreviewTextInput_NumericOnly(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void PreviewTextInput_DecimalOnly(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            string newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);

            // Allows:
            // 123
            // 123.4
            // 0.5
            // Disallows:
            // 123.45
            // 123..
            // abc
            e.Handled = !Regex.IsMatch(newText, @"^\d+(\.\d{0,1})?$");
        }

    }
}
