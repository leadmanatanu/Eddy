using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
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
    /// Interaction logic for Logs.xaml
    /// </summary>
    public partial class Logs : Window
    {
        public bool IsSaved = false;
        public List<LogData> listOfLog = new List<LogData>();
        public Logs()
        {
            InitializeComponent();
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
            try
            {
                if (clStartDate.SelectedDate == null || clToDate.SelectedDate == null)
                {
                    MessageBox.Show("Please select date range");
                    return;
                }

                listOfLog = new List<LogData>();
               
                if (string.IsNullOrEmpty(DeviceCOM.DBConnection))
                {
                    DeviceCOM.DBConnection = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["DBConnection"]) ?? "";
                }

                using (var con = new NpgsqlConnection(DeviceCOM.DBConnection))
                {
                    string sql = @"
                        SELECT 
                            ""BatchName"" AS ""BatchName"",
                            MIN(""TimeStamp"") AS ""StartDate"",
                            MAX(""TimeStamp"") AS ""EndDate"",
                            COUNT(*) FILTER (WHERE ""Result"" = true) AS ""PassCount"",
                            COUNT(*) FILTER (WHERE ""Result"" = false) AS ""FailCount""
                        FROM ""Logs""
                        WHERE 
                            ""BatchName"" ILIKE '%' || @BatchName || '%'
                            AND ""TimeStamp"" >= @StartDate
                            AND ""TimeStamp"" < @EndDate
                        GROUP BY ""BatchName""
                        ORDER BY ""StartDate"";
                    ";

                    con.Open();

                    var cmd = new NpgsqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@BatchName", txtBatchName.Text ?? "");
                    cmd.Parameters.AddWithValue("@StartDate", clStartDate.SelectedDate.Value);
                    cmd.Parameters.AddWithValue("@EndDate", clToDate.SelectedDate.Value.AddDays(1));

                    var reader = cmd.ExecuteReader();

                    DataTable dt = new DataTable();
                    dt.Load(reader);

                    foreach (DataRow row in dt.Rows)
                    {
                        try
                        {
                            LogData log = new LogData
                            {
                                BatchName = row["BatchName"]?.ToString() ?? "",
                                LogStartDate = Convert.ToDateTime(row["StartDate"]).ToLocalTime().ToString("dd/MM/yy HH:mm:ss"),
                                LogEndDate = Convert.ToDateTime(row["EndDate"]).ToLocalTime().ToString("dd/MM/yy HH:mm:ss"),
                                PassCount = Convert.ToInt32(row["PassCount"]),
                                FailCount = Convert.ToInt32(row["FailCount"])
                            };

                            log.TotalCount = log.PassCount + log.FailCount;

                            listOfLog.Add(log);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Something went wrong. Please try again.");
                        }
                    }

                    grdlogs.ItemsSource = listOfLog;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Something went wrong. Please try again");
            }
        }

        //private void btnDownload_MouseDown(object sender, MouseButtonEventArgs e)
        //{
        //    try
        //    {
        //        LoadLogs();
        //        if (listOfLog.Count > 0)
        //        {
        //            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
        //            dlg.FileName = "Report"; // Default file name
        //            dlg.DefaultExt = ".csv"; // Default file extension
        //            dlg.Filter = "CSV Files (.csv)|*.csv"; // Filter files by extension

        //            Nullable<bool> result = dlg.ShowDialog();

        //            if (result == true)
        //            {
        //                string conecnt = "Batch Name,Log Start Date,Log End Date,OK Count,Not OK Count,Total Count";
        //                foreach (var log in listOfLog)
        //                {
        //                    conecnt = conecnt + "\n";
        //                    conecnt = conecnt + log.BatchName + "," + log.LogStartDate + "," + log.LogEndDate + "," + log.PassCount.ToString() + "," + log.FailCount.ToString() + "," + log.TotalCount.ToString();
        //                }
        //                File.WriteAllText(dlg.FileName, conecnt);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("Something went wrong. Please try again.");
        //    }
        //}

        private void btnDownload_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                LoadLogs();

                if (listOfLog == null || listOfLog.Count == 0)
                {
                    MessageBox.Show("No data found");
                    return;
                }

                Microsoft.Win32.SaveFileDialog dlg =
                    new Microsoft.Win32.SaveFileDialog();

                dlg.FileName = "BatchSummaryReport";
                dlg.DefaultExt = ".pdf";
                dlg.Filter = "PDF Files (.pdf)|*.pdf";

                if (dlg.ShowDialog() != true)
                    return;

                var summaryDetails = GetBatchSummaryDetails();

                QuestPDF.Settings.License = LicenseType.Community;

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(20);
                        page.Size(PageSizes.A4);

                        // HEADER
                        page.Header().ShowOnce()
                            .BorderBottom(2)
                            .BorderColor("#0D3B6E")
                            .PaddingBottom(8)
                            .Row(r =>
                            {
                                r.RelativeItem().Row(left =>
                                {
                                    var imagePath = System.IO.Path.Combine(
                                        AppDomain.CurrentDomain.BaseDirectory,
                                        "Assets",
                                        "Magkraft.jpg");

                                    var imageBytes = File.ReadAllBytes(imagePath);

                                    left.AutoItem()
                                        .Height(25)
                                        .Width(25)
                                        .AlignMiddle()
                                        .Image(imageBytes).FitHeight();

                                    left.ConstantItem(12);

                                    left.AutoItem().AlignMiddle()
                                        .Text("TUBE EDDY REPORT")
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

                        page.Content().PaddingTop(10).Column(col =>
                        {
                            col.Spacing(10);

                            foreach (var log in listOfLog)
                            {
                                try
                                {
                                    var batchData = summaryDetails
                                        .FirstOrDefault(x =>
                                            x.BatchName == log.BatchName);

                                    if (batchData == default)
                                        continue;

                                    var config =
                                        JsonConvert.DeserializeObject<Configuration>(
                                            batchData.ConfigurationJson);

                                    var part =
                                        JsonConvert.DeserializeObject<Part>(
                                            batchData.PartJson);

                                    col.Item()
                                        .Border(1)
                                        .BorderColor("#CCCCCC")
                                        .Column(batch =>
                                        {
                                            // BATCH HEADER
                                            batch.Item()
                                                .Background("#0D3B6E")
                                                .Padding(7)
                                                .PaddingLeft(12)
                                                .PaddingRight(12)
                                                .Row(r =>
                                                {
                                                    r.RelativeItem()
                                                        .Text($"File Name: {log.BatchName}")
                                                        .FontSize(8)
                                                        .Bold()
                                                        .FontColor("#FFFFFF");

                                                    r.ConstantItem(420)
                                                        .AlignRight()
                                                        .Row(statsRow =>
                                                        {
                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("Start: ")
                                                                .FontSize(8)
                                                                .FontColor("#ccd6e0");

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text($"{log.LogStartDate}")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor("#FFFFFF");

                                                            statsRow.ConstantItem(5);

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("|")
                                                                .FontSize(8)
                                                                .FontColor("#4a6a8a");

                                                            statsRow.ConstantItem(5);

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("End: ")
                                                                .FontSize(8)
                                                                .FontColor("#ccd6e0");

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text($"{log.LogEndDate}")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor("#FFFFFF");

                                                            statsRow.ConstantItem(5);

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("|")
                                                                .FontSize(8)
                                                                .FontColor("#4a6a8a");

                                                            statsRow.ConstantItem(5);

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("Pass: ")
                                                                .FontSize(8)
                                                                .FontColor("#ccd6e0");

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text($"{log.PassCount}")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor("#69F0AE");

                                                            statsRow.ConstantItem(5);

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("|")
                                                                .FontSize(8)
                                                                .FontColor("#4a6a8a");

                                                            statsRow.ConstantItem(5);

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("Fail: ")
                                                                .FontSize(8)
                                                                .FontColor("#ccd6e0");

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text($"{log.FailCount}")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor("#FF5252");

                                                            statsRow.ConstantItem(5);

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("|")
                                                                .FontSize(8)
                                                                .FontColor("#4a6a8a");

                                                            statsRow.ConstantItem(5);

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text("Total: ")
                                                                .FontSize(8)
                                                                .FontColor("#ccd6e0");

                                                            statsRow.AutoItem()
                                                                .AlignMiddle()
                                                                .Text($"{log.TotalCount}")
                                                                .FontSize(8)
                                                                .Bold()
                                                                .FontColor("#40C4FF");
                                                        });
                                                });

                                            // COMMON LABEL FUNCTION
                                            void LV(IContainer c,
                                                    string label,
                                                    string value)
                                            {
                                                c.Text(t =>
                                                {
                                                    t.Span($"{label}: ")
                                                        .FontSize(7)
                                                        .FontColor("#888888");

                                                    t.Span(value ?? "-")
                                                        .FontSize(7)
                                                        .Bold()
                                                        .FontColor("#111111");
                                                });
                                            }

                                            // PART DETAILS
                                            if (part != null)
                                            {
                                                batch.Item()
                                                    .Padding(7)
                                                    .PaddingLeft(12)
                                                    .PaddingRight(12)
                                                    .Column(record =>
                                                    {
                                                        record.Item()
                                                            .Text("PART DETAILS")
                                                            .FontSize(6.5f)
                                                            .Bold()
                                                            .FontColor("#888888");

                                                        record.Item()
                                                            .PaddingTop(2)
                                                            .Row(r =>
                                                            {
                                                                r.RelativeItem()
                                                                    .Element(c =>
                                                                        LV(c,
                                                                            "Grade",
                                                                            part.Grade));

                                                                r.RelativeItem()
                                                                    .Element(c =>
                                                                        LV(c,
                                                                            "Place",
                                                                            part.Placce));

                                                                r.RelativeItem()
                                                                    .Element(c =>
                                                                        LV(c,
                                                                            "Checked By",
                                                                            part.CheckedBy));

                                                                r.RelativeItem()
                                                                    .Element(c =>
                                                                        LV(c,
                                                                            "Company",
                                                                            part.CompanyName));
                                                            });
                                                    });
                                            }

                                            // FREQUENCY & FILTER
                                            if (config?.Frequency?.FD != null &&
                                                config?.Filter?.FD != null)
                                            {
                                                var uniqueFD = config.Frequency.FD
                                                    .GroupBy(f => new
                                                    {
                                                        f.F,
                                                        f.G,
                                                        f.UTH,
                                                        f.LTH,
                                                        f.TH,
                                                        f.PP
                                                    })
                                                    .Select(g => g.First())
                                                    .ToList();

                                                foreach (var f in uniqueFD)
                                                {
                                                    try
                                                    {
                                                        var filterRow =
                                                            config.Filter.FD
                                                            .FirstOrDefault(fd =>
                                                                fd.FN == f.FN);

                                                        batch.Item()
                                                            .Padding(7)
                                                            .PaddingLeft(12)
                                                            .PaddingRight(12)
                                                            .Column(record =>
                                                            {
                                                                record.Item()
                                                                    .Text("FREQUENCY & FILTER")
                                                                    .FontSize(6.5f)
                                                                    .Bold()
                                                                    .FontColor("#888888");

                                                                record.Item()
                                                                    .PaddingTop(2)
                                                                    .Row(r =>
                                                                    {
                                                                        r.RelativeItem()
                                                                            .Element(c =>
                                                                                LV(c,
                                                                                    "Frequency(KHz)",
                                                                                    (f.F / 1000).ToString()));

                                                                        r.RelativeItem()
                                                                            .Element(c =>
                                                                                LV(c,
                                                                                    "Pre Gain(dB)",
                                                                                    f.G.ToString()));

                                                                        r.RelativeItem()
                                                                            .Element(c =>
                                                                                LV(c,
                                                                                    "Phase",
                                                                                    f.PP.ToString()));

                                                                        r.RelativeItem()
                                                                            .Element(c =>
                                                                                LV(c,
                                                                                    "High Threshold",
                                                                                    f.UTH.ToString()));
                                                                    });

                                                                record.Item()
                                                                    .PaddingTop(1)
                                                                    .Row(r =>
                                                                    {
                                                                        r.RelativeItem()
                                                                            .Element(c =>
                                                                                LV(c,
                                                                                    "Low Threshold",
                                                                                    f.LTH.ToString()));

                                                                        r.RelativeItem()
                                                                            .Element(c =>
                                                                                LV(c,
                                                                                    "Third Threshold",
                                                                                    f.TH.ToString()));

                                                                        r.RelativeItem()
                                                                            .Element(c =>
                                                                                LV(c,
                                                                                    "High Pass Filter",
                                                                                    filterRow?.H.ToString() ?? "-"));

                                                                        r.RelativeItem()
                                                                            .Element(c =>
                                                                                LV(c,
                                                                                    "Low Pass Filter",
                                                                                    filterRow?.L.ToString() ?? "-"));
                                                                    });
                                                            });
                                                    }
                                                    catch
                                                    {
                                                        MessageBox.Show("Something went wrong. Please try again.");
                                                    }
                                                }
                                            }
                                        });
                                }
                                catch
                                {
                                    MessageBox.Show("Something went wrong. Please try again.");
                                }
                            }
                        });

                        // FOOTER
                        page.Footer()
                            .AlignCenter()
                            .PaddingTop(4)
                            .Text(x =>
                            {
                                x.Span("Page ").FontSize(8);
                                x.CurrentPageNumber().FontSize(8);
                                x.Span(" of ").FontSize(8);
                                x.TotalPages().FontSize(8);
                            });
                    });
                })
                .GeneratePdf(dlg.FileName);

                MessageBox.Show("PDF Generated Successfully");
            }
            catch
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }
        }

        private void btnDownloadPdf_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                LoadLogs();

                if (listOfLog == null || listOfLog.Count == 0)
                {
                    MessageBox.Show("No data found");
                    return;
                }

                if (clStartDate.SelectedDate == null || clToDate.SelectedDate == null)
                {
                    MessageBox.Show("Please select date range");
                    return;
                }

                var startDate = clStartDate.SelectedDate.Value;
                var endDate = clToDate.SelectedDate.Value;

                string fileName = startDate == endDate
                    ? $"ProductionReport_{startDate:ddMMM-yyyy}"
                    : $"ProductionReport_{startDate:ddMMMyyyy}_to_{endDate:ddMMMyyyy}";

                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = fileName;
                dlg.DefaultExt = ".pdf";
                dlg.Filter = "PDF Files (.pdf)|*.pdf";

                if (dlg.ShowDialog() == true)
                {
                    GeneratePdf(dlg.FileName);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to generate report.");
            }
        }

        private void ShowNonModalMessage(string message)
        {
            try
            {
                void Show()
                {
                    var win = new Window
                    {
                        Title = "Report Generated",
                        Width = 450,
                        Height = 130,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ResizeMode = ResizeMode.NoResize,
                        Topmost = true,
                        Background = Brushes.WhiteSmoke,
                        Content = new Border
                        {
                            Padding = new Thickness(15),
                            Child = new TextBlock
                            {
                                Text = message,
                                FontSize = 13,
                                Foreground = Brushes.Black,
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    };
                    win.Show();
                    Task.Delay(5000).ContinueWith(_ => win.Dispatcher.Invoke(() => win.Close()));
                }

                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    Application.Current.Dispatcher.InvokeAsync(Show);
                }
                else
                {
                    Console.WriteLine(message);
                }
            }
            catch
            {
                Console.WriteLine(message);
            }
        }

        private void GenerateSingleConfigPdf(
            string targetPath,
            string batchName,
            Configuration? config,
            List<(DateTime TimeStamp, string ConfigurationJson, string GraphDataJson, bool Result, string BatchName, string PartJson)> records)
        {
            var firstTube = records.FirstOrDefault();
            var lastTube = records.LastOrDefault();
            string startStr = firstTube != default ? firstTube.TimeStamp.ToString("dd/MM/yy HH:mm:ss") : "-";
            string endStr = lastTube != default ? lastTube.TimeStamp.ToString("dd/MM/yy HH:mm:ss") : "-";
            int passCount = records.Count(r => r.Result);
            int failCount = records.Count(r => !r.Result);
            int totalCount = records.Count;

            JObject? partJsonObj = null;
            try
            {
                if (firstTube != default && !string.IsNullOrEmpty(firstTube.PartJson))
                    partJsonObj = JObject.Parse(firstTube.PartJson);
            }
            catch { }

            string GetPartField(JObject? obj, params string[] names)
            {
                if (obj == null) return "N/A";
                foreach (var n in names)
                {
                    var val = obj[n]?.ToString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
                return "N/A";
            }

            string partCompany = GetPartField(partJsonObj, "CompanyName", "Company");
            string partGrade = GetPartField(partJsonObj, "Grade");
            string partPlace = GetPartField(partJsonObj, "Placce", "Place");
            string partCheckedBy = GetPartField(partJsonObj, "CheckedBy");
            string partBatchSize = GetPartField(partJsonObj, "BatchSize");
            string partNumber = GetPartField(partJsonObj, "PartNumber");

            void LV(IContainer c, string label, string value)
            {
                c.Text(t =>
                {
                    t.Span($"{label}: ").FontSize(7).FontColor("#888888");
                    t.Span(value ?? "-").FontSize(7).Bold().FontColor("#111111");
                });
            }

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);

                    page.Header().ShowOnce().BorderBottom(2).BorderColor("#0D3B6E").PaddingBottom(8).Row(r =>
                    {
                        r.RelativeItem().Row(left =>
                        {
                            var imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Magkraft.jpg");
                            if (File.Exists(imagePath))
                            {
                                var imageBytes = File.ReadAllBytes(imagePath);
                                left.AutoItem()
                                    .Height(25)
                                    .Width(25)
                                    .AlignMiddle()
                                    .Image(imageBytes).FitHeight();
                            }

                            left.ConstantItem(12);

                            left.AutoItem().AlignMiddle()
                                .Text("TUBE EDDY REPORT")
                                .FontSize(18)
                                .Bold()
                                .FontColor("#0D3B6E");
                        });

                        r.ConstantItem(180).AlignRight().AlignBottom()
                            .Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8)
                            .FontColor("#888888");
                    });

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Item().Border(1).BorderColor("#CCCCCC").Column(batch =>
                        {
                            // Batch header
                            batch.Item()
                                .Background("#0D3B6E")
                                .Padding(7).PaddingLeft(12).PaddingRight(12)
                                .Row(r =>
                                {
                                    r.RelativeItem().Text($"File Name: {batchName}")
                                        .FontSize(8).Bold().FontColor("#FFFFFF");

                                    r.ConstantItem(420).AlignRight().Row(statsRow =>
                                    {
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("Start: ").FontSize(8).FontColor("#ccd6e0");
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text($"{startStr}").FontSize(8).Bold().FontColor("#FFFFFF");

                                        statsRow.ConstantItem(5);
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("|").FontSize(8).FontColor("#4a6a8a");
                                        statsRow.ConstantItem(5);

                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("End: ").FontSize(8).FontColor("#ccd6e0");
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text($"{endStr}").FontSize(8).Bold().FontColor("#FFFFFF");

                                        statsRow.ConstantItem(5);
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("|").FontSize(8).FontColor("#4a6a8a");
                                        statsRow.ConstantItem(5);

                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("Pass: ").FontSize(8).FontColor("#ccd6e0");
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text($"{passCount}").FontSize(8).Bold().FontColor("#69F0AE");

                                        statsRow.ConstantItem(5);
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("|").FontSize(8).FontColor("#4a6a8a");
                                        statsRow.ConstantItem(5);

                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("Fail: ").FontSize(8).FontColor("#ccd6e0");
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text($"{failCount}").FontSize(8).Bold().FontColor("#FF5252");

                                        statsRow.ConstantItem(5);
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("|").FontSize(8).FontColor("#4a6a8a");
                                        statsRow.ConstantItem(5);

                                        statsRow.AutoItem().AlignMiddle()
                                            .Text("Total: ").FontSize(8).FontColor("#ccd6e0");
                                        statsRow.AutoItem().AlignMiddle()
                                            .Text($"{totalCount}").FontSize(8).Bold().FontColor("#40C4FF");
                                    });
                                });

                            // BATCH CONFIGURATION
                            batch.Item()
                                .Padding(7).PaddingLeft(12).PaddingRight(12)
                                .Column(batchCol =>
                                {
                                    batchCol.Item().Text("BATCH CONFIGURATION")
                                        .FontSize(6.5f).Bold().FontColor("#888888");

                                    batchCol.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.RelativeItem().Element(c => LV(c, "Company Name", partCompany));
                                        r.RelativeItem().Element(c => LV(c, "Grade", partGrade));
                                        r.RelativeItem().Element(c => LV(c, "Place", partPlace));
                                        r.RelativeItem().Element(c => LV(c, "Checked By", partCheckedBy));
                                    });

                                    batchCol.Item().PaddingTop(1).Row(r =>
                                    {
                                        r.RelativeItem().Element(c => LV(c, "Batch Size", partBatchSize));
                                        r.RelativeItem().Element(c => LV(c, "Part Number", partNumber));
                                        r.RelativeItem();
                                        r.RelativeItem();
                                    });
                                });

                            batch.Item()
                                .BorderTop(1).BorderColor("#E8E8E8")
                                .Padding(7).PaddingLeft(12).PaddingRight(12)
                                .Column(record =>
                                {
                                    // FREQUENCY & FILTER
                                    if (config?.Frequency?.FD != null && config?.Filter?.FD != null)
                                    {
                                        var freqEntries = config.Frequency.FD.Where(f => f.FN != 3).ToList();
                                        if (freqEntries.Count == 0 && config.Frequency.FD.Count > 0)
                                        {
                                            freqEntries.Add(config.Frequency.FD[0]);
                                        }

                                        var uniqueFD = freqEntries
                                            .GroupBy(f => new { f.F, f.G, f.UTH, f.LTH, f.TH, f.PP })
                                            .Select(g => g.First())
                                            .ToList();

                                        foreach (var f in uniqueFD)
                                        {
                                            try
                                            {
                                                var filterRow = config.Filter.FD.FirstOrDefault(fd => fd.FN == f.FN);

                                                record.Item().PaddingTop(5).Text("FREQUENCY & FILTER")
                                                    .FontSize(6.5f).Bold().FontColor("#888888");

                                                record.Item().PaddingTop(2).Row(r =>
                                                {
                                                    r.RelativeItem().Element(c => LV(c, "Frequency(KHz)", (f.F / 1000).ToString()));
                                                    r.RelativeItem().Element(c => LV(c, "Pre Gain(dB)", f.G.ToString()));
                                                    r.RelativeItem().Element(c => LV(c, "Phase", f.PP.ToString()));
                                                    r.RelativeItem().Element(c => LV(c, "High Threshold", f.UTH.ToString()));
                                                });

                                                record.Item().PaddingTop(1).Row(r =>
                                                {
                                                    r.RelativeItem().Element(c => LV(c, "Low Threshold", f.LTH.ToString()));
                                                    r.RelativeItem().Element(c => LV(c, "Third Threshold", f.TH.ToString()));
                                                    r.RelativeItem().Element(c => LV(c, "High Pass Filter", filterRow?.H.ToString() ?? "-"));
                                                    r.RelativeItem().Element(c => LV(c, "Low Pass Filter", filterRow?.L.ToString() ?? "-"));
                                                });
                                            }
                                            catch (Exception) { }
                                        }
                                    }

                                    // MARKER
                                    if (config?.Marker != null)
                                    {
                                        var m = config.Marker;

                                        record.Item().PaddingTop(5).Text("MARKER")
                                            .FontSize(6.5f).Bold().FontColor("#888888");

                                        record.Item().PaddingTop(2).Row(r =>
                                        {
                                            r.RelativeItem().Element(c => LV(c, "Marker1(ms)", m.M1.ToString()));
                                            r.RelativeItem().Element(c => LV(c, "Marker2(ms)", m.M2.ToString()));
                                            r.RelativeItem().Element(c => LV(c, "Forward Run Time(ms)", m.FmS.ToString()));
                                            r.RelativeItem().Element(c => LV(c, "Reverse Run Time(ms)", m.RmS.ToString()));
                                        });

                                        record.Item().PaddingTop(1).Row(r =>
                                        {
                                            r.RelativeItem().Element(c => LV(c, "Paint Spray Time(ms)", m.P1mS.ToString()));
                                            r.RelativeItem().Element(c => LV(c, "C1 to C2 Sensor Distance(mm)", m.C1C2.ToString()));
                                            r.RelativeItem().Element(c => LV(c, "C2 to Exit Sensor Distance(mm)", m.C2E.ToString()));
                                            r.RelativeItem().Element(c => LV(c, "C Coil to C2 Distance(mm)", m.CC2.ToString()));
                                        });
                                    }

                                    // ABSOLUTE
                                    JObject? configJsonObj = null;
                                    try
                                    {
                                        if (firstTube != default && !string.IsNullOrEmpty(firstTube.ConfigurationJson))
                                        {
                                            configJsonObj = JObject.Parse(firstTube.ConfigurationJson);
                                        }
                                    }
                                    catch { }

                                    var absFreqToken = configJsonObj?["Frequency"]?["FD"]?.FirstOrDefault(x => (int?)x["FN"] == 3);
                                    var absFilterToken = configJsonObj?["Filter"]?["FD"]?.FirstOrDefault(x => (int?)x["FN"] == 3);

                                    string absPreGain = absFreqToken?["G"] != null ? absFreqToken["G"]!.ToString() : "N/A";
                                    string absPostGain = absFilterToken?["G"] != null ? absFilterToken["G"]!.ToString() : "N/A";
                                    string absLowPass = absFilterToken?["L"] != null ? absFilterToken["L"]!.ToString() : "N/A";
                                    string absHighTh = absFreqToken?["UTH"] != null ? absFreqToken["UTH"]!.ToString() : "N/A";
                                    string absLowTh = absFreqToken?["LTH"] != null ? absFreqToken["LTH"]!.ToString() : "N/A";
                                    string absThirdTh = absFreqToken?["TH"] != null ? absFreqToken["TH"]!.ToString() : "N/A";

                                    record.Item().PaddingTop(5).Text("ABSOLUTE")
                                        .FontSize(6.5f).Bold().FontColor("#888888");

                                    record.Item().PaddingTop(2).Row(r =>
                                    {
                                        r.RelativeItem().Element(c => LV(c, "Pre Gain(dB)", absPreGain));
                                        r.RelativeItem().Element(c => LV(c, "Post Gain(dB)", absPostGain));
                                        r.RelativeItem().Element(c => LV(c, "Low Pass Filter", absLowPass));
                                        r.RelativeItem().Element(c => LV(c, "High Threshold", absHighTh));
                                    });

                                    record.Item().PaddingTop(1).Row(r =>
                                    {
                                        r.RelativeItem().Element(c => LV(c, "Low Threshold", absLowTh));
                                        r.RelativeItem().Element(c => LV(c, "Third Threshold", absThirdTh));
                                        r.RelativeItem();
                                        r.RelativeItem();
                                    });

                                    // ---- Records table for this settings group ----
                                    record.Item().PaddingTop(6).Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(50);   // Sr No
                                            columns.RelativeColumn(2);    // Timestamp
                                            columns.ConstantColumn(60);   // Result
                                        });

                                        // Header row
                                        table.Header(header =>
                                        {
                                            header.Cell().Background("#0D3B6E").Padding(4)
                                                .Text("Sr No").FontSize(7).Bold().FontColor("#FFFFFF");

                                            header.Cell().Background("#0D3B6E").Padding(4)
                                                .Text("Timestamp").FontSize(7).Bold().FontColor("#FFFFFF");

                                            header.Cell().Background("#0D3B6E").Padding(4)
                                                .Text("Result").FontSize(7).Bold().FontColor("#FFFFFF");
                                        });

                                        // Data rows
                                        int recordCounter = 0;
                                        foreach (var item in records)
                                        {
                                            recordCounter++;
                                            var localIndex = recordCounter;
                                            var localItem = item;

                                            try
                                            {
                                                bool passed = localItem.Result;
                                                string rowBg = (localIndex % 2 == 0) ? "#FAFAFA" : "#FFFFFF";

                                                table.Cell().Background(rowBg).Padding(4)
                                                    .Text(localIndex.ToString()).FontSize(7).FontColor("#333333");

                                                table.Cell().Background(rowBg).Padding(4)
                                                    .Text(localItem.TimeStamp.ToString("dd/MM/yyyy HH:mm:ss")).FontSize(7).FontColor("#333333");

                                                table.Cell().Background(passed ? "#E8F5E9" : "#FFEBEE").Padding(4)
                                                    .Text(passed ? "PASS" : "FAIL").FontSize(7).Bold()
                                                    .FontColor(passed ? "#2E7D32" : "#C62828");
                                            }
                                            catch
                                            {
                                                MessageBox.Show("Something went wrong. Please try again.");
                                            }
                                        }
                                    });
                                });
                        });
                    });

                    // Footer
                    page.Footer().AlignCenter().PaddingTop(4).Text(x =>
                    {
                        x.Span("Page ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                        x.Span(" of ").FontSize(8);
                        x.TotalPages().FontSize(8);
                    });
                });
            })
            .GeneratePdf(targetPath);
        }

        public void GeneratePdf(string filePath)
        {
            try
            {
                var data = GetBatchDetails();
                var batchDetails = new Dictionary<string, List<(DateTime TimeStamp, string ConfigurationJson, string GraphDataJson, bool Result, string BatchName, string PartJson)>>();
                foreach (var log in listOfLog)
                    batchDetails[log.BatchName] = data.Where(d => d.BatchName == log.BatchName).OrderBy(d => d.TimeStamp).ToList();

                QuestPDF.Settings.License = LicenseType.Community;

                string fullPath = System.IO.Path.GetFullPath(filePath);
                string folder = System.IO.Path.GetDirectoryName(fullPath) ?? "";
                string baseName = System.IO.Path.GetFileNameWithoutExtension(fullPath);
                string ext = System.IO.Path.GetExtension(fullPath);
                if (string.IsNullOrEmpty(ext)) ext = ".pdf";

                Directory.CreateDirectory(folder);

                var generatedFiles = new List<string>();

                foreach (var log in listOfLog)
                {
                    if (!batchDetails.TryGetValue(log.BatchName, out var details) || details.Count == 0)
                    {
                        GenerateSingleConfigPdf(fullPath, log.BatchName, null, new List<(DateTime, string, string, bool, string, string)>());
                        generatedFiles.Add(fullPath);
                        continue;
                    }

                    // ---- Build consecutive groups sharing the same settings ----
                    var groups = new List<(Configuration? Config, List<(DateTime TimeStamp, string ConfigurationJson, string GraphDataJson, bool Result, string BatchName, string PartJson)> Records)>();

                    string? lastKey = null;
                    foreach (var item in details)
                    {
                        Configuration? cfg = null;
                        try { cfg = JsonConvert.DeserializeObject<Configuration>(item.ConfigurationJson); }
                        catch { /* leave null, treated as its own group */ }

                        string key = BuildSettingsKey(cfg);

                        if (groups.Count == 0 || key != lastKey)
                        {
                            groups.Add((cfg, new List<(DateTime, string, string, bool, string, string)> { item }));
                            lastKey = key;
                        }
                        else
                        {
                            groups[groups.Count - 1].Records.Add(item);
                        }
                    }

                    for (int g = 0; g < groups.Count; g++)
                    {
                        var group = groups[g];

                        string targetFile;
                        if (groups.Count == 1)
                        {
                            targetFile = listOfLog.Count == 1
                                ? fullPath
                                : System.IO.Path.Combine(folder, $"{baseName}_{log.BatchName}{ext}");
                        }
                        else
                        {
                            targetFile = listOfLog.Count == 1
                                ? System.IO.Path.Combine(folder, $"{baseName}_Config{g + 1}{ext}")
                                : System.IO.Path.Combine(folder, $"{baseName}_{log.BatchName}_Config{g + 1}{ext}");
                        }

                        GenerateSingleConfigPdf(targetFile, log.BatchName, group.Config, group.Records);
                        generatedFiles.Add(targetFile);
                    }
                }

                if (generatedFiles.Count > 0)
                {
                    string msg = generatedFiles.Count == 1
                        ? $"1 report created: {folder}"
                        : $"{generatedFiles.Count} reports created: {folder}";
                    ShowNonModalMessage(msg);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }
        }

        private string BuildSettingsKey(Configuration? config) => DeviceCOM.BuildSettingsKey(config);

        private void DownloadPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button button || button.Tag is not LogData selectedLog)
                    return;

                GenerateSingleBatchPdf(selectedLog);
            }
            catch
            {
                MessageBox.Show("Error generating PDF");
            }
        }

        private void GenerateSingleBatchPdf(LogData selectedLog)
        {
            try
            {
                if (selectedLog == null)
                {
                    MessageBox.Show("Invalid batch");
                    return;
                }

                // Backup original list
                var originalList = listOfLog;

                // ✅ Replace with ONLY selected batch
                listOfLog = new List<LogData> { selectedLog };

                // File dialog
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = $"{selectedLog.BatchName}_Report";
                dlg.DefaultExt = ".pdf";
                dlg.Filter = "PDF Files (.pdf)|*.pdf";

                if (dlg.ShowDialog() == true)
                {
                    // ✅ Reuse SAME method
                    GeneratePdf(dlg.FileName);
                }

                // Restore original list
                listOfLog = originalList;
            }
            catch (Exception)
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }
        }

        public List<(DateTime TimeStamp, string ConfigurationJson, string GraphDataJson, bool Result, string BatchName, string PartJson)> GetBatchDetails()
        {
            var list = new List<(DateTime, string, string, bool, string, string)>();

            if (!clStartDate.SelectedDate.HasValue || !clToDate.SelectedDate.HasValue)
            {
                return list;
            }

            try
            {
                if (string.IsNullOrEmpty(DeviceCOM.DBConnection))
                {
                    DeviceCOM.DBConnection = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["DBConnection"]) ?? "";
                }

                using (var con = new NpgsqlConnection(DeviceCOM.DBConnection))
                {
                    con.Open();

                    string query = @"
            SELECT 
                ""TimeStamp"",
                ""ConfigurationJson"",
                ""GraphDataJson"",
                ""Result"",
                ""BatchName"",
                ""PartJson""
            FROM ""Logs""
            WHERE ""BatchName"" ILIKE '%' || @BatchName || '%'
                AND ""TimeStamp"" >= @StartDate
                AND ""TimeStamp"" < @EndDate
            ORDER BY ""BatchName""
            ";

                    using (var cmd = new NpgsqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@BatchName", txtBatchName.Text ?? "");
                        cmd.Parameters.AddWithValue("@StartDate", clStartDate.SelectedDate.Value);
                        cmd.Parameters.AddWithValue("@EndDate", clToDate.SelectedDate.Value.AddDays(1));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add((
                                    reader.GetFieldValue<DateTime>(
                                        reader.GetOrdinal("TimeStamp")).ToLocalTime(),

                                    reader["ConfigurationJson"]?.ToString() ?? "",

                                    reader["GraphDataJson"]?.ToString() ?? "",

                                    reader.GetBoolean(
                                        reader.GetOrdinal("Result")),

                                    reader["BatchName"]?.ToString() ?? "",

                                    reader["PartJson"]?.ToString() ?? ""
                                ));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }

            return list;
        }

        public List<(string BatchName,DateTime TimeStamp,string ConfigurationJson,string PartJson)> GetBatchSummaryDetails()
        {
            var list = new List<(string,DateTime,string,string)>();

            if (!clStartDate.SelectedDate.HasValue || !clToDate.SelectedDate.HasValue)
            {
                return list;
            }

            try
            {
                if (string.IsNullOrEmpty(DeviceCOM.DBConnection))
                {
                    DeviceCOM.DBConnection = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["DBConnection"]) ?? "";
                }

                using (var con = new NpgsqlConnection(DeviceCOM.DBConnection))
                {
                    con.Open();

                    string query = @"
                        SELECT DISTINCT ON (""BatchName"")
                            ""BatchName"",
                            ""TimeStamp"",
                            ""ConfigurationJson"",
                            ""PartJson""
                        FROM ""Logs""
                        WHERE ""BatchName"" ILIKE '%' || @BatchName || '%'
                            AND ""TimeStamp"" >= @StartDate
                            AND ""TimeStamp"" < @EndDate
                        ORDER BY ""BatchName"", ""TimeStamp"" ASC
                        ";

                    using (var cmd = new NpgsqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue(
                            "@BatchName",
                            txtBatchName.Text ?? "");

                        cmd.Parameters.AddWithValue(
                            "@StartDate",
                            clStartDate.SelectedDate.Value);

                        cmd.Parameters.AddWithValue(
                            "@EndDate",
                            clToDate.SelectedDate.Value.AddDays(1));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add((
                                    reader["BatchName"]?.ToString() ?? "",

                                    reader.GetFieldValue<DateTime>(
                                        reader.GetOrdinal("TimeStamp"))
                                        .ToLocalTime(),

                                    reader["ConfigurationJson"]?.ToString() ?? "",

                                    reader["PartJson"]?.ToString() ?? ""
                                ));
                            }
                        }
                    }
                }
            }
            catch
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }

            return list;
        }

        private void grdlogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
