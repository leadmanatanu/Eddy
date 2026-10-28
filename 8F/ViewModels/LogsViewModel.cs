using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;

namespace _8F.ViewModels
{
    public class LogsViewModel : BaseViewModel
    {
        private string _batchNameFilter = string.Empty;
        public string BatchNameFilter
        {
            get => _batchNameFilter;
            set => SetProperty(ref _batchNameFilter, value);
        }

        private DateTime? _startDate = DateTime.Now;
        public DateTime? StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime? _endDate = DateTime.Now;
        public DateTime? EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        private ObservableCollection<LogData> _logs = new();
        public ObservableCollection<LogData> Logs
        {
            get => _logs;
            set => SetProperty(ref _logs, value);
        }

        public bool IsReNewConfig => _8F.Services.ConfigKey.ProtectedSettings.IsReNewConfig;
        public string BatchCaption => IsReNewConfig ? "Shift" : "Batch Name";

        public Action? CloseAction { get; set; }

        public ICommand SearchCommand { get; }
        public ICommand CloseCommand { get; }

        public LogsViewModel()
        {
            SearchCommand = new RelayCommand(ExecuteSearch);
            CloseCommand = new RelayCommand(ExecuteClose);
        }

        private void ExecuteSearch()
        {
            LoadLogs();
        }

        public void LoadLogs()
        {
            try
            {
                Logs.Clear();
                string connectionString = System.Configuration.ConfigurationManager.AppSettings["ConnectionString"] ?? string.Empty;

                using (var con = new NpgsqlConnection(connectionString))
                {
                    // PARAMETERIZED QUERY (Fixes SQL Injection)
                    const string sql = @"
                        SELECT 
                            ""BatchName"", 
                            MIN(""TimeStamp"") AS ""StartDate"", 
                            MAX(""TimeStamp"") AS ""EndDate"",
                            (SELECT COUNT(1) FROM public.""Logs"" l1 WHERE l1.""BatchName"" = l.""BatchName"" AND l1.""Result"" = 'true') AS ""PassCount"",
                            (SELECT COUNT(1) FROM public.""Logs"" l1 WHERE l1.""BatchName"" = l.""BatchName"" AND l1.""Result"" = 'false') AS ""FailCount""
                        FROM public.""Logs"" l
                        WHERE ""BatchName"" LIKE @BatchName 
                          AND ""TimeStamp"" >= @StartDate 
                          AND ""TimeStamp"" <= @EndDate
                        GROUP BY ""BatchName""";

                    con.Open();
                    using var cmd = new NpgsqlCommand(sql, con);

                    cmd.Parameters.AddWithValue("@BatchName", $"%{BatchNameFilter}%");
                    cmd.Parameters.AddWithValue("@StartDate", StartDate?.Date ?? DateTime.Today);
                    cmd.Parameters.AddWithValue("@EndDate", (EndDate?.Date ?? DateTime.Today).AddDays(1));

                    using var reader = cmd.ExecuteReader();
                    DataTable dt = new DataTable();
                    dt.Load(reader);

                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        LogData _part = new LogData();
                        _part.BatchName = dt.Rows[i]["BatchName"]?.ToString() ?? string.Empty;
                        string sDate = dt.Rows[i]["StartDate"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(sDate) && DateTimeOffset.TryParse(sDate, out DateTimeOffset dto))
                        {
                            _part.LogStartDate = dto.ToString("dd/MM/yy HH:mm:ss");
                        }

                        string eDate = dt.Rows[i]["EndDate"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(eDate) && DateTimeOffset.TryParse(eDate, out DateTimeOffset dto1))
                        {
                            _part.LogEndDate = dto1.ToString("dd/MM/yy HH:mm:ss");
                        }

                        _part.PassCount = Convert.ToInt32(dt.Rows[i]["PassCount"] == DBNull.Value ? 0 : dt.Rows[i]["PassCount"]);
                        _part.FailCount = Convert.ToInt32(dt.Rows[i]["FailCount"] == DBNull.Value ? 0 : dt.Rows[i]["FailCount"]);
                        _part.TotalCount = _part.PassCount + _part.FailCount;

                        Logs.Add(_part);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading logs: {ex.Message}");
            }
        }

        private void ExecuteClose()
        {
            CloseAction?.Invoke();
        }
    }
}
