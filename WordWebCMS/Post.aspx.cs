using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using static WordWebCMS.Function;

namespace WordWebCMS
{
    public partial class Post : System.Web.UI.Page
    {
        int pID = -1;
        protected void Page_Load(object sender, EventArgs e)
        {
            //Important:把缓存交给设置
            Setting.Application = this.Application;

            ////临时数据id
            //string sessionid = Rnd.Next().ToString("x");

            //Header
            if (Application["MasterHeader"] == null)
                Application["MasterHeader"] = SMaster.GetHeaderHTML();

            LHeader.Text = ((string)Application["MasterHeader"]);

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

            if (Request.QueryString["ID"] != null)
            {
                int.TryParse(Request.QueryString["ID"], out pID);
            }
            if (pID == -1)
            {
                Goto404();
                return;
            }
            Posts post = Posts.GetPost(pID);//获得post信息
            if (post == null)
            {
                Goto404();
                return;
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
                    Response.Redirect(Setting.WebsiteURL + "/password.aspx?type=post&ID=" + pID);
                    Response.End();
                    return;
                }
            }
            if (Application["posttopost" + pID.ToString()] == null)
            {
                Application["posttopost" + pID.ToString()] = post.ToPost();
                LContentPage.Text = (string)Application["posttopost" + pID.ToString()];
            }
            else
            {
                LContentPage.Text = (string)Application["posttopost" + pID.ToString()];
            }

            LCatLink.Text = $"<span class=\"nav-previous\">张贴在<a href=\"{Setting.WebsiteURL}/Index.aspx?class={post.Classify}\" rel=\"category tag\">{post.Classify}</a></span>";
            Lpostlike.Text = $"{post.Likes}个赞<button ID=\"Like\" type=\"button\" onclick=\"LikePost({pID})\" style=\"border-style: none; width: 30px; height: 30px;" +
                $"{(Application[$"Likep{pID}u{usr.uID}"] == null ? "background:url(Picture/like.png);" : "background:url(Picture/likeup.png);")}background-size:cover;\" />";


            if (Application["postreview" + pID.ToString()] == null)
            {
                List<Review> reviews = Review.GetReviewByPostID(pID);
                if (reviews.Count != 0)
                {
                    LComments.Text = $"<h2 class=\"comments-title\">有{reviews.Count}个评论</h2><ol class=\"comment-list\">";
                    foreach (Review rv in reviews)
                        LComments.Text += rv.ToPostReview();
                    Application["postreview" + pID.ToString()] = LComments.Text;
                    Application["postContentToIndex" + pID.ToString()] = post.ContentToIndex();
                    LSecondary.Text += (string)Application["postContentToIndex" + pID.ToString()];
                }
            }
            else
            {
                LComments.Text = (string)Application["postreview" + pID.ToString()];
                LSecondary.Text += (string)Application["postContentToIndex" + pID.ToString()];
            }

            //添加评论
            if (post.AllowComments)
            {
                if (usr == null)
                {
                    LContentPage.Text += $"<h3 id=\"none-reply-title\" class=\"comment-reply-title\">您还没有登陆 无法发表评论 <a href=\"{Setting.WebsiteURL}/login.asp\"><em>->前往登陆</em></a></h3>";
                }
                else
                {
                    commentspanel.Visible = true;
                    //captcha_question.Text = RndQuestion(out int anser); 打不过就加入 不整回答数学题评论了
                }
            }
            else
            {
                LContentPage.Text += "<h3 id=\"none-reply-title\" class=\"comment-reply-title\">这篇文章已关闭评论</h3>";
            }




            //ajaxscript.InnerText = ajaxscript.InnerText.Replace("{pid}", pID.ToString());

            //Footer
            if (Application["MasterFooter"] == null)
                Application["MasterFooter"] = (string)Application["MasterFooter"];
            LFooter.Text = ((string)Application["MasterFooter"]);
        }
        public void Goto404(string type = "post")
        {
            Response.Redirect(Setting.WebsiteURL + "/404.aspx?type=" + type);
            Response.End();
            return;
        }


        private void MsgBox(string msg) => Page.ClientScript.RegisterStartupScript(this.GetType(), "", $"<script>alert('{msg}');</script>");

        protected void submit_Click(object sender, EventArgs e)
        {
            Users usr;
            if (Session["User"] == null)
            {
                MsgBox("登陆已失效,请重新登陆"); return;
            }
            else
            {
                usr = ((Users)Session["User"]);
            }
            //if (!int.TryParse(captcha_anser.Text, out int res))
            //{
            //    MsgBox("验证码答案为纯数字,请检查输入"); return;
            //}
            //if (res != anser)
            //{
            //    MsgBox("您输入了错误的验证码答案。请重试"); return;
            //}
            Application["postreview" + pID.ToString()] = null;//更新post的评论


            var rev = Review.CreatReview(pID, comment.Text, usr.uID, DateTime.Now, DateTime.Now,
                (usr.Authority == Setting.AuthLevel.Admin || usr.Authority == Setting.AuthLevel.Auditor ? Review.ReviewState.Published : Review.ReviewState.Default));
          
            if (rev.State == Review.ReviewState.Default)
                MsgBox("提交评论成功," + (Setting.ReviewDefault == Review.ReviewState.Pending ? "等待审核中" : "已发布"));
            else if (rev.State == Review.ReviewState.Published)
                MsgBox("提交评论成功,已发布");
            else
                MsgBox("提交评论成功!");

            Response.Redirect(HttpContext.Current.Request.Url.ToString() + "#comment-" + rev.rID.ToString());
        }
    }
}