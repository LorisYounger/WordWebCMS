using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;

namespace WordWebCMS
{
    /// <summary>
    /// 作为动态的ajax获取
    /// </summary>
    public class AJAX : Page, IHttpHandler, IRequiresSessionState
    {

        public override void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/plain";
            switch (context.Request.QueryString["action"])
            {
                case "postlike"://点赞
                    int id;
                    if (!int.TryParse(context.Request.QueryString["ID"], out id))
                        break;
                    Users usr;
                    if (context.Session["User"] == null)
                    {
                        context.Response.Write($"请登录后操作<a href=\"{Setting.WebsiteURL}/login.aspx\">");
                        return;
                    }
                    else
                    {
                        usr = ((Users)context.Session["User"]);
                    }

                    Posts post = Posts.GetPost(id);

                    if (post == null)
                        break;

                    if (context.Application[$"Likep{id}u{usr.uID}"] == null)
                    {
                        context.Application[$"Likep{id}u{usr.uID}"] = true;
                        post.Likes += 1;
                        context.Response.Write($"{post.Likes}个赞<button ID=\"Like\" type=\"button\" onclick=\"LikePost({id})\" style=\"border-style: none; width: 30px; height: 30px;" +
                            $"background:url(Picture/likeup.png);background-size:cover;\" />");

                    }
                    else
                    {
                        post.Likes -= 1;
                        context.Application[$"Likep{id}u{usr.uID}"] = null;
                        context.Response.Write($"{post.Likes}个赞<button ID=\"Like\" type=\"button\" onclick=\"LikePost({id})\" style=\"border-style: none; width: 30px; height: 30px;" +
                            $"background:url(Picture/like.png);background-size:cover;\" />");
                    }
                    return;
                case "regemail"://注册时发验证EMAIL
                    string MasterKey = context.Request.QueryString["ID"];
                    //if (Session[MasterKey + "ans"] == null)
                    //{
                    //    context.Response.Write("缓存失效");
                    //    return;
                    //}
                    context.Response.Write("发送成功:" + MasterKey + context.Request.QueryString["email"]);
                    return;
            }
            context.Response.Write("错误的AJAX调用");
        }
    }
}