using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement;
using OrchardCore.Email;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using OrchardCore.Users.ViewModels;

namespace OrchardCore.Users.Controllers
{
    [Feature("OrchardCore.Users.ResetPassword")]
    public class ResetPasswordController : BaseEmailController
    {
        private readonly IUserService _userService;
        private readonly UserManager<IUser> _userManager;
        private readonly ISiteService _siteService;
        private readonly IEnumerable<IPasswordRecoveryFormEvents> _passwordRecoveryFormEvents;
        private readonly ILogger<ResetPasswordController> _logger;

        public ResetPasswordController(
            IUserService userService,
            UserManager<IUser> userManager,
            ISiteService siteService,
            ISmtpService smtpService,
            IDisplayHelper displayHelper,
            IStringLocalizer<ResetPasswordController> stringLocalizer,
            ILogger<ResetPasswordController> logger,
            IEnumerable<IPasswordRecoveryFormEvents> passwordRecoveryFormEvents,
            HtmlEncoder htmlEncoder) : base(smtpService, displayHelper, htmlEncoder)
        {
            _userService = userService;
            _userManager = userManager;
            _siteService = siteService;

            T = stringLocalizer;
            _logger = logger;
            _passwordRecoveryFormEvents = passwordRecoveryFormEvents;
        }

        IStringLocalizer T { get; set; }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword()
        {
            if (!(await _siteService.GetSiteSettingsAsync()).As<ResetPasswordSettings>().AllowResetPassword)
            {
                return NotFound();
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!(await _siteService.GetSiteSettingsAsync()).As<ResetPasswordSettings>().AllowResetPassword)
            {
                return NotFound();
            }

            await _passwordRecoveryFormEvents.InvokeAsync(i => i.RecoveringPasswordAsync((key, message) => ModelState.AddModelError(key, message)), _logger);

            if (ModelState.IsValid)
            {
                var user = await _userService.GetForgotPasswordUserAsync(model.UserIdentifier) as User;
                if (user == null || (
                        (await _siteService.GetSiteSettingsAsync()).As<RegistrationSettings>().UsersMustValidateEmail
                        && !await _userManager.IsEmailConfirmedAsync(user))
                    )
                {
                    // returns to confirmation page anyway: we don't want to let scrapers know if a username or an email exist
                    return RedirectToLocal(Url.Action("ForgotPasswordConfirmation", "ResetPassword"));
                }

                user.ResetToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.ResetToken));
                var resetPasswordUrl = Url.Action("ResetPassword", "ResetPassword", new { code = user.ResetToken }, HttpContext.Request.Scheme);
                // send email with callback link
                await SendEmailAsync(user.Email, T["Reset your password"], new LostPasswordViewModel() { User = user, LostPasswordUrl = resetPasswordUrl });

                await _passwordRecoveryFormEvents.InvokeAsync(i => i.PasswordRecoveredAsync(), _logger);

                return RedirectToLocal(Url.Action("ForgotPasswordConfirmation", "ResetPassword"));
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string code = null)
        {
            if (!(await _siteService.GetSiteSettingsAsync()).As<ResetPasswordSettings>().AllowResetPassword)
            {
                return NotFound();
            }
            if (code == null)
            {
                //"A code must be supplied for password reset.";
            }
            return View(new ResetPasswordViewModel { ResetToken = code });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!(await _siteService.GetSiteSettingsAsync()).As<ResetPasswordSettings>().AllowResetPassword)
            {
                return NotFound();
            }

            await _passwordRecoveryFormEvents.InvokeAsync(i => i.ResettingPasswordAsync((key, message) => ModelState.AddModelError(key, message)), _logger);

            if (ModelState.IsValid)
            {
                if (await _userService.ResetPasswordAsync(model.Email, Encoding.UTF8.GetString(Convert.FromBase64String(model.ResetToken)), model.NewPassword, (key, message) => ModelState.AddModelError(key, message)))
                {
                    await _passwordRecoveryFormEvents.InvokeAsync(i => i.PasswordResetAsync(), _logger);

                    return RedirectToLocal(Url.Action("ResetPasswordConfirmation", "ResetPassword"));
                }
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }
    }
}