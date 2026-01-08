// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace BookShoppingCartMvcUI.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<ConfirmEmailModel> _logger;

        public ConfirmEmailModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<ConfirmEmailModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Confirm email failed: Missing userId or code.");
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Confirm email failed: User {UserId} not found.", userId);
                return NotFound($"Không tìm thấy người dùng với ID '{userId}'.");
            }

            // ✅ Giải mã token
            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch
            {
                _logger.LogWarning("Invalid token format for user {UserId}", userId);
                StatusMessage = "❌ Mã xác thực không hợp lệ.";
                return Page();
            }

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (result.Succeeded)
            {
                _logger.LogInformation("Email confirmed successfully for user {UserId}", userId);

                // ✅ Đăng nhập ngay sau khi xác nhận
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Chuyển hướng về trang chủ
                return RedirectToPage("/Index");
            }
            else
            {
                StatusMessage = "❌ Xác nhận email thất bại. Token có thể đã được sử dụng hoặc không hợp lệ.";
                _logger.LogWarning("Email confirmation failed for user {UserId}. Errors: {Errors}",
                    userId, string.Join(", ", result.Errors));
            }

            return Page();
        }
    }
}
