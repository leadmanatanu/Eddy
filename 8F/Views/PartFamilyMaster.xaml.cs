using _8F.Repositories;
using _8F.Auth;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;

namespace _8F
{
    public partial class PartFamilyMaster : Window
    {
        private int SelectedFamilyId = 0;
        private int SelectedPartId = 0;

        private List<PartFamily> familyList = new List<PartFamily>();
        private List<PartMaster> partList = new List<PartMaster>();
        private bool _isInternalUpdate = false;
        public PartFamilyMaster()
        {
            InitializeComponent();
            if (!AppServices.AuthService.Can(Permission.EditSettings))
            {
                MessageBox.Show("Admin access required for Part Master.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                Loaded += (s, e) => Close();
                return;
            }

            grdParts.Visibility = Visibility.Collapsed;
            txtPartsMessage.Visibility = Visibility.Visible;
            txtPartsMessage.Text = "Please select a family first";

            LoadFamilies();
        }

        private void LoadFamilies()
        {
            familyList.Clear();

            using (var con = new NpgsqlConnection(DbConnectionProvider.ConnectionString))
            {
                con.Open();

                string sql = @"SELECT *
                       FROM public.""PartFamilies""
                       WHERE ""IsActive"" = true
                       ORDER BY ""FamilyName""";

                using var cmd = new NpgsqlCommand(sql, con);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    familyList.Add(new PartFamily()
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        FamilyName = reader["FamilyName"]?.ToString() ?? string.Empty,
                        IsActive = Convert.ToBoolean(reader["IsActive"])
                    });
                }
            }

            grdFamilies.ItemsSource = null;
            grdFamilies.ItemsSource = familyList;

            // =========================
            // CASE 1: NO DATA
            // =========================
            if (familyList.Count == 0)
            {
                SelectedFamilyId = 0;

                grdFamilies.Visibility = Visibility.Collapsed;
                txtFamiliesMessage.Visibility = Visibility.Visible;

                grdParts.ItemsSource = null;
                grdParts.Visibility = Visibility.Collapsed;

                txtPartsMessage.Visibility = Visibility.Visible;
                txtPartsMessage.Text = "No families available. Please add family.";

                return;
            }

            // =========================
            // CASE 2: DATA EXISTS
            // =========================
            grdFamilies.Visibility = Visibility.Visible;
            txtFamiliesMessage.Visibility = Visibility.Collapsed;

            PartFamily? selectedFamily = null;

            if (SelectedFamilyId > 0)
                selectedFamily = familyList.Find(x => x.Id == SelectedFamilyId);

            grdFamilies.SelectedItem = selectedFamily;

            if (selectedFamily == null)
                SelectedFamilyId = 0;

            // =========================
            // RIGHT PANEL STATE
            // =========================
            if (SelectedFamilyId == 0)
            {
                grdParts.ItemsSource = null;
                grdParts.Visibility = Visibility.Collapsed;

                txtPartsMessage.Visibility = Visibility.Visible;
                txtPartsMessage.Text = "Please select a family first";
            }
            else
            {
                txtPartsMessage.Visibility = Visibility.Collapsed;
            }
        }
        private void LoadParts()
        {
            partList.Clear();

            grdParts.ItemsSource = null;
            grdParts.Visibility = Visibility.Collapsed;

            txtPartsMessage.Visibility = Visibility.Visible;
            txtPartsMessage.Text = "Loading...";

            if (SelectedFamilyId <= 0)
            {
                txtPartsMessage.Text = "Please select a family first";
                return;
            }

            using (var con = new NpgsqlConnection(DbConnectionProvider.ConnectionString))
            {
                con.Open();

                string sql = @"SELECT *
                       FROM public.""Parts""
                       WHERE ""PartFamilyId""=@familyId
                       AND ""IsActive""=true
                       ORDER BY ""PartNumber""";

                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@familyId", SelectedFamilyId);

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    partList.Add(new PartMaster()
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        PartFamilyId = Convert.ToInt32(reader["PartFamilyId"]),
                        PartNumber = reader["PartNumber"]?.ToString() ?? string.Empty,
                        IsActive = Convert.ToBoolean(reader["IsActive"])
                    });
                }
            }

            grdParts.ItemsSource = partList;

            if (partList.Count == 0)
            {
                grdParts.Visibility = Visibility.Collapsed;
                txtPartsMessage.Visibility = Visibility.Visible;
                txtPartsMessage.Text = "No parts in selected family. Please add parts.";
            }
            else
            {
                grdParts.Visibility = Visibility.Visible;
                txtPartsMessage.Visibility = Visibility.Collapsed;
            }
        }

        private bool FamilyExists(string name, int ignoreId = 0)
        {
            using var con = new NpgsqlConnection(DbConnectionProvider.ConnectionString);
            con.Open();

            string sql = @"SELECT COUNT(1)
                           FROM public.""PartFamilies""
                           WHERE LOWER(""FamilyName"") = LOWER(@name)
                           AND ""IsActive"" = true
                           AND (@id = 0 OR ""Id"" <> @id)";

            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@id", ignoreId);

            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private bool PartExists(string partNo, int familyId, int ignoreId = 0)
        {
            using var con = new NpgsqlConnection(DbConnectionProvider.ConnectionString);
            con.Open();

            string sql = @"SELECT COUNT(1)
                           FROM public.""Parts""
                           WHERE LOWER(""PartNumber"") = LOWER(@partNo)
                           AND ""PartFamilyId"" = @familyId
                           AND ""IsActive"" = true
                           AND (@id = 0 OR ""Id"" <> @id)";

            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@partNo", partNo);
            cmd.Parameters.AddWithValue("@familyId", familyId);
            cmd.Parameters.AddWithValue("@id", ignoreId);

            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private void grdFamilies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var family = grdFamilies.SelectedItem as PartFamily;

            if (family == null)
            {
                SelectedFamilyId = 0;
                LoadParts();
                return;
            }

            SelectedFamilyId = family.Id;

            _isInternalUpdate = true;
            txtFamilyName.Text = family.FamilyName;
            _isInternalUpdate = false;

            btnFamilyAddSave.Content = "Save";

            SelectedPartId = 0;
            txtPartNumber.Clear();
            btnPartAddSave.Content = "Add";

            LoadParts();
        }

        private void grdParts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (grdParts.SelectedItem == null)
                return;

            var part = (PartMaster)grdParts.SelectedItem;

            SelectedPartId = part.Id;
            txtPartNumber.Text = part.PartNumber;

            btnPartAddSave.Content = "Save";
        }

        private void grdFamilies_Edit_Click(object sender, RoutedEventArgs e)
        {
            var item = ((FrameworkElement)sender).DataContext as PartFamily;
            if (item == null) return;

            SelectedFamilyId = item.Id;
            txtFamilyName.Text = item.FamilyName;

            btnFamilyAddSave.Content = "Save";

            grdFamilies.SelectedItem = item;
        }

        private void grdFamilies_Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = ((FrameworkElement)sender).DataContext as PartFamily;
            if (item == null) return;

            if (MessageBox.Show("Delete this family?", "Confirm",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            using var con = new NpgsqlConnection(DbConnectionProvider.ConnectionString);
            con.Open();

            string sql = @"UPDATE public.""PartFamilies""
                   SET ""IsActive""=false
                   WHERE ""Id""=@id";

            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.ExecuteNonQuery();
            _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Part Master", null, $"Deleted part family '{item.FamilyName}'");

            // RESET UI
            SelectedFamilyId = 0;
            txtFamilyName.Clear();
            btnFamilyAddSave.Content = "Add";
            grdFamilies.SelectedItem = null;

            grdParts.ItemsSource = null;

            LoadFamilies();
            LoadParts();
        }

        private void grdParts_Edit_Click(object sender, RoutedEventArgs e)
        {
            var item = ((FrameworkElement)sender).DataContext as PartMaster;
            if (item == null) return;

            SelectedPartId = item.Id;
            txtPartNumber.Text = item.PartNumber;

            btnPartAddSave.Content = "Save";

            grdParts.SelectedItem = item;
        }

        private void grdParts_Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = ((FrameworkElement)sender).DataContext as PartMaster;
            if (item == null) return;

            if (MessageBox.Show("Delete this part?", "Confirm",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            using var con = new NpgsqlConnection(DbConnectionProvider.ConnectionString);
            con.Open();

            string sql = @"UPDATE public.""Parts""
                   SET ""IsActive""=false
                   WHERE ""Id""=@id";

            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.ExecuteNonQuery();
            _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Part Master", null, $"Deleted part '{item.PartNumber}'");

            // RESET UI
            SelectedPartId = 0;
            txtPartNumber.Clear();
            btnPartAddSave.Content = "Add";
            grdParts.SelectedItem = null;

            LoadParts();
        }

        private void btnClose_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Close();
        }

        private void btnFamilyAddSave_Click(object sender, RoutedEventArgs e)
        {
            string name = txtFamilyName.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Family name cannot be empty.");
                return;
            }

            using var con = new NpgsqlConnection(DbConnectionProvider.ConnectionString);
            con.Open();

            if (SelectedFamilyId == 0)
            {
                // ADD
                if (FamilyExists(name))
                {
                    MessageBox.Show("Family already exists.");
                    return;
                }

                string sql = @"INSERT INTO public.""PartFamilies""(""FamilyName"") VALUES(@name)";
                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
                _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Part Master", null, $"Added part family '{name}'");

                MessageBox.Show("Family added.");
            }
            else
            {
                // SAVE (UPDATE)
                if (FamilyExists(name, SelectedFamilyId))
                {
                    MessageBox.Show("Duplicate family name.");
                    return;
                }

                string sql = @"UPDATE public.""PartFamilies""
                       SET ""FamilyName""=@name
                       WHERE ""Id""=@id";

                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@id", SelectedFamilyId);
                cmd.ExecuteNonQuery();
                _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Part Master", null, $"Updated part family '{name}'");

                MessageBox.Show("Family updated.");
            }

            // RESET
            txtFamilyName.Clear();
            SelectedFamilyId = 0;
            btnFamilyAddSave.Content = "Add";
            grdFamilies.SelectedItem = null;

            LoadFamilies();
        }

        private void btnPartAddSave_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFamilyId == 0)
            {
                MessageBox.Show("Please select a Family first.");
                return;
            }

            string partNo = txtPartNumber.Text.Trim();

            if (string.IsNullOrWhiteSpace(partNo))
            {
                MessageBox.Show("Part number cannot be empty.");
                return;
            }

            using var con = new NpgsqlConnection(DbConnectionProvider.ConnectionString);
            con.Open();

            if (SelectedPartId == 0)
            {
                // ADD
                if (PartExists(partNo, SelectedFamilyId))
                {
                    MessageBox.Show("Part already exists.");
                    return;
                }

                string sql = @"INSERT INTO public.""Parts""
                       (""PartFamilyId"", ""PartNumber"")
                       VALUES(@familyId, @partNo)";

                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@familyId", SelectedFamilyId);
                cmd.Parameters.AddWithValue("@partNo", partNo);
                cmd.ExecuteNonQuery();
                _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Part Master", null, $"Added part '{partNo}' to family");

                MessageBox.Show("Part added.");
            }
            else
            {
                // SAVE
                if (PartExists(partNo, SelectedFamilyId, SelectedPartId))
                {
                    MessageBox.Show("Duplicate part number.");
                    return;
                }

                string sql = @"UPDATE public.""Parts""
                       SET ""PartNumber""=@partNo
                       WHERE ""Id""=@id";

                using var cmd = new NpgsqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@partNo", partNo);
                cmd.Parameters.AddWithValue("@id", SelectedPartId);
                cmd.ExecuteNonQuery();
                _ = AppServices.ConfigChangeRepository.RecordChangeAsync("Part Master", null, $"Updated part '{partNo}'");

                MessageBox.Show("Part updated.");
            }

            // RESET
            txtPartNumber.Clear();
            SelectedPartId = 0;
            btnPartAddSave.Content = "Add";
            grdParts.SelectedItem = null;

            LoadParts();
        }

        private void txtFamilyName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInternalUpdate) return;

            if (string.IsNullOrWhiteSpace(txtFamilyName.Text))
            {
                SelectedFamilyId = 0;
                btnFamilyAddSave.Content = "Add";

                grdParts.ItemsSource = null;
                grdParts.Visibility = Visibility.Collapsed;

                txtPartsMessage.Visibility = Visibility.Visible;
                txtPartsMessage.Text = "Please select a family first";
            }
        }

        private void txtPartNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPartNumber.Text))
            {
                SelectedPartId = 0;
                btnPartAddSave.Content = "Add";
                grdParts.SelectedItem = null;
            }
            else if (SelectedPartId != 0)
            {
                btnPartAddSave.Content = "Save";
            }
        }
    }
}