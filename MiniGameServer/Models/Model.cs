using System;

namespace MiniGameServer.Models
{


    public class Login
    {
        public int UID { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public DateTime LoginDate { get; set; }
    }

    public class ScoreModel
    {
        public int UserId { get; set; }
        public DateTime SubmissionDate { get; set; }
        public int Score { get; set; }
    }

    public class AccountModel
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


    public class LeaderboardRequestModel
    {
        public int UID { get; set; }
    }

    // 响应模型类
    public class LeaderboardResponseModel
    {
        public List<LeaderboardEntry> TopPlayers { get; set; }
        public int PlayerRank { get; set; }
        public int PlayerHighScore { get; set; }
    }

    // 排行榜条目类
    public class LeaderboardEntry
    {
        public string Username { get; set; }
        public int HighScore { get; set; }
    }

    // 玩家统计信息类
    public class PlayerStats
    {
        public int UID { get; set; }
        public int HighScore { get; set; }
        public int Rank { get; set; }
    }
}