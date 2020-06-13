using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.SessionState;

namespace WordWebCMS
{
    /// <summary>
    /// 作为动态的ajax获取
    /// </summary>
    public class AJAX : IHttpHandler, IRequiresSessionState
    {

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/plain";
            switch (context.Request.QueryString["action"])
            {
                case "postlike":
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
            }
            context.Response.Write("错误的AJAX调用");
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}