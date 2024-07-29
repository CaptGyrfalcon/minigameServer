using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MiniGameServer.Data;
using MiniGameServer.Models;
using Npgsql;

namespace MiniGameServer.Controllers
{



    [ApiController]
    [Route("[controller]")]
    public class ScoresController : ControllerBase
    {


        private readonly string _leaderboardIniPath;
        private readonly ILogger<ScoresController> _logger;
        private readonly DatabaseHelper _databaseHelper;

        public ScoresController(ILogger<ScoresController> logger, DatabaseHelper databaseHelper)
        {
            _logger = logger;
            _databaseHelper = databaseHelper;
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string iniRelativePath = "PlagueIncAntiCheatingService\\WebAPI\\public\\Minigame\\minigame.txt";
            _leaderboardIniPath = Path.Combine(desktopPath, iniRelativePath);
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitScore([FromBody] ScoreModel submitScore)
        {
            if (submitScore == null)
            {
                return BadRequest("Invalid data.");
            }

            var score = new ScoreModel
            {
                UserId = submitScore.UserId,
                SubmissionDate = submitScore.SubmissionDate,
                Score = submitScore.Score
            };
            

            await _databaseHelper.InsertMiniGameScoreAsync(score);


            //
            var leaderboard = await _databaseHelper.GetTop100LeaderboardAsync();

            // 检查数据库查询结果
            if (leaderboard == null)
            {
                throw new Exception("Failed to retrieve leaderboard data.");
            }
            List<string> entries = new List<string>();
            int i = 1;
            foreach(LeaderboardEntry en in leaderboard)
            {
                entries.Add($"{i},{en.Username},{en.HighScore}");
                i++;
            }
            string content = String.Join("\n", entries.ToArray());
            WriteToFileWhenAvailable(_leaderboardIniPath, content);
            //


            // Obtain the highest score and ranking
            using (var connection = new NpgsqlConnection(_databaseHelper.ConnectionString))
            {
                var usersHighestScores = await connection.QueryAsync<ScoreModel>(
                    "SELECT UserId, MAX(Score) AS Score FROM MiniGameScore GROUP BY UserId");

                var rank = usersHighestScores
                    .OrderByDescending(s => s.Score)
                    .ToList()
                    .FindIndex(s => s.UserId == score.UserId) + 1;
                Console.WriteLine($"UserId: {score.UserId}, SubmissionDate: {score.SubmissionDate}, Score: {score.Score}");
                return Ok(new { state = "success", rank });
            }
        }

        [HttpPost("createAccount")]
        public async Task<IActionResult> AccountModel([FromBody] AccountModel account)
        {
            if (account == null || string.IsNullOrEmpty(account.Username) || string.IsNullOrEmpty(account.Password))
            {
                return BadRequest("Invalid data.");
            }

            // 检查用户名是否已存在
            var existingUser = await _databaseHelper.GetUserByUsernameAsync(account.Username);
            if (existingUser != null)
            {
                return Conflict(new { state = "failed", message = "Username already exists." });
            }

            var newAccount = new AccountModel
            {
                Username = account.Username,
                Nickname = account.Nickname,
                Password = account.Password,
                RegistrationDate = DateTime.UtcNow
            };

            // 向数据库插入新的用户数据并获取新生成的 UID
            var createdUser = await _databaseHelper.InsertAccountModelAsync(newAccount);

            if (createdUser != null)
            {
                Console.WriteLine($"UID: {createdUser.UID}, Username: {createdUser.Username}, Nickname: {newAccount.Nickname}, RegistrationDate: {newAccount.RegistrationDate}");
                return Ok(new { state = "success", UID = createdUser.UID });
            }

            return StatusCode(500, new { state = "failed", message = "Account creation failed." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel login)
        {
            if (login == null || string.IsNullOrEmpty(login.Username) || string.IsNullOrEmpty(login.Password))
            {
                return BadRequest("Invalid data.");
            }

            // 查询数据库以获取用户名对应的 UID
            var user = await _databaseHelper.GetUserByUsernameAsync(login.Username);
            if (user == null)
            {
                return Unauthorized(new { state = "failed", message = "USER_NOT_EXIST" });
            }

            var loginDetails = new LoginModel
            {
                UID = user.UID, // 从数据库查询到的 UID
                Username = user.Username,
                Password = login.Password,
                LoginDate = DateTime.UtcNow // 设置当前时间为登录时间
            };

            Console.WriteLine($"UID: {loginDetails.UID}, Username: {loginDetails.Username}, LoginDate: {loginDetails.LoginDate}");

            if (await _databaseHelper.ValidateLoginAsync(loginDetails))
            {
                await _databaseHelper.InsertLoginRecordAsync(loginDetails);
                return Ok(new { state = "success", uid = user.UID });
            }

            return Unauthorized(new { state = "failed", message = "INCORRECT_PASSWORD" });
        }

        [HttpPost("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromBody] LeaderboardRequestModel request)
        {
            if (request == null || request.UID == 0)
            {
                return BadRequest("Invalid data.");
            }

            try
            {
                // 获取前100名玩家的昵称和分数
                var leaderboard = await _databaseHelper.GetTop100LeaderboardAsync();

                // 检查数据库查询结果
                if (leaderboard == null)
                {
                    throw new Exception("Failed to retrieve leaderboard data.");
                }

                // 获取当前玩家的排名和最高分
                var playerStats = await _databaseHelper.GetPlayerStatsAsync(request.UID);

                // 检查数据库查询结果
                if (playerStats == null)
                {
                    throw new Exception($"Failed to retrieve player stats for UID: {request.UID}");
                }

                var response = new LeaderboardResponseModel
                {
                    TopPlayers = leaderboard,
                    PlayerRank = playerStats?.Rank ?? -1, // 若未找到玩家，设为-1
                    PlayerHighScore = playerStats?.HighScore ?? 0
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                // 记录详细错误日志
                _logger.LogError(ex, "An error occurred while processing the leaderboard request.");

                // 返回出错信息
                return StatusCode(500, new { state = "failed", message = "An error occurred while processing the request.", error = ex.Message });
            }
        }

        public static void WriteToFileWhenAvailable(string filePath, string content)
        {
            bool fileAvailable = false;
            while (!fileAvailable)
            {
                try
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // 文件可以被读写，认为文件是可用的
                        fileAvailable = true;
                    }
                }
                catch (IOException)
                {
                    // 文件正在被占用，等待一会儿后重试
                    Console.WriteLine("File is in use, waiting...");
                    Thread.Sleep(100);
                }
            }

            // 文件可用后进行写操作
            using (StreamWriter writer = new StreamWriter(filePath, append: false))
            {
                writer.Write(content);
                Console.WriteLine("Content written to file.");
            }
        }


        [HttpGet("highestScore/{uid}")]
        public async Task<IActionResult> GetHighestScore(int uid)
        {
            Console.WriteLine($"high score request uid: {uid}");

            if (uid <= 0)
            {
                Console.WriteLine($"high score failed uid: {uid}");
                return BadRequest("Invalid UID.");
            }

            try
            {
                var highestScore = await _databaseHelper.GetHighestScoreAsync(uid);
                Console.WriteLine($"high score state: {highestScore.HasValue}");

                if (!highestScore.HasValue)
                {
                    Console.WriteLine("User has no scores submitted.");
                    return Ok(new { state = "success", UID = uid, highScore = 0 });
                }

                Console.WriteLine($"high score value: {highestScore.Value}");
                return Ok(new { state = "success", UID = uid, highScore = highestScore.Value });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "An error occurred while fetching the highest score.");
                return StatusCode(500, new { state = "failed", message = "An error occurred while processing the request.", error = ex.Message });
            }
        }



    }




}