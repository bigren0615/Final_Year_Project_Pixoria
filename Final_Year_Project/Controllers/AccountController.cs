using Final_Year_Project.Enums;
using Final_Year_Project.Models.Account;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Supabase.Gotrue;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;


namespace Final_Year_Project.Controllers
{
    public class AccountController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly AccountsService _accountService;
        private readonly EnumService _enumService;
        private readonly LocalStorageService _storageService;
        public string usernamePattern = @"^[\w]*$";
        public string namePattern = @"^[a-zA-Z][a-zA-Z ]*$";
        public string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[a-zA-Z\d!@#$%^&*()_=+-]*$";
        public string emailPattern = @"\w+([\.-]?\w+)*@\w+([\.-]?\w+)*(\.\w{2,3})";
        public string phonePattern = @"[0-9]{10,12}";

        public AccountController(SupabaseService supabaseService, AccountsService accountService, EnumService enumService, LocalStorageService storageService)
        {
            _supabaseService = supabaseService;
            _accountService = accountService;
            _enumService = enumService;
            _storageService = storageService;
        }

        [Authorize]
        [HttpGet]
        [Route("Account/setting")]
        public async Task<IActionResult> AccountSetting()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Index", "Home");

            var client = _supabaseService.GetClient();
            var response = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, int.Parse(uid))
                .Get();

            var user = response.Models.FirstOrDefault();
            if (user == null)
                return RedirectToAction("Index", "Home");

            var vm = new Models.Account.AccountSettingViewModel
            {
                UId = user.Uid,
                Name = user.Name,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                DOB = user.DOB,
                Gender = user.Gender,
                ProfilePicUrl = string.IsNullOrEmpty(user.ProfilePic) ? null : _storageService.BuildFileUrl("profile_pic", user.ProfilePic)
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [Route("Account/setting")]
        public async Task<IActionResult> AccountSetting(Models.Account.AccountSettingViewModel vm)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Index", "Home");

            var uidInt = int.Parse(uid);

            // Validate Name
            if (!Regex.IsMatch(vm.Name, namePattern))
                ModelState.AddModelError(nameof(vm.Name), "Name must contain letters only.");

            if (vm.Name.Length is < 1 or > 100)
                ModelState.AddModelError(nameof(vm.Name), "Name length must be between 1–100.");

            // Validate Username
            if (!Regex.IsMatch(vm.Username, usernamePattern))
                ModelState.AddModelError(nameof(vm.Username), "Only letters, numbers, and '_' allowed.");

            if (vm.Username.Length is < 1 or > 100)
                ModelState.AddModelError(nameof(vm.Username), "Username length must be between 1–100.");

            // Check if username is unique (excluding current user)
            var client = _supabaseService.GetClient();
            var userCheck = await client
                .From<Users>()
                .Filter("username", Supabase.Postgrest.Constants.Operator.Equals, vm.Username)
                .Get();

            var existingUser = userCheck.Models.FirstOrDefault();
            if (existingUser != null && existingUser.Uid != uidInt)
                ModelState.AddModelError(nameof(vm.Username), "Username already taken.");

            // Validate Email
            if (!Regex.IsMatch(vm.Email ?? string.Empty, emailPattern))
                ModelState.AddModelError(nameof(vm.Email), "Invalid email format.");

            // Check if email is unique (excluding current user)
            var emailCheck = await client
                .From<Users>()
                .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, vm.Email)
                .Get();

            var existingEmailUser = emailCheck.Models.FirstOrDefault();
            if (existingEmailUser != null && existingEmailUser.Uid != uidInt)
                ModelState.AddModelError(nameof(vm.Email), "Email already used by another account.");

            if (!ModelState.IsValid)
                return View(vm);

            // Update user
            var userResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Get();

            var user = userResponse.Models.FirstOrDefault();
            if (user == null)
                return RedirectToAction("Index", "Home");

            user.Name = vm.Name.Trim();
            user.Username = vm.Username.Trim();
            user.Email = vm.Email!.Trim();

            // Handle profile picture upload / removal
            var removeProfileFlag = Request.Form["RemoveProfilePic"].FirstOrDefault();
            var removeProfile = !string.IsNullOrEmpty(removeProfileFlag) && removeProfileFlag.ToLower() == "true";

            if (vm.ProfilePicFile != null)
            {
                // replace existing
                if (!string.IsNullOrEmpty(user.ProfilePic))
                    _storageService.Delete("profile_pic", user.ProfilePic);

                var saved = await _storageService.SaveAsync(vm.ProfilePicFile, "profile_pic", new[] { "image/jpeg", "image/png", "image/webp" });
                if (!string.IsNullOrEmpty(saved))
                    user.ProfilePic = saved;
            }
            else if (removeProfile)
            {
                if (!string.IsNullOrEmpty(user.ProfilePic))
                    _storageService.Delete("profile_pic", user.ProfilePic);
                user.ProfilePic = null;
            }

            // Update gender and DOB
            user.Gender = vm.Gender ?? string.Empty;
            user.DOB = vm.DOB;

            await client.From<Users>().Update(user);

            // Persist changes
            await client.From<Users>().Update(user);

            // Refresh authentication cookie so email claim is up-to-date
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Uid.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTime.UtcNow.AddHours(3)
                });

            TempData["AlertClass"] = "alert-success";
            TempData["Info"] = "Account settings updated successfully.";

            return Redirect("/Account/setting");
        }

        [Authorize]
        [HttpGet]
        [Route("Account/change-password")]
        public IActionResult ChangePassword()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Index", "Home");
            // return an empty view model to enable tag helpers & unobtrusive validation
            return View(new Models.Account.ChangePasswordViewModel());
        }

        [Authorize]
        [HttpPost]
        [Route("Account/change-password")]
        public async Task<IActionResult> ChangePassword(Models.Account.ChangePasswordViewModel vm)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Index", "Home");
            // Use view model + data annotations to validate basic rules first
            if (!ModelState.IsValid)
                return View(vm);

            // Verify current password
            var client = _supabaseService.GetClient();
            var uidInt = int.Parse(uid);

            using var sha = SHA256.Create();
            var hashedCurrentBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(vm.CurrentPassword));
            var hashedCurrent = BitConverter.ToString(hashedCurrentBytes).Replace("-", "").ToLower();

            var response = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Get();

            var user = response.Models.FirstOrDefault();
            if (user == null || user.Password != hashedCurrent)
            {
                ModelState.AddModelError(nameof(vm.CurrentPassword), "Current password is incorrect.");
                return View(vm);
            }

            // Check if new password is same as current password
            var hashedNewBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(vm.NewPassword));
            var hashedNew = BitConverter.ToString(hashedNewBytes).Replace("-", "").ToLower();

            if (hashedCurrent == hashedNew)
            {
                ModelState.AddModelError(nameof(vm.NewPassword), "New password cannot be the same as your current password.");
                return View(vm);
            }

            // Verify OTP
            var tokenResponse = await client
                .From<Token>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Filter("otp", Supabase.Postgrest.Constants.Operator.Equals, vm.OtpCode)
                .Get();

            var token = tokenResponse.Models.FirstOrDefault();
            if (token == null || token.Expire < DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(vm.OtpCode), "Invalid or expired OTP code.");
                return View(vm);
            }

            // Delete the used OTP token
            await client.From<Token>().Delete(token);

            // Update password
            user.Password = hashedNew;
            await client.From<Users>().Update(user);

            // Notify user via email that their password has been changed
            try
            {
                _accountService.SendChangePasswordEmail(user);
            }
            catch
            {
                // swallow email errors; do not prevent password change completion
            }

            TempData["AlertClass"] = "alert-success";
            TempData["Info"] = "Password changed successfully.";

            return RedirectToAction("AccountSetting");
        }

        [Authorize]
        [HttpPost]
        [Route("Account/send-change-password-otp")]
        public async Task<IActionResult> SendChangePasswordOtp()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid))
                return Json(new { success = false, message = "User not authenticated." });

            var client = _supabaseService.GetClient();
            var uidInt = int.Parse(uid);

            // Get user details
            var userResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Get();

            var user = userResponse.Models.FirstOrDefault();
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            // Generate 6-digit OTP using cryptographically secure random number generator
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            // Delete any existing OTP tokens for this user
            var existingTokens = await client
                .From<Token>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Get();

            foreach (var t in existingTokens.Models)
            {
                await client.From<Token>().Delete(t);
            }

            // Create new OTP token (valid for 10 minutes)
            var otpToken = new Token
            {
                TokenValue = _accountService.GenerateToken(),
                OTP = otp,
                Expire = DateTime.UtcNow.AddMinutes(10),
                UId = uidInt,
                email = user.Email
            };

            await client.From<Token>().Insert(otpToken);

            // Send OTP email
            try
            {
                _accountService.SendPasswordChangeOtpEmail(otpToken, user);
                return Json(new { success = true, message = "OTP sent to your email." });
            }
            catch
            {
                return Json(new { success = false, message = "Failed to send OTP email." });
            }
        }

        [HttpGet]
        public IActionResult Login()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(uid))
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var client = _supabaseService.GetClient();

            var identifier = (model.Identifier ?? string.Empty).Trim();

            using var sha = SHA256.Create();
            var hashedBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(model.Password));
            var hashedPassword = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();

            Users? user = null;

            // Try by email first (if it looks like an email)
            if (identifier.Contains("@"))
            {
                var resp = await client
                    .From<Users>()
                    .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, identifier)
                    .Filter("password", Supabase.Postgrest.Constants.Operator.Equals, hashedPassword)
                    .Get();

                user = resp.Models.FirstOrDefault();
            }

            // Fallback to username
            if (user == null)
            {
                var resp2 = await client
                    .From<Users>()
                    .Filter("username", Supabase.Postgrest.Constants.Operator.Equals, identifier)
                    .Filter("password", Supabase.Postgrest.Constants.Operator.Equals, hashedPassword)
                    .Get();

                user = resp2.Models.FirstOrDefault();
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login credentials.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Uid.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(3)
                });

            // Redirect admin users to fraud detection dashboard
            if (user.Role == "admin")
            {
                return RedirectToAction("ScanAndMonitor", "ArtworkFraudDetection");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register(string role)
        {
            if (string.IsNullOrEmpty(role))
                role = "customer";

            var vm = new RegisterViewModel
            {
                Role = role
            };

            return View(vm);
        }


        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // USERNAME VALIDATION
            if (!Regex.IsMatch(vm.Username, usernamePattern))
                ModelState.AddModelError(nameof(vm.Username), "Only letters, numbers, and '_' allowed.");

            if (vm.Username.Length is < 1 or > 100)
                ModelState.AddModelError(nameof(vm.Username), "Username length must be between 1–100.");

            if (!await _accountService.IsUsernameAvailable(vm.Username))
                ModelState.AddModelError(nameof(vm.Username), "Username already taken.");

            // NAME VALIDATION
            if (!Regex.IsMatch(vm.Name, namePattern))
                ModelState.AddModelError(nameof(vm.Name), "Name must contain letters only.");

            if (vm.Name.Length is < 1 or > 100)
                ModelState.AddModelError(nameof(vm.Name), "Name length must be between 1–100.");

            // PASSWORD VALIDATION
            if (!Regex.IsMatch(vm.Password, passwordPattern))
                ModelState.AddModelError(nameof(vm.Password), "Must contain uppercase, lowercase, and digit.");

            if (vm.Password.Length is < 8 or > 30)
                ModelState.AddModelError(nameof(vm.Password), "Password must be between 8–30 characters.");

            if (vm.Password != vm.ConfirmPassword)
                ModelState.AddModelError(nameof(vm.ConfirmPassword), "Password and Confirm Password do not match.");

            // EMAIL VALIDATION
            if (!Regex.IsMatch(vm.Email, emailPattern))
                ModelState.AddModelError(nameof(vm.Email), "Invalid email format.");

            if (!await _accountService.IsEmailAvailable(vm.Email))
                ModelState.AddModelError(nameof(vm.Email), "Email already used.");

            // PHONE VALIDATION
            if (!await _accountService.IsPhoneAvailable(vm.PhoneNumber))
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Phone number already used.");

            if (!ModelState.IsValid)
                return View(vm);

            // CREATE PENDING USER
            var token = _accountService.GenerateToken();

            var pending = new PendingUser
            {
                Token = token,
                Username = vm.Username.Trim(),
                Name = vm.Name.Trim(),
                Password = _accountService.HashPassword(vm.Password),
                PhoneNumber = vm.PhoneNumber,
                Gender = vm.Gender,
                Email = vm.Email,
                Role = vm.Role == "artist"
                        ? _enumService.ToStringValue(usersRole.artist)
                        : _enumService.ToStringValue(usersRole.customer),
                CreatedAt = DateTime.UtcNow
            };

            // Only Artists have Nickname
            if (vm.Role == "artist")
                pending.NickName = vm.Nickname;

            var client = _supabaseService.GetClient();
            await client.From<PendingUser>().Insert(pending);

            SendVerifyEmail(pending, token);

            TempData["AlertClass"] = "alert-warning";
            TempData["Info"] = "Please verify your account via email.";

            return RedirectToAction("VerifyRegister");
        }



        [HttpGet]
        public IActionResult VerifyRegister()
        {
            return View();
        }


        public void SendVerifyEmail(PendingUser user, string tokenvalue)
        {
            var url = Url.Action(
                    "CompleteVerify",
                    "Account",
                    new { token = tokenvalue },
                    "https"
                );

            var mail = new MailMessage();
            mail.To.Add(new MailAddress(user.Email, user.Name));
            mail.Subject = "Verify Account";
            mail.IsBodyHtml = true;

            var inlineLogo = _accountService.Logo();
            mail.Attachments.Add(inlineLogo);

            // HTML body
            mail.Body =
                $@"
                <div style='font-family:Arial, sans-serif; text-align:center;'>
                    <img src=""cid:photo"" style='width:200px;height:200px;margin-bottom:20px;' />

                    <h2 style='color:#333;'>Verify Your Account</h2>

                    <p style='font-size:16px;color:#555;'>
                        Hello <b>{user.Username}</b>,<br/>
                        Please click the button below to verify your account.
                    </p>

                    <a href='{url}'
                       style='background:#3aff00;
                              padding:14px 24px;
                              color:#000;
                              text-decoration:none;
                              font-size:16px;
                              border-radius:6px;
                              font-weight:bold;
                              display:inline-block;
                              cursor:pointer;'>
                       VERIFY ACCOUNT
                    </a>

                    <p style='margin-top:30px;font-size:14px;color:#777;'>
                        If you did not request this, you can safely ignore this email.
                    </p>
                </div>
                ";

            _accountService.SendEmail(mail);
        }

        [HttpGet]
        public async Task<IActionResult> CompleteVerify(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["AlertClass"] = "alert-danger";
                TempData["Info"] = "Invalid token!";
                return RedirectToAction("Login");
            }

            var client = _supabaseService.GetClient();

            var pendingResponse = await client
                .From<PendingUser>()
                .Filter("token", Supabase.Postgrest.Constants.Operator.Equals, token)
                .Get();

            var pending = pendingResponse.Models.FirstOrDefault();

            if (pending == null)
            {
                TempData["AlertClass"] = "alert-danger";
                TempData["Info"] = "Invalid or expired token!";
                return RedirectToAction("Login");
            }

            var newUser = new Users
            {
                Username = pending.Username,
                Name = pending.Name,
                Password = pending.Password,
                PhoneNumber = pending.PhoneNumber,
                Gender = pending.Gender,
                Email = pending.Email,
                Role = pending.Role,
                Nickname = pending.NickName,   // ✅ ADD THIS
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };

            await client.From<Users>().Insert(newUser);

            await client.From<PendingUser>().Delete(pending);

            TempData["AlertClass"] = "alert-success";
            TempData["Info"] = "Your account has been verified. Please log in.";

            return RedirectToAction("Login");
        }


        [HttpGet]
        public async Task<IActionResult> ResendVerification()
        {
            TempData["AlertClass"] = "alert-info";
            TempData["Info"] = "Resend verification email feature coming soon.";
            return RedirectToAction("VerifyRegister");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(uid))
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var client = _supabaseService.GetClient();

            // Find user by email
            var response = await client
                .From<Users>()
                .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, vm.Email.Trim())
                .Get();

            var user = response.Models.FirstOrDefault();

            // Always show success message to prevent email enumeration
            if (user == null)
            {
                TempData["AlertClass"] = "alert-success";
                TempData["Info"] = "If an account with that email exists, a password reset link has been sent.";
                return RedirectToAction("ForgotPassword");
            }

            // Generate a secure token
            var tokenValue = _accountService.GenerateToken();
            var expireTime = DateTime.UtcNow.AddHours(1);

            // Delete any existing reset tokens for this user
            var existingTokens = await client
                .From<Token>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, user.Uid)
                .Get();

            foreach (var t in existingTokens.Models)
            {
                await client.From<Token>().Delete(t);
            }

            // Create new token
            var resetToken = new Token
            {
                TokenValue = tokenValue,
                Expire = expireTime,
                UId = user.Uid,
                email = user.Email
            };

            await client.From<Token>().Insert(resetToken);

            // Build reset URL
            var resetUrl = Url.Action(
                "ResetPassword",
                "Account",
                new { token = tokenValue },
                "https"
            );

            // Send email
            try
            {
                _accountService.SendForgotPasswordEmail(user, resetUrl ?? string.Empty);
            }
            catch
            {
                // Swallow email errors to prevent information disclosure
            }

            TempData["AlertClass"] = "alert-success";
            TempData["Info"] = "If an account with that email exists, a password reset link has been sent.";
            return RedirectToAction("ForgotPassword");
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["AlertClass"] = "alert-danger";
                TempData["Info"] = "Invalid reset link.";
                return RedirectToAction("Login");
            }

            var client = _supabaseService.GetClient();

            var tokenResponse = await client
                .From<Token>()
                .Filter("tokenValue", Supabase.Postgrest.Constants.Operator.Equals, token)
                .Get();

            var resetToken = tokenResponse.Models.FirstOrDefault();

            if (resetToken == null || resetToken.Expire < DateTime.UtcNow)
            {
                TempData["AlertClass"] = "alert-danger";
                TempData["Info"] = "Invalid or expired reset link.";
                return RedirectToAction("Login");
            }

            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var client = _supabaseService.GetClient();

            // Verify token
            var tokenResponse = await client
                .From<Token>()
                .Filter("tokenValue", Supabase.Postgrest.Constants.Operator.Equals, vm.Token)
                .Get();

            var resetToken = tokenResponse.Models.FirstOrDefault();

            if (resetToken == null || resetToken.Expire < DateTime.UtcNow)
            {
                TempData["AlertClass"] = "alert-danger";
                TempData["Info"] = "Invalid or expired reset link.";
                return RedirectToAction("Login");
            }

            // Find user
            var userResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, resetToken.UId)
                .Get();

            var user = userResponse.Models.FirstOrDefault();

            if (user == null)
            {
                TempData["AlertClass"] = "alert-danger";
                TempData["Info"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Check if new password is same as old password
            var newPasswordHash = _accountService.HashPassword(vm.NewPassword);
            if (user.Password == newPasswordHash)
            {
                ModelState.AddModelError(nameof(vm.NewPassword), "New password cannot be the same as your current password.");
                return View(vm);
            }

            // Update password
            user.Password = newPasswordHash;
            await client.From<Users>().Update(user);

            // Delete the token
            await client.From<Token>().Delete(resetToken);

            // Notify user via email that their password has been changed
            try
            {
                _accountService.SendChangePasswordEmail(user);
            }
            catch
            {
                // Swallow email errors
            }

            TempData["AlertClass"] = "alert-success";
            TempData["Info"] = "Your password has been reset successfully. Please log in.";
            return RedirectToAction("Login");
        }

    }
}
