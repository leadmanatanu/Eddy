using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Eddy
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PartConfig : Window
    {
        public bool IsSaved = false;
        public PartConfig()
        {
            InitializeComponent();
            if (DeviceCOM.part == null )
                DeviceCOM.part = new Part();

            
            txtBatchName.Text = "";
            txtPlace.Text = DeviceCOM.part.Placce;
            txtCheckedBy.Text = DeviceCOM.part.CheckedBy;
            txtCompanyName.Text = DeviceCOM.part.CompanyName;
            txtBatchSize.Text = DeviceCOM.part.BatchSize.ToString();
            txtGrade.Text = DeviceCOM.part.Grade;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnConfigSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var msg = Validaton();
                if (msg.Count == 0)
                {
                    DeviceCOM.part ??= new Part();
                    DeviceCOM.part.Name = txtBatchName.Text.Trim();
                    DeviceCOM.part.Placce = txtPlace.Text;
                    DeviceCOM.part.Grade = txtGrade.Text;
                    DeviceCOM.part.CheckedBy = txtCheckedBy.Text;
                    DeviceCOM.part.CompanyName = txtCompanyName.Text;
                    DeviceCOM.part.BatchSize= string.IsNullOrEmpty(txtBatchSize.Text) ? 0 : Convert.ToInt16(txtBatchSize.Text);
                    DeviceCOM.IsLogEnable = true;
                    IsSaved = true;
                    lblMsg.Content = "Log has been started!!!";
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
            catch (Exception)
            {
                lblMsg.Content = "Error while saving the Configuration!!!";
            }
        }

        public List<String> Validaton()
        {
            List<String> validationMsg = new List<string>();
            string batchName = txtBatchName.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(batchName))
            {
                validationMsg.Add("File Name is required.");
            }
            else
            {
                try
                {
                    using (var con = new NpgsqlConnection(DeviceCOM.DBConnection))
                    {
                        con.Open();
                        using (var cmd = new NpgsqlCommand("SELECT 1 FROM \"Logs\" WHERE LOWER(TRIM(\"BatchName\")) = LOWER(TRIM(@batch)) LIMIT 1;", con))
                        {
                            cmd.Parameters.AddWithValue("@batch", batchName);
                            var exists = cmd.ExecuteScalar();
                            if (exists != null)
                            {
                                validationMsg.Add("File name already used. Enter a new name.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Batch duplicate check error: {ex}");
                }
            }
            if (string.IsNullOrEmpty(txtPlace.Text))
            {
                validationMsg.Add("Place Name is required.");
            }
            
            if (string.IsNullOrEmpty(txtGrade.Text))
            {
                validationMsg.Add("Grade is required.");
            }
            

            if (string.IsNullOrEmpty(txtCompanyName.Text))
            {
                validationMsg.Add("Company Name is required.");
            }

            if (string.IsNullOrEmpty(txtCheckedBy.Text))
            {
                validationMsg.Add("Checked By is required.");
            }

            if (string.IsNullOrEmpty(txtBatchSize.Text))
            {
                validationMsg.Add("Batch Size is required and sould be greater than 0");
            }
            else
            {
                if (Convert.ToInt16(txtBatchSize.Text) <= 0)
                {
                    validationMsg.Add("Batch Size is required and sould be greater than 0");
                }
            }


            return validationMsg;
        }

        

        private void PreviewTextInput_NumericOnly(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void PreviewTextInput_NumericWithNegativeOnly(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("^[0-9]+([0-9]-)+$");
            e.Handled = regex.IsMatch(e.Text);
        }

       
    }
}
