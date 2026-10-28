using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using WordWebCMS.Data;
using WordWebCMS.Models;

namespace WordWebCMS.Services;

public class AccountService(CmsDb db, IPasswordHasher<CmsUser> hasher)
{
    public static long UserId(ClaimsPrincipal user) => long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public static bool CanEditAll(ClaimsPrincipal user) => user.IsInRole("Editor") || user.IsInRole("Administrator");

    public async Task<CmsUser?> CheckPasswordAsync(string login, string password)
    {
        login = login.Trim().ToLowerInvariant();
        var account = await db.Accounts.Select<CmsUser>().Where(u => u.UserName == login || u.Email == login).FirstAsync();
        if (account == null || account.Disabled ||
            hasher.VerifyHashedPassword(account, account.PasswordHash, password) == PasswordVerificationResult.Failed) return null;
        return account;
    }

    public async Task<CmsUser> CreateAsync(string name, string email, string password, bool confirmed = false)
    {
        name = name.Trim().ToLowerInvariant();
        email = email.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9_-]{3,64}$") ||
            email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email ||
            password.Length < 12 || password.Length > 128)
            throw new InvalidDataException(UiText.T("用户名需为 3–64 位字母、数字、下划线或连字符；请填写有效邮箱及 12–128 位密码。"));
        var account = new CmsUser { UserName = name, Email = email, EmailConfirmed = confirmed };
        account.PasswordHash = hasher.HashPassword(account, password);
        account.Id = await db.Accounts.Insert(account).ExecuteIdentityAsync();
        return account;
    }

    public async Task SignInAsync(HttpContext context, CmsUser account, bool remember)
    {
        if (!account.EmailConfirmed || account.Disabled)
            throw new UnauthorizedAccessException(UiText.T("账号未验证或已在本站停用。"));
        var membership = await db.Site.Select<SiteUserRole>().Where(r => r.UserId == account.Id).FirstAsync();
        if (membership == null)
        {
            var settings = await db.Site.Select<SiteSettings>().FirstAsync();
            if (settings.Registration == RegistrationMode.Closed) throw new UnauthorizedAccessException(UiText.T("本站已关闭注册，请联系管理员加入本站。"));
            membership = new SiteUserRole { UserId = account.Id, Role = settings.Registration == RegistrationMode.Approval ? SiteRole.Pending : SiteRole.Reader };
            await db.Site.InsertOrUpdate<SiteUserRole>().SetSource(membership).IfExistsDoNothing().ExecuteAffrowsAsync();
            membership = await db.Site.Select<SiteUserRole>().Where(r => r.UserId == account.Id).FirstAsync();
        }
        var role = membership?.Role ?? SiteRole.Reader;
        if (role == SiteRole.Pending) throw new UnauthorizedAccessException(UiText.T("注册申请正在等待本站管理员审核。"));
        if (role is SiteRole.Banned or SiteRole.Removed)
            throw new UnauthorizedAccessException(UiText.T("账号未验证或已在本站停用。"));
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Name, account.UserName),
            new Claim("stamp", account.SecurityStamp), new Claim(ClaimTypes.Role, role.ToString())
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = remember });
    }

    public async Task ValidateCookieAsync(CookieValidatePrincipalContext context)
    {
        var id = UserId(context.Principal!);
        var account = await db.Accounts.Select<CmsUser>().Where(u => u.Id == id).FirstAsync();
        var membership = await db.Site.Select<SiteUserRole>().Where(r => r.UserId == id).FirstAsync();
        var role = membership?.Role ?? SiteRole.Reader;
        var closedToNewMembers = membership == null && (await db.Site.Select<SiteSettings>().FirstAsync()).Registration != RegistrationMode.Open;
        if (account == null || account.Disabled || !account.EmailConfirmed || role is SiteRole.Banned or SiteRole.Pending or SiteRole.Removed || closedToNewMembers ||
            context.Principal!.FindFirstValue("stamp") != account.SecurityStamp)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
            return;
        }
        context.HttpContext.Items["UserAvatar"] = ShortLinks.Avatar(account.UserName, account.AvatarUrl);
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        if (identity.FindFirst(ClaimTypes.Role)?.Value == role.ToString()) return;
        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
    }

    public async Task<string> CreateTokenAsync(long userId, string purpose)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await db.Accounts.Delete<AccountToken>().Where(t => t.ExpiresUtc < DateTime.UtcNow).ExecuteAffrowsAsync();
        await db.Accounts.Insert(new AccountToken {
            Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), UserId = userId,
            Purpose = purpose, ExpiresUtc = DateTime.UtcNow.AddHours(purpose == "verify" ? 24 : 1)
        }).ExecuteAffrowsAsync();
        return token;
    }

    public void ConsumeToken(string token, string purpose, string? password)
    {
        if (purpose == "reset" && (password == null || password.Length < 12 || password.Length > 128))
            throw new InvalidDataException(UiText.T("密码长度需为 12–128 位。"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        db.Accounts.Transaction(() =>
        {
            var credential = db.Accounts.Select<AccountToken>().Where(t => t.Hash == hash && t.Purpose == purpose && t.ExpiresUtc > DateTime.UtcNow).First();
            if (credential == null) throw new InvalidDataException(UiText.T("链接已过期或已使用，请重新申请。"));
            var account = db.Accounts.Select<CmsUser>().Where(u => u.Id == credential.UserId).ForUpdate().First();
            if (account == null || account.Disabled) throw new InvalidDataException(UiText.T("账号不可用。"));
            if (!db.Accounts.Select<AccountToken>().Where(t => t.Hash == hash && t.Purpose == purpose && t.ExpiresUtc > DateTime.UtcNow).ForUpdate().Any())
                throw new InvalidDataException(UiText.T("链接已过期或已使用，请重新申请。"));
            if (purpose == "verify") account.EmailConfirmed = true;
            else
            {
                account.PasswordHash = hasher.HashPassword(account, password!);
                account.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            db.Accounts.Update<CmsUser>().SetSource(account).ExecuteAffrows();
            db.Accounts.Delete<AccountToken>().Where(t => t.UserId == account.Id && t.Purpose == purpose).ExecuteAffrows();
        });
    }
}
