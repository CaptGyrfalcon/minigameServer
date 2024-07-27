using System;

namespace MiniGameServer.Models
{
    public class MiniGameScore
    {
        public int UserId { get; set; }
        public DateTime SubmissionDate { get; set; }
        public int Score { get; set; }
    }

    public class CreateAccount
    {
        public int UID { get; set; }
        public string Username { get; set; }
        public string Nickname { get; set; }
        public string Password { get; set; }
        public DateTime RegistrationDate { get; set; }
    }

    public class Login
    {
        public int UID { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public DateTime LoginDate { get; set; }
    }
}