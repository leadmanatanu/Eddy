using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;

namespace _8F.ViewModels
{
    public class LogsAllViewModel : BaseViewModel
    {
        private string _batchNameFilter = string.Empty;
        public string BatchNameFilter
        {
            get => _batchNameFilter;
            set => SetProperty(ref _batchNameFilter, value);
        }

        private string _serialNoFilter = string.Empty;
        public string SerialNoFilter
        {
            get => _serialNoFilter;
            set => SetProperty(ref _serialNoFilter, value);
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

        private ObservableCollection<LogData1> _logs = new();
        public ObservableCollection<LogData1> Logs
        {
            get => _logs;
            set => SetProperty(ref _logs, value);
        }

        public Action? CloseAction { get; set; }

        public ICommand SearchCommand { get; }
        public ICommand CloseCommand { get; }

        public LogsAllViewModel()
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
                    const string sql = @"
                        SELECT 
                            ""BatchName"", 
                            ""PartName"", 
                            ""SrNo"", 
                            ""TimeStamp"",  
                            CASE WHEN ""Result"" = TRUE THEN 'OK' ELSE 'Not OK' END AS ""ResultStatus"", 
                            CASE WHEN ""Ch1Result"" = TRUE THEN 'OK' WHEN ""Ch1Result"" IS NULL THEN 'NA' ELSE 'Not OK' END AS ""Ch1Result"", 
                            CASE WHEN ""Ch2Result"" = TRUE THEN 'OK' WHEN ""Ch2Result"" IS NULL THEN 'NA' ELSE 'Not OK' END AS ""Ch2Result"", 
                            CASE WHEN ""Ch3Result"" = TRUE THEN 'OK' WHEN ""Ch3Result"" IS NULL THEN 'NA' ELSE 'Not OK' END AS ""Ch3Result"", 
                            CASE WHEN ""Ch4Result"" = TRUE THEN 'OK' WHEN ""Ch4Result"" IS NULL THEN 'NA' ELSE 'Not OK' END AS ""Ch4Result"",
                            COALESCE(""FrequencyData""::text, ""ResultData""::text, '[]') AS ""FrequencyData""
                        FROM public.""Logs"" l
                        WHERE ""BatchName"" LIKE @BatchName 
                          AND ""SrNo"" LIKE @SrNo 
                          AND ""TimeStamp"" >= @StartDate 
                          AND ""TimeStamp"" <= @EndDate";

                    con.Open();
                    using var cmd = new NpgsqlCommand(sql, con);

                    cmd.Parameters.AddWithValue("@BatchName", $"%{BatchNameFilter}%");
                    cmd.Parameters.AddWithValue("@SrNo", $"%{SerialNoFilter}%");
                    cmd.Parameters.AddWithValue("@StartDate", StartDate?.Date ?? DateTime.Today);
                    cmd.Parameters.AddWithValue("@EndDate", (EndDate?.Date ?? DateTime.Today).AddDays(1));

                    using var reader = cmd.ExecuteReader();
                    DataTable dt = new DataTable();
                    dt.Load(reader);

                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        LogData1 _part = new LogData1();
                        _part.BatchName = dt.Rows[i]["BatchName"]?.ToString() ?? string.Empty;
                        _part.PartName = dt.Rows[i]["PartName"]?.ToString() ?? string.Empty;
                        string ts = dt.Rows[i]["TimeStamp"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(ts) && DateTimeOffset.TryParse(ts, out DateTimeOffset dto))
                        {
                            _part.TimeStamp = dto.ToString("dd/MM/yy HH:mm:ss");
                        }

                        _part.ResultStatus = dt.Rows[i]["ResultStatus"]?.ToString() ?? string.Empty;
                        _part.SrNo = dt.Rows[i]["SrNo"]?.ToString() ?? string.Empty;
                        _part.Ch1Result = dt.Rows[i]["Ch1Result"]?.ToString() ?? string.Empty;
                        _part.Ch2Result = dt.Rows[i]["Ch2Result"]?.ToString() ?? string.Empty;
                        _part.Ch3Result = dt.Rows[i]["Ch3Result"]?.ToString() ?? string.Empty;
                        _part.Ch4Result = dt.Rows[i]["Ch4Result"]?.ToString() ?? string.Empty;
                        _part.FrequencyData = dt.Rows[i]["FrequencyData"]?.ToString() ?? string.Empty;

                        Logs.Add(_part);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading all logs: {ex.Message}");
            }
        }

        private void ExecuteClose()
        {
            CloseAction?.Invoke();
        }
    }
}
