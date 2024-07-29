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

        public async Task<int> InsertMiniGameScoreAsync(ScoreModel score)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "INSERT INTO MiniGameScore (UserId, SubmissionDate, Score) VALUES (@UserId, @SubmissionDate, @Score)";
                return await connection.ExecuteAsync(sql, score);
            }
        }



        public async Task<User> InsertAccountModelAsync(AccountModel newAccount)
        {
            newAccount.Password = ConvertBase64ToMd5(newAccount.Password);
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = @"
            INSERT INTO MiniGameUser (Username, Nickname, Password, RegistrationDate)
            VALUES (@Username, @Nickname, @Password, @RegistrationDate)
            RETURNING UID, Username, Nickname, RegistrationDate";
                return await connection.QueryFirstOrDefaultAsync<User>(sql, new { newAccount.Username, newAccount.Nickname, newAccount.Password, newAccount.RegistrationDate });
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

        public async Task<bool> ValidateLoginAsync(LoginModel login)
        {
            string passwordToCompare = ConvertBase64ToMd5(login.Password);
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "SELECT COUNT(1) FROM MiniGameUser WHERE Username = @Username AND Password = @Password";
                return await connection.ExecuteScalarAsync<bool>(sql, new { login.Username, Password = passwordToCompare });
            }
        }

        public async Task<int> InsertLoginRecordAsync(LoginModel login)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "INSERT INTO MiniGameLogin (UID, LoginDate) VALUES (@UID, @LoginDate)";
                return await connection.ExecuteAsync(sql, new { login.UID, login.LoginDate });
            }
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "SELECT UID, Username FROM MiniGameUser WHERE Username = @Username LIMIT 1";
                return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
            }
        }
        public class User
        {
            public int UID { get; set; }
            public string Username { get; set; }
        }


        public async Task<List<LeaderboardEntry>> GetTop100LeaderboardAsync()
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = @"
            SELECT 
                u.Nickname AS Username,
                MAX(s.Score) AS HighScore
            FROM 
                MiniGameScore AS s
            INNER JOIN 
                MiniGameUser AS u
            ON 
                s.UserId = u.UID
            GROUP BY 
                u.Nickname
            ORDER BY 
                HighScore DESC
            LIMIT 100";

                var result = await connection.QueryAsync<LeaderboardEntry>(sql);
                return result.ToList();
            }
        }

        public async Task<PlayerStats> GetPlayerStatsAsync(int playerUID)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                // 获取玩家的最高分
                var highScoreSql = @"
                    SELECT MAX(Score) AS HighScore
                    FROM MiniGameScore
                    WHERE userId = @UID";
                var highScore = await connection.QueryFirstOrDefaultAsync<int?>(highScoreSql, new { UID = playerUID });

                if (highScore == null)
                {
                    Console.WriteLine($"Player UID: {playerUID} hasn't submitted any scores yet");
                    return new PlayerStats
                    {
                        UID = playerUID,
                        HighScore = highScore ?? 0,
                        Rank = 9999
                    };
                    return null; // 玩家没有提交过分数
                }

                // 计算玩家的排名
                var rankSql = @"
                    SELECT COUNT(*) + 1 AS Rank
                    FROM (SELECT MAX(Score) AS HighScore
                          FROM MiniGameScore
                          GROUP BY userId) AS RankedScores
                    WHERE HighScore > @HighScore";
                var playerRank = await connection.QueryFirstOrDefaultAsync<int>(rankSql, new { HighScore = highScore });

                return new PlayerStats
                {
                    UID = playerUID,
                    HighScore = highScore ?? 0,
                    Rank = playerRank
                };
            }
        }


        public async Task<int?> GetHighestScoreAsync(int uid)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                var sql = "SELECT MAX(Score) AS HighScore FROM MiniGameScore WHERE UserId = @UID";
                return await connection.QueryFirstOrDefaultAsync<int?>(sql, new { UID = uid });
            }
        }





    }
}