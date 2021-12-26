using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using static WordWebCMS.Function;
using System.IO;

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
                        context.Response.Write($"{post.Likes}个赞<button type=\"button\" onclick=\"LikePost({id})\" class=\"like-post\" style=\"" +
                            $"background:url(Picture/likeup.png);background-size:cover;\" />");

                    }
                    else
                    {
                        post.Likes -= 1;
                        context.Application[$"Likep{id}u{usr.uID}"] = null;
                        context.Response.Write($"{post.Likes}个赞<button type=\"button\" onclick=\"LikePost({id})\" class=\"like-post\" style=\"" +
                            $"background:url(Picture/like.png);background-size:cover;\" />");
                    }
                    return;
                case "reviewlike":
                    if (!int.TryParse(context.Request.QueryString["ID"], out id))
                        break;
                    if (context.Session["User"] == null)
                    {
                        context.Response.Write($"请登录后操作<a href=\"{Setting.WebsiteURL}/login.aspx\">");
                        return;
                    }
                    else
                    {
                        usr = (Users)context.Session["User"];
                    }

                    Review review = Review.GetReviewByID(id);
                    if (review == null)
                        break;

                    if (context.Application[$"Liker{id}u{usr.uID}"] == null)
                    {
                        context.Application[$"Liker{id}u{usr.uID}"] = true;
                        review.Likes += 1;
                        context.Response.Write($"{review.Likes}个赞<button type=\"button\" onclick=\"LikeReview({id})\" class=\"like-review\" style=\"" +
                            $"background:url(Picture/likeup.png);background-size:cover;\" />");

                    }
                    else
                    {
                        review.Likes -= 1;
                        context.Application[$"Liker{id}u{usr.uID}"] = null;
                        context.Response.Write($"{review.Likes}个赞<button type=\"button\" onclick=\"LikeReview({id})\" class=\"like-review\" style=\"" +
                            $"background:url(Picture/like.png);background-size:cover;\" />");
                    }
                    //清除缓存
                    Setting.Application["postreview" + review.pID.ToString()] = null;
                    return;
                case "regemail"://注册时发验证EMAIL
                    string MasterKey = context.Request.QueryString["ID"];
                    string anser = context.Request.QueryString["key"];
                    string email = context.Request.QueryString["email"];
                    if (Session[MasterKey + "ans"] == null)
                    //连接丢失,请重试
                    {
                        context.Response.Write("验证码缓存已丢失,请刷新重试");
                    }
                    else if (string.IsNullOrEmpty(anser))
                    {
                        context.Response.Write("验证码不能为空");
                    }
                    else if (!int.TryParse(anser, out int ans))
                    {
                        context.Response.Write("验证码为纯数字,请检查输入");
                    }
                    else if (string.IsNullOrEmpty(email))
                    {
                        context.Response.Write("请输入邮件账号");
                    }
                    else if ((int)Session[MasterKey + "ans"] == ans)
                    {
                        if (!(Session["MailTimeing"] == null))
                        {
                            int waitsec = (int)DateTime.Now.Subtract((DateTime)Session["MailTimeing"]).TotalSeconds;
                            if (waitsec < 60)
                            {
                                context.Response.Write($"请等待{60 - waitsec}秒后重试");
                                return;
                            }
                        }
                        Session[MasterKey + "mail"] = Rnd.Next(100000, 999999);
                        string msg = SendEmail(Setting.WebTitle + " 邮箱验证码", $"感谢您使用 {Setting.WebTitle}\n您的激活码为: {Session[MasterKey + "mail"]}\n该激活码仅用于注册,请不要将该验证码泄露给他人. \n如果此活动不是您本人操作,请无视该邮件\n如需帮助,请联系 {Setting.ContactLink}", email);
                        if (string.IsNullOrEmpty(msg))
                            context.Response.Write("邮件发送成功");
                        else
                            context.Response.Write("邮件发送失败:" + msg);
                        Session["MailTimeing"] = DateTime.Now;
                    }
                    else
                    {
                        context.Response.Write("验证码错误,请重新计算");
                    }
                    return;
                case "sendreview":
                    if (!int.TryParse(context.Request.QueryString["ID"], out int pID))
                        break;

                    StreamReader sr = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                    string comment = sr.ReadToEnd();
                    sr.Close();
                    if (string.IsNullOrWhiteSpace(comment))
                    {
                        context.Response.Write("评论内容为空"); return;
                    }
                    if (Session["User"] == null)
                    {
                        context.Response.Write("登陆已失效,请重新登陆"); return;
                    }
                    else
                    {
                        usr = ((Users)Session["User"]);
                    }
                    context.Application["postreview" + pID.ToString()] = null;//更新post的评论
                    comment = HttpUtility.UrlDecode(comment);
                    var lstr = comment.Split(new char[] { '\n' }, 2);
                    Review rev;
                    if (lstr[0].StartsWith("wwcms:\\|"))
                    {
                        lstr[0] = lstr[0].Replace(@"\|", "|").Replace(@"\#", "#");
                        if (lstr.Length == 2)
                            lstr[0] += '\n' + lstr[1];
                        rev = Review.CreatReview(pID, lstr[0], usr.uID, DateTime.Now, DateTime.Now,
                        (usr.Authority == Setting.AuthLevel.Admin || usr.Authority == Setting.AuthLevel.Auditor ? Review.ReviewState.Published : Review.ReviewState.Default));
                    }
                    else
                    {
                        rev = Review.CreatReview(pID, comment, usr.uID, DateTime.Now, DateTime.Now,
                        (usr.Authority == Setting.AuthLevel.Admin || usr.Authority == Setting.AuthLevel.Auditor ? Review.ReviewState.Published : Review.ReviewState.Default));
                    }

                    if (rev.State == Review.ReviewState.Default)
                        context.Response.Write("评论提交成功," + (Setting.ReviewDefault == Review.ReviewState.Pending ? "等待审核中" : "已发布"));
                    else if (rev.State == Review.ReviewState.Published)
                        context.Response.Write("评论提交成功,已发布");
                    else
                        context.Response.Write("评论提交成功!");
                    return;
            }
            context.Response.Write("错误的AJAX调用");
        }
    }
}