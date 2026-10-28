using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MySqlConnector;

namespace WordWebCMS.Services;

public class CmsExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var duplicate = context.Exception.GetBaseException() is MySqlException { Number: 1062 };
        if (context.Exception is not (InvalidDataException or UnauthorizedAccessException or System.Data.DBConcurrencyException or System.Text.Json.JsonException) && !duplicate) return;
        if (context.Exception is System.Data.DBConcurrencyException)
            context.HttpContext.Response.Headers["X-Cms-Draft-Conflict"] = "true";
        var code = context.Exception is UnauthorizedAccessException ? 403 : 409;
        var message = duplicate ? UiText.T("名称、邮箱或短链接已被使用，请返回修改后重试。") : context.Exception is System.Text.Json.JsonException ? UiText.T("译文数据格式无效。") : context.Exception.Message;
        context.Result = new ContentResult { Content = message, ContentType = "text/plain; charset=utf-8", StatusCode = code };
        context.ExceptionHandled = true;
    }
}
