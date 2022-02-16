using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using static WordWebCMS.Function;

namespace WordWebCMS
{
    public partial class Post : Page
    {
        Posts post = null;
        int pID => post.pID;

        protected void Page_Load(object sender, EventArgs e)
        {
            //Important:把缓存交给设置
            Setting.Application = this.Application;

            ////临时数据id
            //string sessionid = Rnd.Next().ToString("x");

            //通过id获得文章
            if (Request.QueryString["id"] != null && int.TryParse(Request.QueryString["id"], out int pid))
            {
                post = Posts.GetPost(pid);//获得post信息                
            }
            else if (Request.QueryString["shortname"] != null)
            {
                post = Posts.GetPostFormShortName(Request.QueryString["shortname"]);
                if (post == null)
                {//跳转到主页的搜索
                    Server.Transfer("Index.aspx", true);
                }
            }
            else
            {
                Goto404();
                return;
            }

            //Header
            if (Application["MasterHeader"] == null)
                Application["MasterHeader"] = SMaster.GetHeaderHTML();

            LHeader.Text = ((string)Application["MasterHeader"]);

            //添加Toast MD编辑器
            LHeader.Text += "<link rel=\"stylesheet\" href=\"https://uicdn.toast.com/editor/latest/toastui-editor.min.css\" /><script src=\"https://unpkg.com/babel-standalone@6.26.0/babel.min.js\"></script><script src=\"https://uicdn.toast.com/editor/latest/toastui-editor-all.min.js\"></script>";

            //User
            //用户相关
            Users usr = null;
            if (Session["User"] == null)
                LSecondary.Text = SMaster.GetNoLoginHTML();
            else
            {
                usr = ((Users)Session["User"]);
                LSecondary.Text = usr.ToWidget();
            }


            switch (post.State)//判断用户是否有权限查看文章
            {
                case Posts.PostState.Delete:
                    if (usr == null || !(usr.Authority == Setting.AuthLevel.Admin || usr.Authority == Setting.AuthLevel.PostManager))
                        Goto404("postdelete");
                    return;
                case Posts.PostState.Default:
                case Posts.PostState.None:
                case Posts.PostState.Pending:
                    if (usr == null || !(usr.Authority == Setting.AuthLevel.Admin || usr.Authority == Setting.AuthLevel.PostManager || post.AuthorID == usr.uID))
                        Goto404("postpending");
                    return;
            }
            if (!post.PasswordCheck(Request.QueryString["password"]))//查看是否有私人密码,若有,让用户输密码
            {
                if (usr == null || !(usr.Authority == Setting.AuthLevel.Admin || usr.Authority == Setting.AuthLevel.PostManager))
                {
                    Response.Redirect(Setting.WebsiteURL + "/password.aspx?type=post&id=" + pID);
                    Response.End();
                    return;
                }
            }
            //网站标题
            LHeader.Text = LHeader.Text.Replace("<!--WWC:head-->", $"<title>{post.Name} - {Setting.WebTitle}</title>");

            if (Application["posttopost" + pID.ToString()] == null)
                Application["posttopost" + pID.ToString()] = post.ToPost();
            LContentPage.Text = (string)Application["posttopost" + pID.ToString()];
            post.Readers += 1;

            LCatLink.Text = $"<span class=\"nav-previous\">张贴在<a href=\"{Setting.WebsiteURL}/Index.aspx?class={post.Classify}\" rel=\"category tag\">{post.Classify}</a></span>";
            Lpostlike.Text = $"{post.Likes}个赞<button type =\"button\" onclick=\"LikePost({pID})\" class=\"like-post\" style=\"" +
                $"{(usr == null || (Application[$"Likep{pID}u{usr.uID}"] == null) ? "background:url(Picture/like.png);" : "background:url(Picture/likeup.png);")} background-size:cover;\" />";


            if (Application["postreview" + pID.ToString()] == null)
            {
                List<Review> reviews = Review.GetReviewByPostID(pID);
                if (reviews.Count != 0)
                {
                    LComments.Text = $"<h2 class=\"comments-title\">共有{reviews.Count}个评论</h2><a href=\"#reply-title\" class=\"nav-next\"><ol class=\"comment-list\">";
                    foreach (Review rv in reviews)
                        LComments.Text += rv.ToPostReview(usr);
                    Application["postreview" + pID.ToString()] = LComments.Text;
                }
            }
            else
            {
                LComments.Text = (string)Application["postreview" + pID.ToString()];
            }

            if (Application["postContentToIndex" + pID.ToString()] == null)
                Application["postContentToIndex" + pID.ToString()] = post.ContentToIndex();
            LSecondary.Text += (string)Application["postContentToIndex" + pID.ToString()];
            //添加评论
            if (post.AllowComments)
            {
                if (usr == null)
                {
                    LContentPage.Text += $"<h3 id=\"reply-title\" class=\"comment-reply-title\">您还没有登陆 无法发表评论 <a href=\"{Setting.WebsiteURL}/login.aspx\"><em>->前往登陆</em></a></h3>";
                }
                else
                {
                    commentspanel.Visible = true;
                    //captcha_question.Text = RndQuestion(out int anser); 打不过就加入 不整回答数学题评论了
                    //判断能不能发邮件,如果能就支持邮件回复
                    //if (Setting.EnabledEmail)
                    //{
                    //    comment_mail_notify.Visible = true;
                    //}//邮件相关判断更改至个人设置 是否接受通知
                    commentssubmit.InnerHtml = $"<button class=\"submit\" onclick=\"SendReview({pID})\">发表评论</button>";
                }
            }
            else
            {
                LContentPage.Text += "<h3 id=\"none-reply-title\" class=\"comment-reply-title\">这篇文章已关闭评论</h3>";
            }
            //ajaxscript.InnerText = ajaxscript.InnerText.Replace("{pid}", pID.ToString());

            //Footer
            if (Application["MasterFooter"] == null)
                Application["MasterFooter"] = SMaster.GetFooterHTML();
            LFooter.Text = ((string)Application["MasterFooter"]);
        }
        public void Goto404(string type = "post")
        {
            Response.Redirect(Setting.WebsiteURL + "/404.aspx?type=" + type);
            Response.End();
            return;
        }
    }
}