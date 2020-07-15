using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WordWebCMS
{
    public partial class Index : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            //Important:把缓存交给设置
            Setting.Application = this.Application;

            if (Setting.NomalIndex != -1 && Request.QueryString["page"] == null)
            {//主页是post 跳转到post
                Response.Redirect(Setting.WebsiteURL + "/Post.aspx?ID=" + Setting.NomalIndex.ToString());
                Response.End();
                return;
            }

            //在很远的将来TODO: ALLinONE
            //可以通过域名判断网站类型,有专有的Style和内容,在主站则显示全部内容


            //Header
            if (Application["MasterHeader"] == null)
                Application["MasterHeader"] = SMaster.GetHeaderHTML();
            LHeader.Text = ((string)Application["MasterHeader"]);

            //NomalIndex
            List<string> MasterIndex;

            if (Request.QueryString["class"] != null)
            {
                MasterIndex = new List<string>();
                foreach (Posts post in Posts.GetPostFormClassify(Request.QueryString["class"]))
                    MasterIndex.Add(post.ToIndex());
                Application["MasterIndex" + Request.QueryString["class"]] = MasterIndex;
            }
            else
            {
                if (Application["MasterNomalIndex"] != null)
                    MasterIndex = (List<string>)Application["MasterNomalIndex"];
                else
                {
                    MasterIndex = new List<string>();
                    foreach (Posts post in Posts.GetAllAvailablePost())
                        MasterIndex.Add(post.ToIndex());
                    Application["MasterNomalIndex"] = MasterIndex;
                }
            }

            int page = 0;
            if (Request.QueryString["page"] != null)
            {
                int.TryParse(Request.QueryString["page"], out page);
            }
            LHeader.Text = LHeader.Text.Replace("<!--WWC:head-->", $"<title>{Setting.WebTitle} - {(page == 0 ? Setting.WebSubTitle : $"第{page + 1}页")}</title>");

            if ((page + 1) * 10 < MasterIndex.Count)
                LNavLinks.Text = $"<div class=\"nav-previous\"><a href=\"?page={page + 1}\"><span class=\"meta-nav\">←</span> 早期文章</a></div>";
            if (page != 0)
                LNavLinks.Text += $"<div class=\"nav-next\"><a href=\"?page={page - 1}\"><span class=\"meta-nav\">→</span> 较新文章</a></div>";

            for (int i = page * 10; i < MasterIndex.Count && i < (page + 1) * 10; i++)
                LContentPage.Text += MasterIndex[i];
#if DEBUG //DEBUG:默认给第一个用户权限免得登陆
            Session["User"] = Users.GetUser(1);
#endif
            //用户相关
            if (Session["User"] == null)
                LSecondary.Text = SMaster.GetNoLoginHTML();
            else
                LSecondary.Text = ((Users)Session["User"]).ToWidget();

            //Footer
            if (Application["MasterFooter"] == null)
                Application["MasterFooter"] = SMaster.GetFooterHTML();
            LFooter.Text = ((string)Application["MasterFooter"]);
        }
    }
}