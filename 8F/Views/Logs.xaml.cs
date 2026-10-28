using Newtonsoft.Json;
using Npgsql;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using System.IO;
using System.Reflection.Metadata;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace _8F
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Logs : Window
    {
        public bool IsSaved = false;

        public List<LogData> listOfLog = new();

        private bool IsReNewConfig =>
            _8F.Services.ConfigKey.ProtectedSettings.IsReNewConfig;

        private string BatchCaption => IsReNewConfig ? "Shift" : "Batch Name";

        public Logs()
        {
            InitializeComponent();

            ((DataGridTextColumn)grdlogs.Columns[0]).Header = BatchCaption;
            lblBatchName.Content = BatchCaption;

            clStartDate.SelectedDate = DateTime.Now;
            clToDate.SelectedDate = DateTime.Now;

            ApplyColumnVisibility();
        }

        private static bool GetConfigBool(string key, bool defaultValue = true)
        {
            return _8F.Services.ConfigKey.ProtectedSettings.GetBool(key, defaultValue);


        }

        private void ApplyColumnVisibility()
        {
            bool isTotalCountVisible = GetConfigBool("IsTotalCountVisible", true);
            bool isOkCountVisible = GetConfigBool("IsOkCountVisible", true);
            bool isNotOkCountVisible = GetConfigBool("IsNotOkCountVisible", true);

            if (!isTotalCountVisible)
            {
                var totalCol = grdlogs.Columns.FirstOrDefault(c => c.Header?.ToString() == "Total Count");
                if (totalCol != null) totalCol.Visibility = Visibility.Collapsed;
            }

            if (!isOkCountVisible)
            {
                var okCol = grdlogs.Columns.FirstOrDefault(c => c.Header?.ToString() == "OK Count" || c.Header?.ToString() == "Ok Count");
                if (okCol != null) okCol.Visibility = Visibility.Collapsed;
            }

            if (!isNotOkCountVisible)
            {
                var notOkCol = grdlogs.Columns.FirstOrDefault(c => c.Header?.ToString() == "Not OK Count" || c.Header?.ToString() == "Not Ok Count");
                if (notOkCol != null) notOkCol.Visibility = Visibility.Collapsed;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnSearch_MouseDown(object sender, MouseButtonEventArgs e)
        {
            LoadLogs();
        }

        public void LoadLogs()
        {
            var vm = new _8F.ViewModels.LogsViewModel
            {
                BatchNameFilter = txtBatchName.Text,
                StartDate = clStartDate.SelectedDate,
                EndDate = clToDate.SelectedDate
            };
            vm.LoadLogs();
            listOfLog = vm.Logs.ToList();
            grdlogs.ItemsSource = listOfLog;
        }

        private void btnDownload_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                LoadLogs();
                if (listOfLog.Count > 0)
                {
                    Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                    dlg.FileName = "Report"; // Default file name
                    dlg.DefaultExt = ".csv"; // Default file extension
                    dlg.Filter = "CSV Files (.csv)|*.csv"; // Filter files by extension

                    Nullable<bool> result = dlg.ShowDialog();

                    if (result == true)
                    {
                        string batchLabel = BatchCaption;

                        string conecnt = $"{batchLabel},Log Start Date,Log End Date,OK Count,Not OK Count,Total Count";

                        foreach (var log in listOfLog)
                        {
                            conecnt = conecnt + "\n";
                            conecnt = conecnt + log.BatchName + "," + log.LogStartDate + "," + log.LogEndDate + "," + log.PassCount.ToString() + "," + log.FailCount.ToString() + "," + log.TotalCount.ToString();
                        }
                        File.WriteAllText(dlg.FileName, conecnt);
                        MessageBox.Show("CSV Generated Successfully");
                    }
                }
            }
            catch (Exception)
            {

                MessageBox.Show("CSV generation failed.");
            }
           
        }

        private void btnDownloadPdf_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                LoadLogs();

                if (listOfLog == null || listOfLog.Count == 0)
                {
                    MessageBox.Show("No data found.");
                    return;
                }

                Microsoft.Win32.SaveFileDialog dlg =
                    new Microsoft.Win32.SaveFileDialog();

                dlg.FileName = "Report";
                dlg.DefaultExt = ".pdf";
                dlg.Filter = "PDF Files (*.pdf)|*.pdf";

                if (dlg.ShowDialog() == true)
                {
                    GeneratePdf(dlg.FileName);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }

        }

        public void GeneratePdf(string filePath)
        {
            try
            {
                var logs = listOfLog;

                QuestPDF.Settings.License = LicenseType.Community;

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(20);
                        page.Size(PageSizes.A4);

                        page.Header()
                            .ShowOnce()
                            .BorderBottom(2)
                            .BorderColor("#0D3B6E")
                            .PaddingBottom(8)
                            .Row(r =>
                            {
                                r.RelativeItem().Row(left =>
                                {
                                    var imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Magkraft.jpg");
                                    var imageBytes = File.ReadAllBytes(imagePath);

                                    left.AutoItem()
                                        .Height(25)
                                        .Width(25)
                                        .AlignMiddle()
                                        .Image(imageBytes);

                                    left.ConstantItem(10);

                                    left.AutoItem()
                                        .AlignMiddle()
                                        .Text("Sorter Eddy Report")
                                        .FontSize(18)
                                        .Bold()
                                        .FontColor("#0D3B6E");
                                });

                                r.ConstantItem(180)
                                    .AlignRight()
                                    .AlignBottom()
                                    .Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                    .FontSize(8)
                                    .FontColor("#888888");
                            });

                        page.Content()
                            .PaddingTop(10)
                            .Column(col =>
                            {
                                col.Spacing(10);

                                foreach (var log in logs)
                                {
                                    col.Item()
                                        .Border(1)
                                        .BorderColor("#CCCCCC")
                                        .Column(batch =>
                                        {
                                            // Batch Header
                                            batch.Item()
                                                .Background("#0D3B6E")
                                                .Padding(7)
                                                .PaddingLeft(12)
                                                .PaddingRight(12)
                                                .Row(r =>
                                                {
                                                    r.RelativeItem()
                                                        .Text($"{BatchCaption} : {log.BatchName}")
                                                        .FontSize(8)
                                                        .Bold()
                                                        .FontColor("#FFFFFF");

                                                    r.ConstantItem(250)
                                                        .AlignRight()
                                                        .Row(stats =>
                                                        {
                                                            stats.AutoItem()
                                                                .Text($"OK : {log.PassCount}")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor("#69F0AE");

                                                            stats.ConstantItem(10);

                                                            stats.AutoItem()
                                                                .Text($"NOT OK : {log.FailCount}")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor("#FF5252");

                                                            stats.ConstantItem(10);

                                                            stats.AutoItem()
                                                                .Text($"TOTAL : {log.TotalCount}")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor("#40C4FF");
                                                        });
                                                });

                                            var details = GetBatchDetails(log.BatchName);

                                            int recordIndex = 0;

                                            foreach (var item in details)
                                            {
                                                recordIndex++;

                                                batch.Item()
                                                    .BorderTop(1)
                                                    .BorderColor("#E8E8E8")
                                                    .Background(recordIndex % 2 == 0 ? "#FAFAFA" : "#FFFFFF")
                                                    .Padding(7)
                                                    .PaddingLeft(12)
                                                    .PaddingRight(12)
                                                    .Column(record =>
                                                    {
                                                        // PASS / FAIL row
                                                        record.Item()
                                                            .Row(r =>
                                                            {
                                                                r.RelativeItem()
                                                                    .Text($"{item.TimeStamp:dd/MM/yyyy HH:mm:ss}")
                                                                    .FontSize(9)
                                                                    .Bold()
                                                                    .FontColor("#333333");

                                                                r.ConstantItem(50)
                                                                    .AlignRight()
                                                                    .Background(item.Result ? "#E8F5E9" : "#FFEBEE")
                                                                    .Padding(2)
                                                                    .Text(item.Result ? "PASS" : "FAIL")
                                                                    .FontSize(8)
                                                                    .Bold()
                                                                    .FontColor(item.Result ? "#2E7D32" : "#C62828");
                                                            });

                                                        List<GraphData> fdList = new();

                                                        try
                                                        {
                                                             fdList = JsonConvert.DeserializeObject<List<GraphData>>(item.FDData ?? "[]") ?? new();
                                                        }
                                                        catch
                                                        {
                                                            return;
                                                        }

                                                        record.Item()
                                                         .PaddingTop(8)
                                                         .Border(1)
                                                         .BorderColor("#DDDDDD")
                                                         .Background("#FAFAFA")
                                                         .Padding(10)
                                                         .Column(section =>
                                                         {
                                                             // ================= CONFIGURATION =================

                                                             section.Item()
                                                                 .Text("CONFIGURATION SETTING")
                                                                 .FontSize(8)
                                                                 .Bold()
                                                                 .FontColor("#0D3B6E");

                                                             section.Item()
                                                                 .PaddingTop(5)
                                                                 .Table(table =>
                                                                 {
                                                                     table.ColumnsDefinition(columns =>
                                                                     {
                                                                         columns.RelativeColumn(); // Channel
                                                                         columns.RelativeColumn(); // Frequency
                                                                         columns.RelativeColumn(); // Gain
                                                                         columns.RelativeColumn(); // Phase
                                                                         columns.RelativeColumn(); // Status
                                                                     });

                                                                     table.Header(header =>
                                                                     {
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Channel").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Frequency").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Gain").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Phase").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Status").FontColor(Colors.White);
                                                                     });

                                                                     foreach (var d in fdList)
                                                                     {
                                                                         table.Cell().Border(1).Padding(3).Text(d.Name);
                                                                         table.Cell().Border(1).Padding(3).Text(d.freq.ToString());
                                                                         table.Cell().Border(1).Padding(3).Text(d.gain.ToString());
                                                                         table.Cell().Border(1).Padding(3).Text(d.phase.ToString());
                                                                         table.Cell().Border(1).Padding(3).Text(d.isEnable ? "ON" : "OFF");
                                                                     }
                                                                 });

                                                             // ================= THRESHOLD SETTING =================

                                                             section.Item()
                                                                 .PaddingTop(10)
                                                                 .Text("THRESHOLD SETTING")
                                                                 .FontSize(8)
                                                                 .Bold()
                                                                 .FontColor("#0D3B6E");

                                                             section.Item()
                                                                 .PaddingTop(5)
                                                                 .Table(table =>
                                                                 {
                                                                     table.ColumnsDefinition(columns =>
                                                                     {
                                                                         columns.RelativeColumn(); // Channel
                                                                         columns.RelativeColumn(); // Height
                                                                         columns.RelativeColumn(); // Width
                                                                         columns.RelativeColumn(); // Ex
                                                                         columns.RelativeColumn(); // Ey
                                                                         columns.RelativeColumn(); // Angle
                                                                     });

                                                                     table.Header(header =>
                                                                     {
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Channel").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Height").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Width").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Ex").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Ey").FontColor(Colors.White);
                                                                         header.Cell().Background("#0D3B6E").Padding(3).Text("Angle").FontColor(Colors.White);
                                                                     });

                                                                     foreach (var d in fdList)
                                                                     {
                                                                         table.Cell().Border(1).Padding(3).Text(d.Name);
                                                                         table.Cell().Border(1).Padding(3).Text(d.height.ToString("0.##"));
                                                                         table.Cell().Border(1).Padding(3).Text(d.width.ToString("0.##"));
                                                                         table.Cell().Border(1).Padding(3).Text(d.ex.ToString("0.##"));
                                                                         table.Cell().Border(1).Padding(3).Text(d.ey.ToString("0.##"));
                                                                         table.Cell().Border(1).Padding(3).Text(d.angel.ToString("0.##"));
                                                                     }
                                                                 });

                                                             // ================= PART CONFIGURATION =================

                                                             section.Item()
                                                                 .PaddingTop(10)
                                                                 .Text("PART CONFIGURATION")
                                                                 .FontSize(8)
                                                                 .Bold()
                                                                 .FontColor("#0D3B6E");

                                                             PartConfiguration? part = null;

                                                             try
                                                             {
                                                                 part = JsonConvert.DeserializeObject<PartConfiguration>(item.PartData ?? "{}");
                                                             }
                                                             catch
                                                             {
                                                                 part = null;
                                                             }

                                                             section.Item()
                                                                 .PaddingTop(5)
                                                                 .Column(c =>
                                                                 {
                                                                     // ================= RENEW CONFIG (6 FIELDS) =================
                                                                     if (IsReNewConfig)
                                                                     {
                                                                         // ROW 1 (3 + 3 layout)
                                                                         c.Item().Row(r =>
                                                                         {
                                                                             r.RelativeItem(1).Element(x => LV(x, "Production Order", part?.ProductionOrder));
                                                                             r.ConstantItem(10);

                                                                             r.RelativeItem(1).Element(x => LV(x, "Machine Number", part?.MachineNumber));
                                                                             r.ConstantItem(10);

                                                                             r.RelativeItem(1).Element(x => LV(x, "Part Number", part?.PartNumber));
                                                                         });

                                                                         // ROW 2 (3 + 3 layout)
                                                                         c.Item().PaddingTop(2).Row(r =>
                                                                         {
                                                                             r.RelativeItem(1).Element(x => LV(x, "Part Family", part?.PartFamily));
                                                                             r.ConstantItem(10);

                                                                             r.RelativeItem(1).Element(x => LV(x, "Shift", part?.BatchName));
                                                                             r.ConstantItem(10);

                                                                             r.RelativeItem(1).Element(x => LV(x, "Operator", part?.CheckedBy));
                                                                         });
                                                                     }

                                                                     // ================= OLD CONFIG (5 FIELDS) =================
                                                                     else
                                                                     {
                                                                         // ROW 1 (3 fields)
                                                                         c.Item().Row(r =>
                                                                         {
                                                                             r.RelativeItem(1).Element(x => LV(x, "Batch Name", part?.BatchName));
                                                                             r.ConstantItem(10);
                                                                             r.RelativeItem(1).Element(x => LV(x, "Part Name", part?.Name));
                                                                             r.ConstantItem(10);
                                                                             r.RelativeItem(1).Element(x => LV(x, "Grade", part?.Grade));
                                                                         });

                                                                         c.Item().PaddingTop(2).Row(r =>
                                                                         {
                                                                             r.RelativeItem(1).Element(x => LV(x, "Checked By", part?.CheckedBy));
                                                                             r.ConstantItem(10);
                                                                             r.RelativeItem(1).Element(x => LV(x, "Company Name", part?.CompanyName));
                                                                             r.ConstantItem(10);
                                                                             r.RelativeItem(1).Element(x => LV(x, "", ""));
                                                                         });
                                                                     }
                                                                 });



                                                         });
                                                    });
                                            }
                                        });
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                    });
                })
                .GeneratePdf(filePath);

                MessageBox.Show("PDF Generated Successfully");
            }
            catch
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }
        }

        private List<BatchDetail> GetBatchDetails(string batchName)
        {

            List<BatchDetail> list = new List<BatchDetail>();

            using (var con = new NpgsqlConnection(
                System.Configuration.ConfigurationManager.AppSettings["ConnectionString"]))
            {
                con.Open();

                string sql =
                @"SELECT
                    ""TimeStamp"",
                    ""BatchName"",
                    ""Result"",
                    ""FDData"",
                    ""PartData"",
                    COALESCE(""FrequencyData""::text, ""ResultData""::text, '[]') AS ""FrequencyData""
                FROM public.""Logs""
                WHERE ""BatchName""=@BatchName
                ORDER BY ""TimeStamp""";


                NpgsqlCommand cmd = new NpgsqlCommand(sql, con);

                cmd.Parameters.AddWithValue("@BatchName", batchName);

                DataTable dt = new DataTable();
                dt.Load(cmd.ExecuteReader());

                foreach (DataRow row in dt.Rows)
                {
                    list.Add(new BatchDetail()
                    {
                        TimeStamp = Convert.ToDateTime(row["TimeStamp"]),
                        BatchName = row["BatchName"]?.ToString() ?? string.Empty,
                        Result = Convert.ToBoolean(row["Result"]),
                        FDData = row["FDData"]?.ToString() ?? string.Empty,
                        PartData = row["PartData"]?.ToString() ?? string.Empty,
                        FrequencyData = row["FrequencyData"]?.ToString() ?? string.Empty,
                        ResultData = row["FrequencyData"]?.ToString() ?? string.Empty
                    });
                }
            }

            return list;
        }

        private void btnRowDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button? btn = sender as Button;

                LogData? log = btn?.Tag as LogData;

                if (log == null)
                    return;

                Microsoft.Win32.SaveFileDialog dlg =
                    new Microsoft.Win32.SaveFileDialog();

                dlg.FileName = log.BatchName;
                dlg.DefaultExt = ".pdf";
                dlg.Filter = "PDF Files (*.pdf)|*.pdf";

                if (dlg.ShowDialog() == true)
                {
                    GeneratePdf(log, dlg.FileName);
                }
            }
            catch
            {
                MessageBox.Show("Something went wrong.");
            }
        }

        public void GeneratePdf(LogData log, string filePath)
        {
            try
            {
                var details = GetBatchDetails(log.BatchName);

                QuestPDF.Settings.License = LicenseType.Community;

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(20);
                        page.Size(PageSizes.A4);

                        page.Header()
                            .ShowOnce()
                            .BorderBottom(2)
                            .BorderColor("#0D3B6E")
                            .PaddingBottom(8)
                            .Row(r =>
                            {
                                r.RelativeItem().Row(left =>
                                {
                                    var imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Magkraft.jpg");
                                    var imageBytes = File.ReadAllBytes(imagePath);

                                    left.AutoItem()
                                        .Height(25)
                                        .Width(25)
                                        .AlignMiddle()
                                        .Image(imageBytes);

                                    left.ConstantItem(10);

                                    left.AutoItem()
                                        .AlignMiddle()
                                        .Text("Sorter Eddy Report")
                                        .FontSize(18)
                                        .Bold()
                                        .FontColor("#0D3B6E");
                                });

                                r.ConstantItem(180)
                                    .AlignRight()
                                    .AlignBottom()
                                    .Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                    .FontSize(8)
                                    .FontColor("#888888");
                            });

                        page.Content()
                            .PaddingTop(10)
                            .Column(col =>
                            {
                                col.Item()
                                    .Border(1)
                                    .BorderColor("#CCCCCC")
                                    .Column(batch =>
                                    {
                                        // Batch Header
                                        batch.Item()
                                            .Background("#0D3B6E")
                                            .Padding(7)
                                            .PaddingLeft(12)
                                            .PaddingRight(12)
                                            .Row(r =>
                                            {
                                                r.RelativeItem()
                                                    .Text($"{BatchCaption} : {log.BatchName}")
                                                    .FontSize(8)
                                                    .Bold()
                                                    .FontColor("#FFFFFF");

                                                r.ConstantItem(250)
                                                    .AlignRight()
                                                    .Row(stats =>
                                                    {
                                                        stats.AutoItem()
                                                            .Text($"OK : {log.PassCount}")
                                                            .FontSize(8)
                                                            .Bold()
                                                            .FontColor("#69F0AE");

                                                        stats.ConstantItem(10);

                                                        stats.AutoItem()
                                                            .Text($"NOT OK : {log.FailCount}")
                                                            .FontSize(8)
                                                            .Bold()
                                                            .FontColor("#FF5252");

                                                        stats.ConstantItem(10);

                                                        stats.AutoItem()
                                                            .Text($"TOTAL : {log.TotalCount}")
                                                            .FontSize(8)
                                                            .Bold()
                                                            .FontColor("#40C4FF");
                                                    });
                                            });

                                        int recordIndex = 0;

                                        foreach (var item in details)
                                        {
                                            recordIndex++;

                                            batch.Item()
                                                .BorderTop(1)
                                                .BorderColor("#E8E8E8")
                                                .Background(recordIndex % 2 == 0 ? "#FAFAFA" : "#FFFFFF")
                                                .Padding(7)
                                                .PaddingLeft(12)
                                                .PaddingRight(12)
                                                .Column(record =>
                                                {
                                                    // PASS / FAIL
                                                    record.Item()
                                                        .Row(r =>
                                                        {
                                                            r.RelativeItem()
                                                                .Text($"{item.TimeStamp:dd/MM/yyyy HH:mm:ss}")
                                                                .FontSize(9)
                                                                .Bold()
                                                                .FontColor("#333333");

                                                            r.ConstantItem(50)
                                                                .AlignRight()
                                                                .Background(item.Result ? "#E8F5E9" : "#FFEBEE")
                                                                .Padding(2)
                                                                .Text(item.Result ? "PASS" : "FAIL")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor(item.Result ? "#2E7D32" : "#C62828");
                                                        });

                                                    List<GraphData> fdList = new();

                                                    try
                                                    {
                                                        fdList = JsonConvert.DeserializeObject<List<GraphData>>(item.FDData ?? "[]") ?? new();
                                                    }
                                                    catch
                                                    {
                                                        return;
                                                    }

                                                    record.Item()
                                                   .PaddingTop(8)
                                                   .Border(1)
                                                   .BorderColor("#DDDDDD")
                                                   .Background("#FAFAFA")
                                                   .Padding(10)
                                                   .Column(section =>
                                                   {
                                                       // ================= CONFIGURATION =================

                                                       section.Item()
                                                           .Text("CONFIGURATION SETTING")
                                                           .FontSize(8)
                                                           .Bold()
                                                           .FontColor("#0D3B6E");

                                                       section.Item()
                                                           .PaddingTop(5)
                                                           .Table(table =>
                                                           {
                                                               table.ColumnsDefinition(columns =>
                                                               {
                                                                   columns.RelativeColumn(); // Channel
                                                                   columns.RelativeColumn(); // Frequency
                                                                   columns.RelativeColumn(); // Gain
                                                                   columns.RelativeColumn(); // Phase
                                                                   columns.RelativeColumn(); // Status
                                                               });

                                                               table.Header(header =>
                                                               {
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Channel").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Frequency").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Gain").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Phase").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Status").FontColor(Colors.White);
                                                               });

                                                               foreach (var d in fdList)
                                                               {
                                                                   table.Cell().Border(1).Padding(3).Text(d.Name);
                                                                   table.Cell().Border(1).Padding(3).Text(d.freq.ToString());
                                                                   table.Cell().Border(1).Padding(3).Text(d.gain.ToString());
                                                                   table.Cell().Border(1).Padding(3).Text(d.phase.ToString());
                                                                   table.Cell().Border(1).Padding(3).Text(d.isEnable ? "ON" : "OFF");
                                                               }
                                                           });

                                                       // ================= THRESHOLD SETTING =================

                                                       section.Item()
                                                           .PaddingTop(10)
                                                           .Text("THRESHOLD SETTING")
                                                           .FontSize(8)
                                                           .Bold()
                                                           .FontColor("#0D3B6E");

                                                       section.Item()
                                                           .PaddingTop(5)
                                                           .Table(table =>
                                                           {
                                                               table.ColumnsDefinition(columns =>
                                                               {
                                                                   columns.RelativeColumn(); // Channel
                                                                   columns.RelativeColumn(); // Height
                                                                   columns.RelativeColumn(); // Width
                                                                   columns.RelativeColumn(); // Ex
                                                                   columns.RelativeColumn(); // Ey
                                                                   columns.RelativeColumn(); // Angle
                                                               });

                                                               table.Header(header =>
                                                               {
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Channel").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Height").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Width").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Ex").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Ey").FontColor(Colors.White);
                                                                   header.Cell().Background("#0D3B6E").Padding(3).Text("Angle").FontColor(Colors.White);
                                                               });

                                                               foreach (var d in fdList)
                                                               {
                                                                   table.Cell().Border(1).Padding(3).Text(d.Name);
                                                                   table.Cell().Border(1).Padding(3).Text(d.height.ToString("0.##"));
                                                                   table.Cell().Border(1).Padding(3).Text(d.width.ToString("0.##"));
                                                                   table.Cell().Border(1).Padding(3).Text(d.ex.ToString("0.##"));
                                                                   table.Cell().Border(1).Padding(3).Text(d.ey.ToString("0.##"));
                                                                   table.Cell().Border(1).Padding(3).Text(d.angel.ToString("0.##"));
                                                               }
                                                           });


                                                       // ================= PART CONFIGURATION =================

                                                       section.Item()
                                                           .PaddingTop(10)
                                                           .Text("PART CONFIGURATION")
                                                           .FontSize(8)
                                                           .Bold()
                                                           .FontColor("#0D3B6E");

                                                       PartConfiguration? part = null;

                                                       try
                                                       {
                                                           part = JsonConvert.DeserializeObject<PartConfiguration>(item.PartData ?? "{}");
                                                       }
                                                       catch
                                                       {
                                                           part = null;
                                                       }

                                                       section.Item()
                                                           .PaddingTop(5)
                                                           .Column(c =>
                                                           {
                                                               // ================= RENEW CONFIG (6 FIELDS) =================
                                                               if (IsReNewConfig)
                                                               {
                                                                   // ROW 1 (3 + 3 layout)
                                                                   c.Item().Row(r =>
                                                                   {
                                                                       r.RelativeItem(1).Element(x => LV(x, "Production Order", part?.ProductionOrder));
                                                                       r.ConstantItem(10);

                                                                       r.RelativeItem(1).Element(x => LV(x, "Machine Number", part?.MachineNumber));
                                                                       r.ConstantItem(10);

                                                                       r.RelativeItem(1).Element(x => LV(x, "Part Number", part?.PartNumber));
                                                                   });

                                                                   // ROW 2 (3 + 3 layout)
                                                                   c.Item().PaddingTop(2).Row(r =>
                                                                   {
                                                                       r.RelativeItem(1).Element(x => LV(x, "Part Family", part?.PartFamily));
                                                                       r.ConstantItem(10);

                                                                       r.RelativeItem(1).Element(x => LV(x, "Shift", part?.BatchName));
                                                                       r.ConstantItem(10);

                                                                       r.RelativeItem(1).Element(x => LV(x, "Operator", part?.CheckedBy));
                                                                   });
                                                               }

                                                               // ================= OLD CONFIG (5 FIELDS) =================
                                                               else
                                                               {
                                                                   // ROW 1 (3 fields)
                                                                   c.Item().Row(r =>
                                                                   {
                                                                       r.RelativeItem(1).Element(x => LV(x, "Batch Name", part?.BatchName));
                                                                       r.ConstantItem(10);
                                                                       r.RelativeItem(1).Element(x => LV(x, "Part Name", part?.Name));
                                                                       r.ConstantItem(10);
                                                                       r.RelativeItem(1).Element(x => LV(x, "Grade", part?.Grade));
                                                                   });

                                                                   c.Item().PaddingTop(2).Row(r =>
                                                                   {
                                                                       r.RelativeItem(1).Element(x => LV(x, "Checked By", part?.CheckedBy));
                                                                       r.ConstantItem(10);
                                                                       r.RelativeItem(1).Element(x => LV(x, "Company Name", part?.CompanyName));
                                                                       r.ConstantItem(10);
                                                                       r.RelativeItem(1).Element(x => LV(x, "", ""));
                                                                   });
                                                               }
                                                           });




                                                   });
                                                });
                                        }
                                    });
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                    });
                })
                .GeneratePdf(filePath);

                MessageBox.Show("PDF Generated Successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void LV(IContainer c, string label, string? value)
        {
            c.Text(t =>
            {
                t.Span($"{label}: ").FontSize(7).FontColor("#888888");
                t.Span(value ?? "-").FontSize(7).Bold().FontColor("#111111");
            });
        }
    }
}
