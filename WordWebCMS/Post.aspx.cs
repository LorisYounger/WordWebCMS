using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WordWebCMS
{
    public partial class Post : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            //Important:把缓存交给设置
            Setting.Application = this.Application;

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

            int pID = -1;
            if (Request.QueryString["ID"] != null)
            {
                int.TryParse(Request.QueryString["ID"], out pID);
            }
            if (pID == -1)
            {
                Goto404();
                return;
            }
            Posts post = Posts.GetPost(pID);
            if (post == null)
            {
                Goto404();
                return;
            }

            switch (post.State)
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
            if (!post.PasswordCheck(Request.QueryString["password"]))
            {
                if (usr == null || !(usr.Authority == Setting.AuthLevel.Admin || usr.Authority == Setting.AuthLevel.PostManager))
                {
                    Response.Redirect(Setting.WebsiteURL + "/password.aspx?type=post&ID=" + pID);
                    Response.End();
                    return;
                }
            }

            LContentPage.Text = post.ToPost();

            //添加评论
            if (post.AllowComments)
            {
                
            }


            LSecondary.Text += post.ContentToIndex();
            //Footer
            if (Application["MasterFooter"] != null)
                LFooter.Text = (string)Application["MasterFooter"];
            else
                LFooter.Text = SMaster.GetFooterHTML();
        }
        public void Goto404(string type = "post")
        {
            Response.Redirect(Setting.WebsiteURL + "/404.aspx?type=" + type);
            Response.End();
            return;
        }
    }
}