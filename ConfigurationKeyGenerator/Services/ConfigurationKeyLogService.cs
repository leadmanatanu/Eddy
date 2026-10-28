using System;
using System.Collections.Generic;
using ConfigurationKeyGenerator.Models;
using Npgsql;
using Shared.MagkraftTools;

namespace ConfigurationKeyGenerator.Services
{
    public class ConfigurationKeyLogService
    {
        private readonly string _connectionString;

        public ConfigurationKeyLogService(string? connectionString = null)
        {
            _connectionString = DatabaseBootstrap.ResolveConnectionString(connectionString);
        }

        public string? GetRegistrySerial(string machineId)
        {
            using var con = new NpgsqlConnection(_connectionString);
            con.Open();

            string query = "SELECT serial_number FROM machine_registry WHERE LOWER(machine_id) = LOWER(@machineId) LIMIT 1;";
            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("@machineId", machineId.Trim());

            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        public void Save(ConfigurationKeyLog log)
        {
            using var con = new NpgsqlConnection(_connectionString);
            con.Open();

            string query = @"
                INSERT INTO ConfigurationKeyLogs
                (
                    ProductName,
                    CustomerName,
                    MachineId,
                    ConfigurationFileName,
                    GeneratedFileName,
                    GeneratedFile,
                    SettingsJson,
                    GeneratedDate
                )
                VALUES
                (
                    @product,
                    @customer,
                    @machine,
                    @configFile,
                    @generatedFile,
                    @file,
                    @settingsJson,
                    NOW()
                );";

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("@product", log.ProductName);
            cmd.Parameters.AddWithValue("@customer", log.CustomerName);
            cmd.Parameters.AddWithValue("@machine", log.MachineId);
            cmd.Parameters.AddWithValue("@configFile", log.ConfigurationFileName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@generatedFile", log.GeneratedFileName);
            cmd.Parameters.AddWithValue("@file", log.GeneratedFile);
            cmd.Parameters.AddWithValue("@settingsJson", (object?)log.SettingsJson ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public void Update(ConfigurationKeyLog log)
        {
            using var con = new NpgsqlConnection(_connectionString);
            con.Open();

            string query = @"
                UPDATE ConfigurationKeyLogs
                SET
                    ProductName = @product,
                    CustomerName = @customer,
                    MachineId = @machine,
                    ConfigurationFileName = @configFile,
                    GeneratedFileName = @generatedFile,
                    GeneratedFile = @file,
                    SettingsJson = @settingsJson,
                    UpdatedDate = NOW()
                WHERE Id = @id;";

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("@id", log.Id);
            cmd.Parameters.AddWithValue("@product", log.ProductName);
            cmd.Parameters.AddWithValue("@customer", log.CustomerName);
            cmd.Parameters.AddWithValue("@machine", log.MachineId);
            cmd.Parameters.AddWithValue("@configFile", log.ConfigurationFileName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@generatedFile", log.GeneratedFileName);
            cmd.Parameters.AddWithValue("@file", log.GeneratedFile);
            cmd.Parameters.AddWithValue("@settingsJson", (object?)log.SettingsJson ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public List<ConfigurationKeyLog> GetAll()
        {
            var list = new List<ConfigurationKeyLog>();

            using var con = new NpgsqlConnection(_connectionString);
            con.Open();

            string query = "SELECT Id, ProductName, CustomerName, MachineId, ConfigurationFileName, GeneratedFileName, GeneratedFile, GeneratedDate, UpdatedDate, SettingsJson FROM ConfigurationKeyLogs ORDER BY Id DESC;";

            using var cmd = new NpgsqlCommand(query, con);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new ConfigurationKeyLog
                {
                    Id = reader.GetInt32(0),
                    ProductName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    CustomerName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    MachineId = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ConfigurationFileName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    GeneratedFileName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    GeneratedFile = reader.IsDBNull(6) ? Array.Empty<byte>() : (byte[])reader[6],
                    GeneratedDate = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                    UpdatedDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    SettingsJson = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }

            return list;
        }

        public bool ExistsByMachineId(string machineId)
        {
            using var con = new NpgsqlConnection(_connectionString);
            con.Open();

            string query = "SELECT COUNT(1) FROM ConfigurationKeyLogs WHERE LOWER(MachineId) = LOWER(@machineId);";

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("@machineId", machineId.Trim());

            int count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }
    }
}
