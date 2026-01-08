// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication;
using System.Text.Encodings.Web;

namespace BookShoppingCartMvcUI.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }
        public string ProviderDisplayName { get; set; }
        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            _logger.LogInformation("External login POST triggered. Provider={Provider}, ReturnUrl={ReturnUrl}", provider, returnUrl);

            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

            // ✅ Luôn hiện popup chọn tài khoản Google
            properties.Items["prompt"] = "select_account";

            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                _logger.LogWarning("Remote error from provider: {Error}", remoteError);
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogWarning("GetExternalLoginInfoAsync returned NULL during callback.");
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            _logger.LogInformation("External login info received: Provider={Provider}, Key={Key}, Email={Email}",
                info.LoginProvider, info.ProviderKey, info.Principal.FindFirstValue(ClaimTypes.Email));

            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                _logger.LogInformation("User successfully signed in via {Provider}", info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User is locked out during external login.");
                return RedirectToPage("./Lockout");
            }

            // ✅ Nếu user tồn tại
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                if (!existingUser.EmailConfirmed)
                {
                    _logger.LogWarning("User exists but email not confirmed: {Email}", email);

                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(existingUser);
                    token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

                    var confirmationLink = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { userId = existingUser.Id, code = token },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(existingUser.Email,
                        "Xác nhận tài khoản BookStore",
                        $"<p>Chào bạn,</p><p>Vui lòng xác nhận email của bạn bằng cách nhấn vào link sau:</p>" +
                        $"<p><a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>Xác nhận tài khoản</a></p>");

                    ErrorMessage = "Tài khoản đã tồn tại nhưng chưa xác thực email. Hệ thống đã gửi lại liên kết xác nhận.";
                    return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                }

                var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                if (addLoginResult.Succeeded)
                {
                    await _signInManager.SignInAsync(existingUser, isPersistent: false);
                    _logger.LogInformation("Linked Google to existing account and signed in.");
                    return LocalRedirect(returnUrl);
                }

                _logger.LogWarning("Failed to add external login: {Errors}", string.Join(", ", addLoginResult.Errors));
            }

            // 🟢 User chưa tồn tại → hiển thị form đăng ký
            _logger.LogInformation("User does not exist. Showing registration confirmation page.");

            // 👉 Lưu dữ liệu provider để dùng sau
            TempData["Provider"] = info.LoginProvider;
            TempData["ProviderKey"] = info.ProviderKey;

            ReturnUrl = returnUrl;
            ProviderDisplayName = info.ProviderDisplayName;
            if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                Input = new InputModel
                {
                    Email = info.Principal.FindFirstValue(ClaimTypes.Email)
                };
            }
            return Page();
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            // 👉 Lấy dữ liệu provider từ TempData
            var provider = TempData["Provider"]?.ToString();
            var providerKey = TempData["ProviderKey"]?.ToString();

            if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(providerKey))
            {
                _logger.LogWarning("Missing provider info from TempData during confirmation.");
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();
                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    var loginInfo = new UserLoginInfo(provider, providerKey, provider);
                    await _userManager.AddLoginAsync(user, loginInfo);

                    // Gửi email xác thực
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code = code },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email,
                        "Xác nhận tài khoản",
                        $"Vui lòng xác nhận tài khoản bằng cách <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>nhấp vào đây</a>.");

                    _logger.LogInformation("User created and external login added successfully.");
                    return RedirectToPage("./RegisterConfirmation", new { Email = Input.Email });
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            ProviderDisplayName = provider;
            ReturnUrl = returnUrl;
            return Page();
        }

        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure '{nameof(IdentityUser)}' has a parameterless constructor.");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
                throw new NotSupportedException("User store does not support email.");
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}
