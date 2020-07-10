﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WordWebCMS
{
    public partial class Login : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            //Important:把缓存交给设置
            Setting.Application = this.Application;
            //Header
            if (Application["MasterHeader"] == null)
                Application["MasterHeader"] = SMaster.GetHeaderHTML();
            LHeader.Text = ((string)Application["MasterHeader"]);

            //Footer
            if (Application["MasterFooter"] == null)
                Application["MasterFooter"] = (string)Application["MasterFooter"];
            LFooter.Text = ((string)Application["MasterFooter"]);
        }
    }
}