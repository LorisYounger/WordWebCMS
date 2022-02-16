using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WordWebCMS
{
    public partial class UserInfo : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            //Important:把缓存交给设置
            Setting.Application = this.Application;

            if (Application["MasterHeader"] == null)
                Application["MasterHeader"] = SMaster.GetHeaderHTML();
            LHeader.Text = ((string)Application["MasterHeader"]);

            //Footer
            if (Application["MasterFooter"] == null)
                Application["MasterFooter"] = SMaster.GetFooterHTML();
            LFooter.Text = ((string)Application["MasterFooter"]);


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

            //查看用户相关
            Users display = null;
            //先看看有没有给UID
            if (Request.QueryString["id"] != null)
            {
                if (int.TryParse(Request.QueryString["id"], out int uID))
                    display = Users.GetUser(uID);//获得post信息                
            }
            if (display == null)
                if (usr == null)
                    Goto404();
                else
                    display = usr;
            
            //然后开始显示用户界面


        }
        public void Goto404(string type = "user")
        {
            Response.Redirect(Setting.WebsiteURL + "/404.aspx?type=" + type);
            Response.End();
            return;
        }
    }
}