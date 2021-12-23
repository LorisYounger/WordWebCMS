using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace WordWebCMS
{
    public static class SMaster
    {
        /// <summary>
        /// 获取相关输出HTML
        /// </summary>
        public static class GetHTML
        {
            public static string MenuItem()
            {
                StringBuilder sb = new StringBuilder();
                foreach (LinePutScript.Sub sub in WordWebCMS.Setting.MenuList)
                {
                    sb.AppendLine($"<li class=\"current-menu-item\"><a href=\"{sub.Info}\">{sub.Name}</a></li>");
                }
                return sb.ToString();
            }


        }
        public static string ReplaceHTML(string html)
            => html.Replace("<!--WWC:themepath-->", $"Themes/{Setting.Themes}").Replace("<!--WWC:webinfo-->", Setting.WebInfo)
                .Replace("<!--WWC:webtitle-->", Setting.WebTitle).Replace("<!--WWC:websubtitle-->", Setting.WebSubTitle)
                .Replace("<!--WWC:menuitem-->", GetHTML.MenuItem()).Replace("<!--WWC:websiteurl-->", Setting.WebsiteURL)
                .Replace("<!--WWC:sidebar1-->", Setting.SideBar1).Replace("<!--WWC:sidebar2-->", Setting.SideBar2).Replace("<!--WWC:sidebar3-->", Setting.SideBar3)
            .Replace("<!--WWC:icon-->", Setting.Icon)
            ;
        public static string GetHeaderHTML()
            => ReplaceHTML(System.IO.File.ReadAllText(HttpContext.Current.Server.MapPath($"Themes/{Setting.Themes}/header.html")));
        public static string GetFooterHTML()
            => ReplaceHTML(System.IO.File.ReadAllText(HttpContext.Current.Server.MapPath($"Themes/{Setting.Themes}/footer.html")));
        public static string GetNoLoginHTML()
            => "<aside id=\"widget-user\" class=\"widget\"><h2 class=\"widget-title\">用户中心</h2><ul><li>未登录</li><li><a href=\"Login.aspx\">->点击此处前往登陆页面</a></li></ul></aside>";
    }
}