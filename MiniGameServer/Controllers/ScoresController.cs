using System.Linq;
using System.Reflection;
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
        public class SubmitScoreModel
        {
            public int UserId { get; set; }
            public DateTime SubmissionDate { get; set; }
            public int Score { get; set; }
        }

        public class CreateAccountModel
        {
            public int UID { get; set; }
            public string Username { get; set; }
            public string Nickname { get; set; }
            public string Password { get; set; }
            public DateTime RegistrationDate { get; set; }
        }

        public class LoginModel
        {
            public int UID { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public DateTime LoginDate { get; set; }
        }
        private readonly ILogger<ScoresController> _logger;
        private readonly DatabaseHelper _databaseHelper;

        public ScoresController(ILogger<ScoresController> logger, DatabaseHelper databaseHelper)
        {
            _logger = logger;
            _databaseHelper = databaseHelper;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitScore([FromBody] SubmitScoreModel submitScore)
        {
            if (submitScore == null)
            {
                return BadRequest("Invalid data.");
            }

            var score = new MiniGameScore
            {
                UserId = submitScore.UserId,
                SubmissionDate = submitScore.SubmissionDate,
                Score = submitScore.Score
            };
            

            await _databaseHelper.InsertMiniGameScoreAsync(score);

            // Obtain the highest score and ranking
            using (var connection = new NpgsqlConnection(_databaseHelper.ConnectionString))
            {
                var usersHighestScores = await connection.QueryAsync<MiniGameScore>(
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
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountModel account)
        {
            if (account == null)
            {
                return BadRequest("Invalid data.");
            }

            var newAccount = new CreateAccount
            {
                UID = account.UID,
                Username = account.Username,
                Nickname = account.Nickname,
                Password = account.Password,
                RegistrationDate = account.RegistrationDate
            };

            await _databaseHelper.InsertCreateAccountAsync(newAccount);
            Console.WriteLine($"UID: {newAccount.UID}, Username: {newAccount.Username}, Nickname: {newAccount.Nickname}, RegistrationDate: {newAccount.RegistrationDate}");
            return Ok(new { state = "success" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel login)
        {
            if (login == null)
            {
                return BadRequest("Invalid data.");
            }

            var loginDetails = new Login
            {
                UID = login.UID,
                Username = login.Username,
                Password = login.Password,
                LoginDate = login.LoginDate
            };
            Console.WriteLine($"UID: {loginDetails.UID}, Username: {loginDetails.Username}, LoginDate: {loginDetails.LoginDate}");
            if (await _databaseHelper.ValidateLoginAsync(loginDetails))
            {
                await _databaseHelper.InsertLoginRecordAsync(loginDetails);
                return Ok(new { state = "success" });
            }

            return Unauthorized(new { state = "failed" });
        }
    }
    
}