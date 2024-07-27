using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using MiniGameServer.Models;
using System.Security.Cryptography;
using System.Text;

namespace MiniGameServer.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public string ConnectionString => _connectionString; // 添加这个属性

        private string ConvertBase64ToMd5(string base64Password)
        {
            var plainTextBytes = Convert.FromBase64String(base64Password);
            var md5 = MD5.Create();
            var hashedBytes = md5.ComputeHash(plainTextBytes);
            var sb = new StringBuilder();
            foreach (var b in hashedBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        public async Task<int> InsertMiniGameScoreAsync(MiniGameScore score)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "INSERT INTO MiniGameScore (UserId, SubmissionDate, Score) VALUES (@UserId, @SubmissionDate, @Score)";
                return await connection.ExecuteAsync(sql, score);
            }
        }

        public async Task<int> InsertCreateAccountAsync(CreateAccount account)
        {
            account.Password = ConvertBase64ToMd5(account.Password);
            Console.WriteLine($"created password: {account.Password}");
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "INSERT INTO MiniGameUser (Username, Nickname, Password, RegistrationDate) VALUES (@Username, @Nickname, @Password, @RegistrationDate)";
                return await connection.ExecuteAsync(sql, account);
            }
        }

        private string ConvertMd5(string password)
        {
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(password);
                var hashBytes = md5.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes);
            }
        }

        public async Task<bool> ValidateLoginAsync(Login login)
        {
            string passwordToCompare = ConvertBase64ToMd5(login.Password);
            Console.WriteLine($"login password: {passwordToCompare}");
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "SELECT COUNT(1) FROM MiniGameUser WHERE UID = @UID AND Username = @Username AND Password = @Password";
                return await connection.ExecuteScalarAsync<bool>(sql, new { login.UID, login.Username, Password = passwordToCompare });
            }
        }

        public async Task<int> InsertLoginRecordAsync(Login login)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "INSERT INTO MiniGameLogin (UID, LoginDate) VALUES (@UID, @LoginDate)";
                return await connection.ExecuteAsync(sql, new { login.UID, login.LoginDate });
            }
        }
    }
}