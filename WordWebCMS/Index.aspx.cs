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
            try
            {
                if (Setting.NomalIndex != -1 && (Request.QueryString["page"] == null || Request.QueryString["class"] == null))
                {//主页是post 跳转到post
                    Server.Transfer(Setting.WebsiteURL + "/Post.aspx?ID=" + Setting.NomalIndex.ToString());
                    Response.End();
                    return;
                }
            }
            catch (Exception ex)
            {//如果读取数据库出现了一些问题
                if (ex.Message.StartsWith("WWCMS"))
                {
                    switch (ex.Message)
                    {
                        case "WWCMS:无法连接数据库":
                            Response.Redirect(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority) + "/Setup.aspx?step=1");
                            Response.End();
                            break;
                    }
                }
                throw (ex);
            }

            //在很远的将来TODO: ALLinONE
            //可以通过域名判断网站类型,有专有的Style和内容,在主站则显示全部内容

            //Header
            if (Application["MasterHeader"] == null)
                Application["MasterHeader"] = SMaster.GetHeaderHTML();
            LHeader.Text = ((string)Application["MasterHeader"]);

            //NomalIndex
            List<string> MasterIndex;
            string WebTitle = Setting.WebTitle;
            string WebSubTitle = Setting.WebSubTitle;

            if (Request.QueryString["class"] != null || Request.QueryString["shortname"] != null)
            {
                //shortname是从Post借来的
                string cfy;
                if (Request.QueryString["class"] != null)
                    cfy = Request.QueryString["class"];
                else
                    cfy = Request.QueryString["shortname"];

                if (Application["MasterIndex" + cfy] != null)
                {
                    MasterIndex = (List<string>)Application["MasterIndex" + cfy];
                }
                else
                {
                    MasterIndex = new List<string>();
                    foreach (Posts post in Posts.GetPostFormTopClassify(cfy))
                        MasterIndex.Add(post.ToIndex());

                    Application["MasterIndex" + cfy] = MasterIndex;
                }
                WebSubTitle = WebTitle;
                WebTitle = $"[{cfy}]";
            }
            else if (Request.QueryString["date"] != null)
            {
                string date = Request.QueryString["date"];
                //日期分类
                //先看看有没有给具体的日期
                if (DateTime.TryParse(date, out DateTime bef))
                {
                    bef = bef.Date;
                    if (Application["MasterIndex" + bef.ToShortDateString()] == null)
                    {
                        MasterIndex = new List<string>();
                        foreach (Posts post in Posts.GetPostFormDate(bef, bef.AddDays(1)))
                            MasterIndex.Add(post.ToIndex());
                        Application["MasterIndex" + bef.ToShortDateString()] = MasterIndex;
                    }
                    else
                    {
                        MasterIndex = (List<string>)Application["MasterIndex" + bef.ToShortDateString()];
                    }
                }
                else
                {
                    var spl = date.Split('/');
                    if (spl.Length == 2 && int.TryParse(spl[0], out int y) && int.TryParse(spl[1], out int m))
                    {                       
                        if (Application[$"MasterIndexDy{y}m{m}"] == null)
                        {
                            bef = new DateTime(y, m, 1);
                            MasterIndex = new List<string>();
                            foreach (Posts post in Posts.GetPostFormDate(bef, bef.AddMonths(1)))
                                MasterIndex.Add(post.ToIndex());
                            Application[$"MasterIndexDy{y}m{m}"] = MasterIndex;
                        }
                        else
                        {
                            MasterIndex = (List<string>)Application[$"MasterIndexDy{y}m{m}"];
                        }
                    }
                    else if (spl.Length == 1 && int.TryParse(spl[0], out y))
                    {
                        if (Application[$"MasterIndexDy{y}"] == null)
                        {
                            bef = new DateTime(y, 1, 1);
                            MasterIndex = new List<string>();
                            foreach (Posts post in Posts.GetPostFormDate(bef, bef.AddYears(1)))
                                MasterIndex.Add(post.ToIndex());
                            Application[$"MasterIndexDy{y}"] = MasterIndex;
                        }
                        else
                        {
                            MasterIndex = (List<string>)Application[$"MasterIndexDy{y}"];
                        }
                    }
                    else
                    {
                        //参考下方NOMALINDEX代码
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
                }
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
            LHeader.Text = LHeader.Text.Replace("<!--WWC:head-->", $"<title>{WebTitle} - {(page == 0 ? WebSubTitle : $"第{page + 1}页")}</title>");

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