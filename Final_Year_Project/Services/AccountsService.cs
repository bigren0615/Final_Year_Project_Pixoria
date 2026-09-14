using Final_Year_Project.Models.DB;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;


namespace Final_Year_Project.Services
{
    public class AccountsService
    {
        private readonly SupabaseService _supabaseService;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public AccountsService(SupabaseService supabaseService, IWebHostEnvironment env, IConfiguration config, IHttpClientFactory httpFactory)
        {
            _supabaseService = supabaseService;
            _env = env;
            _config = config;
            _http = httpFactory.CreateClient();
        }

        public string GenerateToken()
        {
            var allChar = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@#$%&+=-_/?*!|";
            var random = new Random();
            var Token = new string(Enumerable.Repeat(allChar, 50).Select(token => token[random.Next(token.Length)]).ToArray());
            return Token.ToString();
        }

        public string HashPassword(string password)
        {
            var sha = SHA256.Create();
            var hashedBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
        }

        public Attachment Logo()
        {
            var path = Path.Combine(_env.WebRootPath, "images", "pixoria_icon.png");
            var a = new Attachment(path);
            a.ContentId = "photo";
            return a;
        }

        public void SendResetPasswordEmail(Token token, Users user)
        {
            var mail = new MailMessage();
            mail.To.Add(new MailAddress(user.Email, user.Name));
            mail.Subject = "Reset Password";
            mail.IsBodyHtml = true;
            mail.Attachments.Add(Logo());

            mail.Body =
            $@"<center>
                <h2>Password Reset</h2>
                <p>Your verification code:</p>
                <h1>{token.OTP}</h1>
            </center>";

            SendEmail(mail);
        }

        public void SendPasswordChangeOtpEmail(Token token, Users user)
        {
            var mail = new MailMessage();
            mail.To.Add(new MailAddress(user.Email, user.Name));
            mail.Subject = "Password Change Verification";
            mail.IsBodyHtml = true;
            mail.Attachments.Add(Logo());

            mail.Body =
            $@"<center>
                <h2>Password Change Verification</h2>
                <p>Your verification code to change your password:</p>
                <h1>{token.OTP}</h1>
                <p style='color:#777;'>This code expires in 10 minutes.</p>
            </center>";

            SendEmail(mail);
        }

        public void SendForgotPasswordEmail(Users user, string resetUrl)
        {
            var mail = new MailMessage();
            mail.To.Add(new MailAddress(user.Email, user.Name));
            mail.Subject = "Reset Your Password";
            mail.IsBodyHtml = true;
            mail.Attachments.Add(Logo());

            mail.Body =
            $@"
            <div style='font-family:Arial, sans-serif; text-align:center;'>
                <img src=""cid:photo"" style='width:200px;height:200px;margin-bottom:20px;' />

                <h2 style='color:#333;'>Reset Your Password</h2>

                <p style='font-size:16px;color:#555;'>
                    Hello <b>{user.Name}</b>,<br/>
                    We received a request to reset your password. Click the button below to set a new password.
                </p>

                <a href='{resetUrl}'
                   style='background:#3aff00;
                          padding:14px 24px;
                          color:#000;
                          text-decoration:none;
                          font-size:16px;
                          border-radius:6px;
                          font-weight:bold;
                          display:inline-block;
                          cursor:pointer;'>
                   RESET PASSWORD
                </a>

                <p style='margin-top:30px;font-size:14px;color:#777;'>
                    This link will expire in 1 hour. If you did not request a password reset, you can safely ignore this email.
                </p>
            </div>
            ";

            SendEmail(mail);
        }

        public void SendChangePasswordEmail(Users user)
        {
            var mail = new MailMessage();
            mail.To.Add(new MailAddress(user.Email, user.Name));
            mail.Subject = "Password Changed";
            mail.IsBodyHtml = true;
            mail.Attachments.Add(Logo());

            mail.Body =
            @"<center>
                <h2>Your password has been changed</h2>
                <p>If this wasn't you, contact support immediately.</p>
            </center>";

            SendEmail(mail);
        }

        public void SendEmail(MailMessage mail)
        {
            string user = _config["Smtp:User"] ?? "";
            string pass = _config["Smtp:Pass"] ?? "";
            string name = _config["Smtp:Name"] ?? "";
            string host = _config["Smtp:Host"] ?? "";
            int port = _config.GetValue<int>("Smtp:Port");

            mail.From = new MailAddress(user, name);
            using var smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                Credentials = new NetworkCredential(user, pass)
            };
            try
            {
                smtp.Send(mail);
            }
            catch (SmtpException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task<bool> IsUsernameAvailable(string username)
        {
            var client = _supabaseService.GetClient();

            var result = await client
                .From<Users>()
                .Where(x => x.Username == username)
                .Get();

            var pending = await client
                .From<PendingUser>()
                .Where(x => x.Username == username)
                .Get();

            return !result.Models.Any() && !pending.Models.Any();
        }

        public async Task<bool> IsEmailAvailable(string email)
        {
            var client = _supabaseService.GetClient();

            var result = await client
                .From<Users>()
                .Where(x => x.Email == email)
                .Get();

            var pending = await client
                .From<PendingUser>()
                .Where(x => x.Email == email)
                .Get();

            return !result.Models.Any() && !pending.Models.Any();
        }

        public async Task<bool> IsPhoneAvailable(string phone)
        {
            var client = _supabaseService.GetClient();

            var result = await client
                .From<Users>()
                .Where(x => x.PhoneNumber == phone)
                .Get();

            var pending = await client
                .From<PendingUser>()
                .Where(x => x.PhoneNumber == phone)
                .Get();

            return !result.Models.Any() && !pending.Models.Any();
        }

    }
}
