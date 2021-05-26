using NorthwindWebApi.Entities;

namespace NorthwindWebApi.Models.Accounts

{
    public class Response
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public Token Token { get; set; }
        public RefreshToken RefreshToken { get; set; }
    }
}