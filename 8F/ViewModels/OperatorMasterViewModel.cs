using _8F.Repositories;
using _8F.Auth;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Windows;
using System.Windows.Input;

namespace _8F.ViewModels
{
    public class OperatorMasterViewModel : BaseViewModel
    {
        private int _selectedId = 0;
        public int SelectedId
        {
            get => _selectedId;
            set => SetProperty(ref _selectedId, value);
        }

        private string _operatorName = string.Empty;
        public string OperatorName
        {
            get => _operatorName;
            set => SetProperty(ref _operatorName, value);
        }

        private ObservableCollection<Operator> _operatorList = new();
        public ObservableCollection<Operator> OperatorList
        {
            get => _operatorList;
            set => SetProperty(ref _operatorList, value);
        }

        public Action? CloseAction { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand CloseCommand { get; }

        public OperatorMasterViewModel()
        {
            SaveCommand = new RelayCommand(ExecuteSave);
            CancelCommand = new RelayCommand(ResetForm);
            CloseCommand = new RelayCommand(ExecuteClose);

            LoadOperators();
        }

        public void LoadOperators()
        {
            try
            {
                OperatorList.Clear();
                string connectionString = DbConnectionProvider.ConnectionString;

                using (var con = new NpgsqlConnection(connectionString))
                {
                    con.Open();
                    const string sql = @"SELECT * FROM public.""Operators"" WHERE ""IsActive"" = true ORDER BY ""OperatorName""";

                    using var cmd = new NpgsqlCommand(sql, con);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        OperatorList.Add(new Operator
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            OperatorName = reader["OperatorName"]?.ToString() ?? string.Empty,
                            IsActive = Convert.ToBoolean(reader["IsActive"])
                        });
                    }
                }
                ResetForm();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading operators: {ex.Message}");
            }
        }

        private void ExecuteSave()
        {
            string name = OperatorName.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Operator name cannot be empty.");
                return;
            }

            string connectionString = DbConnectionProvider.ConnectionString;
            using var con = new NpgsqlConnection(connectionString);
            con.Open();

            if (SelectedId == 0)
            {
                if (OperatorExists(name, con))
                {
                    MessageBox.Show("Operator already exists.");
                    return;
                }

                const string sql = @"INSERT INTO public.""Operators""(""OperatorName"") VALUES(@name)";
                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
                _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Operator Master", null, $"Added operator '{name}'");
            }
            else
            {
                const string sql = @"UPDATE public.""Operators"" SET ""OperatorName"" = @name WHERE ""Id"" = @id";
                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@id", SelectedId);
                cmd.ExecuteNonQuery();
                _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Operator Master", null, $"Updated operator '{name}'");
            }

            LoadOperators();
        }

        private bool OperatorExists(string name, NpgsqlConnection con)
        {
            const string checkSql = @"SELECT COUNT(1) FROM public.""Operators"" WHERE LOWER(""OperatorName"") = LOWER(@name) AND ""IsActive"" = true";
            using var checkCmd = new NpgsqlCommand(checkSql, con);
            checkCmd.Parameters.AddWithValue("@name", name);
            var count = Convert.ToInt32(checkCmd.ExecuteScalar());
            return count > 0;
        }

        public void EditOperator(Operator item)
        {
            if (item != null)
            {
                SelectedId = item.Id;
                OperatorName = item.OperatorName;
            }
        }

        public void DeleteOperator(Operator item)
        {
            if (item != null)
            {
                var confirmResult = MessageBox.Show($"Are you sure you want to delete '{item.OperatorName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirmResult == MessageBoxResult.Yes)
                {
                    string connectionString = DbConnectionProvider.ConnectionString;
                    using var con = new NpgsqlConnection(connectionString);
                    con.Open();

                    const string sql = @"UPDATE public.""Operators"" SET ""IsActive"" = false WHERE ""Id"" = @id";
                    using var cmd = new NpgsqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@id", item.Id);
                    cmd.ExecuteNonQuery();
                    _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Operator Master", null, $"Deleted operator '{item.OperatorName}'");

                    LoadOperators();
                }
            }
        }

        public void ResetForm()
        {
            SelectedId = 0;
            OperatorName = string.Empty;
        }

        private void ExecuteClose()
        {
            CloseAction?.Invoke();
        }
    }
}
