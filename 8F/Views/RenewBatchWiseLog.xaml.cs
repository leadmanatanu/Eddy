using Newtonsoft.Json;
using Npgsql;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace _8F
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class RenewBatchWiseLog : Window
    {
        public bool IsSaved = false;

        public List<LogData> listOfLog = new();

        private bool IsReNewConfig =>Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["isrenewconfig"]);

        private string BatchCaption => IsReNewConfig ? "Shift" : "Batch Name";

        public RenewBatchWiseLog()
        {
            InitializeComponent();

            ((DataGridTextColumn)grdlogs.Columns[2]).Header = BatchCaption;  // 0→2
            lblBatchName.Content = BatchCaption;

            clStartDate.SelectedDate = DateTime.Now;
            clToDate.SelectedDate = DateTime.Now;

            ApplyColumnVisibility();
        }

        private static bool GetConfigBool(string key, bool defaultValue = true)
        {
            string? val = System.Configuration.ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(val)) return defaultValue;
            return bool.TryParse(val, out bool result) ? result : defaultValue;
        }

        private void ApplyColumnVisibility()
        {
            bool isTotalCountVisible = GetConfigBool("IsTotalCountVisible", true);
            bool isNotOkCountVisible = GetConfigBool("IsNotOkCountVisible", true);

            if (!isTotalCountVisible)
            {
                var totalCol = grdlogs.Columns.FirstOrDefault(c => c.Header?.ToString() == "Total Count");
                if (totalCol != null) totalCol.Visibility = Visibility.Collapsed;
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
            try
            {
                listOfLog = new List<LogData>();
                using (var con = new NpgsqlConnection(System.Configuration.ConfigurationManager.AppSettings["ConnectionString"]))
                {
                    string sql = @"
                        SELECT 
                            ""BatchName"",
                            ""TimeStamp""::date AS ""LogDate"",
                            ""PartData"" ->> 'ProductionOrder' AS ""ProductionOrder"",
                            COUNT(1) FILTER (WHERE ""Result"" = 'true')  AS ""PassCount"",
                            COUNT(1) FILTER (WHERE ""Result"" = 'false') AS ""FailCount""
                        FROM public.""Logs""
                        WHERE ""BatchName"" LIKE '%" + txtBatchName.Text + @"%'
                          AND ""TimeStamp"" >= '" + clStartDate.Text + @"'
                          AND ""TimeStamp"" <= '" + Convert.ToDateTime(clToDate.Text).AddDays(1).ToString() + @"'
                        GROUP BY ""BatchName"", ""TimeStamp""::date, ""PartData"" ->> 'ProductionOrder'
                        ORDER BY ""TimeStamp""::date, ""PartData"" ->> 'ProductionOrder', ""BatchName""";

                    con.Open();
                    var cmd = new NpgsqlCommand(sql, con);
                    var dataReader = cmd.ExecuteReader();
                    DataTable dt = new DataTable();
                    dt.Load(dataReader);
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        LogData _part = new LogData();
                        _part.BatchName = dt.Rows[i]["BatchName"]?.ToString() ?? string.Empty;
                        _part.ProductionOrder = dt.Rows[i]["ProductionOrder"] == DBNull.Value
                            ? string.Empty
                            : (dt.Rows[i]["ProductionOrder"]?.ToString() ?? string.Empty);
                        DateTime logDate = Convert.ToDateTime(dt.Rows[i]["LogDate"]);
                        _part.LogDateRaw = logDate;
                        _part.Date = logDate.ToString("dd/MM/yyyy");
                        _part.PassCount = Convert.ToInt32(dt.Rows[i]["PassCount"]);
                        _part.FailCount = Convert.ToInt32(dt.Rows[i]["FailCount"]);
                        _part.TotalCount = _part.PassCount + _part.FailCount;
                        listOfLog.Add(_part);
                    }
                    grdlogs.ItemsSource = listOfLog;
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }
        }

        private void btnDownload_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                LoadLogs();
                if (listOfLog.Count > 0)
                {
                    string from = Convert.ToDateTime(clStartDate.Text).ToString("dd-MM-yyyy");
                    string to = Convert.ToDateTime(clToDate.Text).ToString("dd-MM-yyyy");

                    Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                    dlg.FileName = SafeFileName($"SummaryReport_{from}_to_{to}");
                    dlg.DefaultExt = ".pdf";
                    dlg.Filter = "PDF Files (*.pdf)|*.pdf";

                    Nullable<bool> result = dlg.ShowDialog();

                    if (result == true)
                    {
                        GenerateSummaryPdf(dlg.FileName);
                        MessageBox.Show("PDF Generated Successfully");
                    }
                }
                else
                {
                    MessageBox.Show("No data found.");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("PDF generation failed.");
            }
        }

        public void GenerateSummaryPdf(string filePath)
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

                        // ----- HEADER (logo + title) -----
                        page.Header()
                            .ShowOnce()
                            .BorderBottom(2)
                            .BorderColor("#1B365D")
                            .PaddingBottom(8)
                            .Row(r =>
                            {
                                r.RelativeItem().Row(left =>
                                {
                                    var imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Magkraft.jpg");
                                    if (File.Exists(imagePath))
                                    {
                                        var imageBytes = File.ReadAllBytes(imagePath);
                                        left.AutoItem().Height(28).Width(28).AlignMiddle().Image(imageBytes);
                                        left.ConstantItem(10);
                                    }

                                    left.AutoItem().AlignMiddle().Column(tc =>
                                    {
                                        tc.Item().Text("SORTER EDDY REPORT - SUMMARY REPORT").FontSize(15).Bold().FontColor("#1B365D");
                                        tc.Item().Text("INDUSTRIAL NON-DESTRUCTIVE TESTING (NDT) INSPECTION SYSTEM").FontSize(7).FontColor("#64748B");
                                    });
                                });

                                r.ConstantItem(180).AlignRight().AlignMiddle()
                                    .Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#64748B");
                            });

                        // ----- CONTENT (summary table) -----
                        page.Content()
                            .PaddingTop(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);   // Production Order
                                    columns.RelativeColumn(2);   // Date
                                    columns.RelativeColumn(2);   // Shift / Batch
                                    columns.RelativeColumn(1);   // OK
                                    columns.RelativeColumn(1);   // Not OK
                                    columns.RelativeColumn(1);   // Total
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(5).AlignCenter().Text("Production Order").FontSize(8.5f).Bold().FontColor(Colors.White);
                                    header.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(5).AlignCenter().Text("Date").FontSize(8.5f).Bold().FontColor(Colors.White);
                                    header.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(5).AlignCenter().Text(BatchCaption).FontSize(8.5f).Bold().FontColor(Colors.White);
                                    header.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(5).AlignCenter().Text("OK Count").FontSize(8.5f).Bold().FontColor(Colors.White);
                                    header.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(5).AlignCenter().Text("Not OK Count").FontSize(8.5f).Bold().FontColor(Colors.White);
                                    header.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(5).AlignCenter().Text("Total Count").FontSize(8.5f).Bold().FontColor(Colors.White);
                                });

                                int rowIndex = 0;
                                foreach (var log in logs)
                                {
                                    rowIndex++;
                                    string bg = rowIndex % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

                                    table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(4).AlignCenter().Text(log.ProductionOrder ?? "-").FontSize(8).FontColor("#1E293B");
                                    table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(4).AlignCenter().Text(log.Date).FontSize(8).FontColor("#1E293B");
                                    table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(4).AlignCenter().Text(log.BatchName).FontSize(8).FontColor("#1E293B");
                                    table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(4).AlignCenter().Text(log.PassCount.ToString()).FontSize(8).Bold().FontColor("#065F46");
                                    table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(4).AlignCenter().Text(log.FailCount.ToString()).FontSize(8).Bold().FontColor("#991B1B");
                                    table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(4).AlignCenter().Text(log.TotalCount.ToString()).FontSize(8).Bold().FontColor("#0F172A");
                                }
                            });

                        // ----- FOOTER (page numbers) -----
                        page.Footer()
                            .BorderTop(1)
                            .BorderColor("#E2E8F0")
                            .PaddingTop(6)
                            .Row(r =>
                            {
                                r.RelativeItem().AlignLeft().Text("Antigravity 8F ECT Inspection System | Confidential")
                                    .FontSize(7).FontColor("#94A3B8");

                                r.RelativeItem().AlignCenter().Text(x =>
                                {
                                    x.Span("Page ").FontSize(7).FontColor("#64748B");
                                    x.CurrentPageNumber().FontSize(7).Bold().FontColor("#1E293B");
                                    x.Span(" of ").FontSize(7).FontColor("#64748B");
                                    x.TotalPages().FontSize(7).Bold().FontColor("#1E293B");
                                });

                                r.RelativeItem().AlignRight().Text("Automated Summary Report")
                                    .FontSize(7).FontColor("#94A3B8");
                            });
                    });
                })
                .GeneratePdf(filePath);
            }
            catch
            {
                MessageBox.Show("Summary PDF generation failed.");
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

                string from = Convert.ToDateTime(clStartDate.Text).ToString("dd-MM-yyyy");
                string to = Convert.ToDateTime(clToDate.Text).ToString("dd-MM-yyyy");

                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = SafeFileName($"DetailedReport_{from}_to_{to}");
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
                if (logs == null || logs.Count == 0)
                {
                    MessageBox.Show("No data found.");
                    return;
                }

                BuildDetailedReportPdf(logs, filePath);
                MessageBox.Show("PDF Generated Successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Detailed PDF generation error: {ex.Message}");
                MessageBox.Show("Something went wrong. Please try again.");
            }
        }

        private void BuildDetailedReportPdf(List<LogData> logs, string filePath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4.Landscape());
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8).FontColor("#1E293B"));

                    // ================= PAGE HEADER =================
                    page.Header()
                        .ShowOnce()
                        .BorderBottom(2)
                        .BorderColor("#1B365D")
                        .PaddingBottom(8)
                        .Row(r =>
                        {
                            r.RelativeItem().Row(left =>
                            {
                                var imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Magkraft.jpg");
                                if (File.Exists(imagePath))
                                {
                                    var imageBytes = File.ReadAllBytes(imagePath);
                                    left.ConstantItem(28).Height(28).AlignMiddle().Image(imageBytes).FitArea();
                                    left.ConstantItem(10);
                                }

                                left.RelativeItem().AlignMiddle().Column(tc =>
                                {
                                    tc.Item().Text("SORTER EDDY REPORT - DETAILED REPORT")
                                        .FontSize(15).Bold().FontColor("#1B365D");
                                    tc.Item().Text("INDUSTRIAL NON-DESTRUCTIVE TESTING (NDT) INSPECTION SYSTEM")
                                        .FontSize(7).FontColor("#64748B");
                                });
                            });

                            r.ConstantItem(220).AlignRight().AlignMiddle().Column(rc =>
                            {
                                rc.Item().AlignRight().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                    .FontSize(8).FontColor("#475569");
                                rc.Item().AlignRight().Text("8-Frequency Inspection Data")
                                    .FontSize(7).FontColor("#94A3B8");
                            });
                        });

                    // ================= PAGE CONTENT =================
                    page.Content()
                        .PaddingTop(10)
                        .Column(col =>
                        {
                            col.Spacing(10);

                            foreach (var log in logs)
                            {
                                var details = GetBatchDetails(log.BatchName, log.LogDateRaw, log.ProductionOrder);
                                if (details == null || details.Count == 0) continue;

                                // 1. Shift / Batch Header Bar
                                col.Item()
                                    .Background("#1B365D")
                                    .Padding(7).PaddingHorizontal(12)
                                    .Row(r =>
                                    {
                                        r.RelativeItem().Row(br =>
                                        {
                                            br.AutoItem().Text($"{BatchCaption} : ").FontSize(8.5f).Bold().FontColor("#CBD5E1");
                                            br.AutoItem().Text(log.BatchName).FontSize(8.5f).Bold().FontColor(Colors.White);
                                            if (!string.IsNullOrWhiteSpace(log.ProductionOrder))
                                            {
                                                br.ConstantItem(15);
                                                br.AutoItem().Text("PO : ").FontSize(8.5f).Bold().FontColor("#CBD5E1");
                                                br.AutoItem().Text(log.ProductionOrder).FontSize(8.5f).Bold().FontColor(Colors.White);
                                            }
                                            br.ConstantItem(15);
                                            br.AutoItem().Text("Date : ").FontSize(8.5f).Bold().FontColor("#CBD5E1");
                                            br.AutoItem().Text(log.Date).FontSize(8.5f).Bold().FontColor(Colors.White);
                                        });

                                        r.ConstantItem(260).AlignRight().Row(stats =>
                                        {
                                            stats.AutoItem().Background("#0F2942").PaddingHorizontal(6).PaddingVertical(2)
                                                .Text($"OK : {log.PassCount}").FontSize(8).Bold().FontColor("#86EFAC");
                                            stats.ConstantItem(6);
                                            stats.AutoItem().Background("#0F2942").PaddingHorizontal(6).PaddingVertical(2)
                                                .Text($"NOT OK : {log.FailCount}").FontSize(8).Bold().FontColor("#FCA5A5");
                                            stats.ConstantItem(6);
                                            stats.AutoItem().Background("#0F2942").PaddingHorizontal(6).PaddingVertical(2)
                                                .Text($"TOTAL : {log.TotalCount}").FontSize(8).Bold().FontColor(Colors.White);
                                        });
                                    });

                                // 2. Sections per unique settings group
                                foreach (var group in details.GroupBy(d => BuildSettingsKey(d.FDData, d.PartData)))
                                {
                                    List<GraphData> fdList = new();
                                    try { fdList = JsonConvert.DeserializeObject<List<GraphData>>(group.First().FDData) ?? new(); }
                                    catch { fdList = new(); }

                                    PartConfiguration? part = null;
                                    try { part = JsonConvert.DeserializeObject<PartConfiguration>(group.First().PartData ?? "{}"); }
                                    catch { part = null; }

                                    // Determine ONLY the ON (enabled) channels from configuration
                                    var enabledChannels = fdList.Where(g => g.isEnable).OrderBy(g => g.Id).ToList();
                                    if (enabledChannels.Count == 0 && (fdList.Count == 0 || fdList.All(g => g.isEnable)))
                                    {
                                        enabledChannels = Enumerable.Range(1, 8).Select(i => new GraphData { Id = i, Name = $"D{i}", isEnable = true }).ToList();
                                    }

                                    // Configuration, Threshold, and Part Configuration Card
                                    col.Item().Border(1).BorderColor("#E2E8F0").Background("#FFFFFF").Padding(8).Column(cfgCol =>
                                    {
                                        cfgCol.Spacing(6);

                                        // Side-by-side Configuration & Threshold settings in Landscape
                                        cfgCol.Item().Row(row =>
                                        {
                                            // Left: Configuration Settings
                                            row.RelativeItem(5).Column(left =>
                                            {
                                                left.Item().Text("CONFIGURATION SETTINGS").FontSize(8f).Bold().FontColor("#1B365D");
                                                left.Item().PaddingTop(3).Table(table =>
                                                {
                                                    table.ColumnsDefinition(cols =>
                                                    {
                                                        cols.RelativeColumn(1.2f);
                                                        cols.RelativeColumn();
                                                        cols.RelativeColumn();
                                                        cols.RelativeColumn();
                                                        cols.RelativeColumn();
                                                    });

                                                    table.Header(h =>
                                                    {
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Channel").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Frequency").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Gain").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Phase").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Status").FontSize(7f).Bold().FontColor(Colors.White);
                                                    });

                                                    int idx = 0;
                                                    foreach (var d in enabledChannels)
                                                    {
                                                        idx++;
                                                        string bg = idx % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.Name ?? "").FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.freq.ToString()).FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.gain.ToString()).FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.phase.ToString()).FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.isEnable ? "ON" : "OFF").FontSize(7f);
                                                    }
                                                });
                                            });

                                            row.ConstantItem(12);

                                            // Right: Threshold Settings
                                            row.RelativeItem(6).Column(right =>
                                            {
                                                right.Item().Text("THRESHOLD SETTINGS").FontSize(8f).Bold().FontColor("#1B365D");
                                                right.Item().PaddingTop(3).Table(table =>
                                                {
                                                    table.ColumnsDefinition(cols =>
                                                    {
                                                        cols.RelativeColumn(1.2f);
                                                        cols.RelativeColumn();
                                                        cols.RelativeColumn();
                                                        cols.RelativeColumn();
                                                        cols.RelativeColumn();
                                                        cols.RelativeColumn();
                                                    });

                                                    table.Header(h =>
                                                    {
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Channel").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Height").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Width").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Ex").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Ey").FontSize(7f).Bold().FontColor(Colors.White);
                                                        h.Cell().Background("#1B365D").Padding(2.5f).AlignCenter().Text("Angle").FontSize(7f).Bold().FontColor(Colors.White);
                                                    });

                                                    int idx = 0;
                                                    foreach (var d in enabledChannels)
                                                    {
                                                        idx++;
                                                        string bg = idx % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.Name ?? "").FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.height.ToString("0.##")).FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.width.ToString("0.##")).FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.ex.ToString("0.##")).FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.ey.ToString("0.##")).FontSize(7f);
                                                        table.Cell().Background(bg).Border(1).BorderColor("#E2E8F0").Padding(2).AlignCenter().Text(d.angel.ToString("0.##")).FontSize(7f);
                                                    }
                                                });
                                            });
                                        });

                                        // Part Configuration below tables
                                        cfgCol.Item().PaddingTop(2).Column(c =>
                                        {
                                            c.Item().Text("PART CONFIGURATION").FontSize(8f).Bold().FontColor("#1B365D");
                                            c.Item().PaddingTop(2).Row(r =>
                                            {
                                                if (IsReNewConfig)
                                                {
                                                    r.RelativeItem(1).Element(x => LV(x, "Production Order", part?.ProductionOrder));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Machine Number", part?.MachineNumber));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Part Number", part?.PartNumber));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Part Family", part?.PartFamily));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Shift", part?.BatchName));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Operator", part?.CheckedBy));
                                                }
                                                else
                                                {
                                                    r.RelativeItem(1).Element(x => LV(x, "Batch Name", part?.BatchName ?? log.BatchName));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Part Name", part?.Name));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Grade", part?.Grade));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Checked By", part?.CheckedBy));
                                                    r.ConstantItem(10);
                                                    r.RelativeItem(1).Element(x => LV(x, "Company Name", part?.CompanyName));
                                                }
                                            });
                                        });
                                    });

                                    // ----- SINGLE COMBINED RESULTS TABLE -----
                                    col.Item().Column(resCol =>
                                    {
                                        resCol.Item().Text("INSPECTION RESULTS").FontSize(8.5f).Bold().FontColor("#1B365D");

                                        resCol.Item().PaddingTop(3).Table(resultTable =>
                                        {
                                            // Dynamic columns: Sr No., Timestamp, ON channels (X,Y), Result
                                            resultTable.ColumnsDefinition(cols =>
                                            {
                                                cols.ConstantColumn(36);   // 1. Sr No.
                                                cols.ConstantColumn(110);  // 2. Timestamp
                                                foreach (var ch in enabledChannels)
                                                {
                                                    cols.RelativeColumn(); // Only ON channels
                                                }
                                                cols.ConstantColumn(52);   // Result
                                            });

                                            resultTable.Header(h =>
                                            {
                                                h.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(3.5f).AlignCenter().Text("Sr No.").FontSize(7.5f).Bold().FontColor(Colors.White);
                                                h.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(3.5f).AlignLeft().Text("Timestamp").FontSize(7.5f).Bold().FontColor(Colors.White);

                                                foreach (var ch in enabledChannels)
                                                {
                                                    string chName = !string.IsNullOrWhiteSpace(ch.Name) ? ch.Name : $"D{ch.Id}";
                                                    h.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(3.5f).AlignCenter().Text($"{chName} (X,Y)").FontSize(7f).Bold().FontColor(Colors.White);
                                                }

                                                h.Cell().Background("#1B365D").Border(1).BorderColor("#1B365D").Padding(3.5f).AlignCenter().Text("Result").FontSize(7.5f).Bold().FontColor(Colors.White);
                                            });

                                            int recordIndex = 0;
                                            foreach (var item in group)
                                            {
                                                recordIndex++;
                                                string rowBg = recordIndex % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

                                                // Map FN 1..8 to FreqResult objects
                                                var freqList = item.GetFrequencyResults();
                                                var freqMap = new Dictionary<int, FreqResult>();
                                                foreach (var f in freqList)
                                                {
                                                    if (f != null && f.FN >= 1 && f.FN <= 8)
                                                    {
                                                        freqMap[f.FN] = f;
                                                    }
                                                }

                                                // 1. Sr No.
                                                resultTable.Cell().Background(rowBg).Border(1).BorderColor("#E2E8F0").Padding(3)
                                                    .AlignCenter().Text(recordIndex.ToString()).FontSize(7.5f).FontColor("#1E293B");

                                                // 2. Timestamp
                                                resultTable.Cell().Background(rowBg).Border(1).BorderColor("#E2E8F0").Padding(3)
                                                    .AlignLeft().Text($"{item.TimeStamp:dd/MM/yyyy HH:mm:ss}").FontSize(7.5f).FontColor("#1E293B");

                                                // 3 to N. ONLY ON channels (X,Y)
                                                foreach (var ch in enabledChannels)
                                                {
                                                    int fn = ch.Id;
                                                    bool exists = freqMap.TryGetValue(fn, out var fObj);
                                                    string coord = exists && fObj != null ? $"({fObj.X}, {fObj.Y})" : "N/A";
                                                    bool isFailedCell = !item.Result && exists && fObj != null && fObj.R == 0;

                                                    string cellBg = isFailedCell ? "#FEF2F2" : rowBg;
                                                    string cellBorder = isFailedCell ? "#FECACA" : "#E2E8F0";
                                                    string cellTextColor = isFailedCell ? "#991B1B" : "#1E293B";

                                                    var textElement = resultTable.Cell().Background(cellBg).Border(1).BorderColor(cellBorder).Padding(3)
                                                        .AlignCenter().Text(coord).FontSize(7.5f).FontColor(cellTextColor);

                                                    if (isFailedCell)
                                                    {
                                                        textElement.Bold();
                                                    }
                                                }

                                                // Result - Restrained Industrial PASS/FAIL Colors
                                                string resultBg = item.Result ? "#ECFDF5" : "#FEF2F2";
                                                string resultTextColor = item.Result ? "#065F46" : "#991B1B";
                                                string resultBorderColor = item.Result ? "#A7F3D0" : "#FECACA";

                                                resultTable.Cell().Background(resultBg).Border(1).BorderColor(resultBorderColor).Padding(3)
                                                    .AlignCenter().Text(item.Result ? "PASS" : "FAIL").FontSize(7.5f).Bold().FontColor(resultTextColor);
                                            }
                                        });                                                                              
                                    });
                                }
                            }
                        });

                    // ================= PAGE FOOTER =================
                    page.Footer()
                        .BorderTop(1)
                        .BorderColor("#E2E8F0")
                        .PaddingTop(6)
                        .Row(r =>
                        {
                           
                            r.RelativeItem().AlignCenter().Text(x =>
                            {
                                x.Span("Page ").FontSize(7).FontColor("#64748B");
                                x.CurrentPageNumber().FontSize(7).Bold().FontColor("#1E293B");
                                x.Span(" of ").FontSize(7).FontColor("#64748B");
                                x.TotalPages().FontSize(7).Bold().FontColor("#1E293B");
                            });

                        });
                });
            })
            .GeneratePdf(filePath);
        }

        private List<BatchDetail>? GetBatchDetails(string batchName, DateTime? logDate, string productionOrder)
        {
            try
            {
                List<BatchDetail> list = new List<BatchDetail>();

                using (var con = new NpgsqlConnection(System.Configuration.ConfigurationManager.AppSettings["ConnectionString"]))
                {
                    con.Open();

                    string sql = @"
                        SELECT ""TimeStamp"", ""BatchName"", ""Result"", ""FDData"", ""PartData"", COALESCE(""FrequencyData""::text, ""ResultData""::text, '[]') AS ""FrequencyData""
                        FROM public.""Logs""
                        WHERE 1=1";

                    if (IsReNewConfig)
                    {
                        sql += @"
                          AND ""BatchName"" = @BatchName
                          AND DATE(""TimeStamp"") = @LogDate::date
                          AND (
                                @ProductionOrder = '' 
                             OR ""PartData""::jsonb->>'ProductionOrder' = @ProductionOrder
                          )";
                    }
                    else
                    {
                        sql += @" AND ""BatchName"" = @BatchName";
                    }

                    sql += @" ORDER BY ""TimeStamp""";

                    using (var cmd = new NpgsqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@BatchName", (object?)batchName ?? DBNull.Value);

                        if (IsReNewConfig)
                        {
                            cmd.Parameters.AddWithValue("@LogDate", logDate.HasValue ? (object)logDate.Value.Date : DBNull.Value);
                            cmd.Parameters.AddWithValue("@ProductionOrder", (object?)productionOrder ?? "");
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);

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
                    }
                }

                return list;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void btnRowDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button? btn = sender as Button;
                if (btn == null) return;

                LogData? log = btn.Tag as LogData;

                if (log == null)
                    return;

                string shiftPart = SafeFileName(BatchCaption.Replace(" ", "") + log.BatchName);
                string poPart = string.IsNullOrWhiteSpace(log.ProductionOrder)? "": "_" + SafeFileName(log.ProductionOrder);
                string datePart = "_" + log.LogDateRaw.ToString("dd-MM-yyyy");

                string fileName = $"DetailedReport_{shiftPart}{poPart}{datePart}";

                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = SafeFileName(fileName);
                dlg.DefaultExt = ".pdf";
                dlg.Filter = "PDF Files (*.pdf)|*.pdf";

                if (dlg.ShowDialog() == true)
                {
                    GeneratePdf(log, dlg.FileName);
                }
            }
            catch
            {
                MessageBox.Show("Something went wrong. Please try again.");
            }
        }

        public void GeneratePdf(LogData log, string filePath)
        {
            try
            {
                if (log == null) return;
                BuildDetailedReportPdf(new List<LogData> { log }, filePath);
                MessageBox.Show("PDF Generated Successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Detailed PDF generation error: {ex.Message}");
                MessageBox.Show("Something went wrong. Please try again.");
            }
        }

        void LV(IContainer c, string label, string? value)
        {
            c.Text(t =>
            {
                t.Span($"{label}: ").FontSize(7.5f).FontColor("#64748B");
                t.Span(value ?? "-").FontSize(7.5f).Bold().FontColor("#0F172A");
            });
        }

        private string BuildSettingsKey(string fdData, string partData)
        {
            try
            {
                var sb = new StringBuilder();

                // ---------- Configuration + Threshold ----------
                var fdList = JsonConvert.DeserializeObject<List<GraphData>>(fdData) ?? new List<GraphData>();

                foreach (var d in fdList.OrderBy(x => x.Name))
                {
                    sb.Append(d.Name).Append('|')
                      .Append(d.freq).Append('|')
                      .Append(d.gain).Append('|')
                      .Append(d.phase).Append('|')
                      .Append(d.isEnable).Append('|')
                      .Append(Math.Round(d.height, 2)).Append('|')
                      .Append(Math.Round(d.width, 2)).Append('|')
                      .Append(Math.Round(d.ex, 2)).Append('|')
                      .Append(Math.Round(d.ey, 2)).Append('|')
                      .Append(Math.Round(d.angel, 2)).Append(';');
                }

                // ---------- Part Configuration ----------
                var part = JsonConvert.DeserializeObject<PartConfiguration>(partData ?? "{}");

                if (part != null)
                {
                    if (IsReNewConfig)
                    {
                        sb.Append("##PART##")
                          .Append(part.ProductionOrder).Append('|')
                          .Append(part.MachineNumber).Append('|')
                          .Append(part.PartNumber).Append('|')
                          .Append(part.PartFamily).Append('|')
                          .Append(part.BatchName).Append('|')
                          .Append(part.CheckedBy);
                    }
                    else
                    {
                        sb.Append("##PART##")
                          .Append(part.BatchName).Append('|')
                          .Append(part.Name).Append('|')
                          .Append(part.Grade).Append('|')
                          .Append(part.CheckedBy).Append('|')
                          .Append(part.CompanyName);
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }

        private string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }
    }
}

