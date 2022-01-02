using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using LinePutScript;

namespace WordWebCMS
{
    public partial class Setup : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            //Important:把缓存交给设置
            Setting.Application = this.Application;

            //判断部署情况
            if (Application["setupfastcheck"] == null)
                try
                {
                    Application["dtbfSetting"] = Conn.RAW.ExecuteQuery("SELECT * FROM setting");
                    if (Setting.DisableSetup)
                    {
                        Application["setupfastcheck"] = true;
                        divdisable.Visible = true;
                        return;
                    }
                    Application["setupfastcheck"] = false;
                }
                catch
                {
                    Application["setupfastcheck"] = false;
                }
            else if ((bool)Application["setupfastcheck"])
            {
                divdisable.Visible = true;
                return;
            }

            int step = 1;
            if (!string.IsNullOrEmpty(Request.QueryString["step"]))
            {
                if (!int.TryParse(Request.QueryString["step"], out step))
                {
                    step = 1;
                }
            }
            switch (step)
            {
                default:
                case 1://步骤1:修改web.config
                    divfirst.Visible = true;
                    //帮助自动填写信息
                    if (ConfigurationManager.ConnectionStrings["connUsrStr"].ConnectionString != ConfigurationManager.ConnectionStrings["connStr"].ConnectionString && Session["setupstop1"] == null)
                    {
                        RadioUserDataYes.Checked = true;
                        Session["setupstop1"] = true;
                    }
                    SetTextBox(TextBoxOtherLink, ConfigurationManager.ConnectionStrings["connUsrStr"].ConnectionString);
                    Line conn = new Line(ConfigurationManager.ConnectionStrings["connStr"].ConnectionString.Replace(" ;", ":|").Replace("; ", ":|").Replace(";", ":|").Trim().Replace("=", "#"));
                    SetTextBox(TextBoxServer, conn.GetString("Server", conn.GetString("server", "服务器地址,IP或URL")));
                    SetTextBox(TextBoxPort, conn.GetString("Port", conn.GetString("port", "服务器端口")));
                    SetTextBox(TextBoxPass, conn.GetString("User", conn.GetString("user", "数据库用户名")));
                    SetTextBox(TextBoxUserName, conn.GetString("Password", conn.GetString("password", "数据库密码")));
                    SetTextBox(TextBoxTable, conn.GetString("Database", conn.GetString("database", "数据库表名")));
                    break;
                case 2:
                    divsecond.Visible = true;
                    break;
                case 3:
                    //判断是否有用户
                    try
                    {
                        var usr = Users.GetUser(1);
                        if (usr == null)
                        {
                            divCreateAdmin.Visible = true;
                        }
                        else
                        {
                            divhaveadmin.Visible = true;
                            padmininfo.InnerHtml = $"用户名: {usr.UserName}<br />邮件: {usr.Email}<br />权限: {usr.Authority}";
                        }
                    }
                    catch
                    {
                        goto case 1;
                    }
                    divThird.Visible = true;
                    break;
                case 4:
                    //第四步:初始化网站信息
                    divfourth.Visible = true;
                    SetTextBox(TextBoxwebtitle, Setting.WebTitle);
                    SetTextBox(TextBoxwebsubtitle, Setting.WebSubTitle);
                    SetTextBox(TextBoxweburl, Setting.WebsiteURL);
                    SetTextBox(TextBoxwebconnlink, Setting.ContactLink);
                    if (Setting.EnabledEmail && Session["setupstop2"] == null)
                    {
                        Session["setupstop2"] = true;
                        CheckBoxemail.Checked = true;
                    }

                    SetTextBox(TextBoxwebemailsmtp, Setting.SMTPEmail);
                    SetTextBox(TextBoxwebemailpass, Setting.SMTPPassword);
                    SetTextBox(TextBoxwebemaillink, Setting.SMTPURL);
                    SetTextBox(TextBoxwebicon, Setting.Icon);
                    SetTextBox(TextBoxwebinfo, Setting.WebInfo);
                    if (Setting.WebInfo.ToLower().Contains("wordwebcms"))
                        CheckBoxwebwwcms.Checked = true;
                    if (Session["setupstop3"] == null)
                    {
                        Session["setupstop3"] = true;
                        CheckBoxweballowreg.Checked = Setting.AllowRegister;
                    }
                    SetTextBox(TextBoxwebnewmoney, Setting.NewUserMoney.ToString());
                    SetTextBox(TextBoxwebuserAuth, ((int)Setting.UserAuthorityDefault).ToString());
                    SetTextBox(TextBoxpoststatie, ((int)Setting.PostDefault).ToString());
                    SetTextBox(TextBoxreviewstatie, ((int)Setting.ReviewDefault).ToString());
                    if (Session["setupstop4"] == null)
                    {
                        Session["setupstop4"] = true;
                        CheckBoxweburlrewrite.Checked = Setting.EnabledUrlRewrite;
                    }
                    break;
                case 5:
                    divfifth.Visible = true;
                    break;
            }
        }
        private void SetTextBox(TextBox tb, string str)
        {
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = str;
            }
        }
        protected void RadioUserDataYes_CheckedChanged(object sender, EventArgs e)
        {
            divuserdatayes.Visible = RadioUserDataYes.Checked;
        }


        protected void ButtonGenConn_Click(object sender, EventArgs e)
        {
            div1output.Visible = true;
            string connstr = $"Server={TextBoxServer.Text};port={TextBoxPort.Text};Database={TextBoxTable.Text};User={TextBoxUserName.Text};Password={TextBoxPass.Text};{TextBoxotherconn.Text}";
            TextBoxOutputConn.InnerHtml = $"&lt;connectionStrings&gt;<br />&nbsp;&nbsp;&nbsp;&nbsp;&lt;!-- 数据库连接字符串 --&gt;<br />&nbsp;&nbsp;&nbsp;&nbsp;&lt;add name=\"connStr\" connectionString=\"{connstr}\" /&gt;<br />&nbsp;&nbsp;&nbsp;&nbsp;&lt;!-- 用户数据库连接字符串 : 用户数据若使用相同数据库 则写一样的即可 --&gt;<br />&nbsp;&nbsp;&nbsp;&nbsp;&lt;add name=\"connUsrStr\" connectionString=\"{(RadioUserDataYes.Checked ? TextBoxOtherLink.Text : connstr)}\" /&gt;<br />&lt;/connectionStrings&gt;";
        }

        protected void ButtonCreateTable_Click(object sender, EventArgs e)
        {
            pcreateinfo.Visible = true;
#if !DEBUG
            try
            {
#endif
            StringBuilder sb = new StringBuilder();
            string database = ConfigurationManager.ConnectionStrings["connStr"].ConnectionString.ToLower().Split(';').FirstOrDefault(x => x.Trim().StartsWith("database")).Split('=').Last().Trim();
            //先看看设置表在不在
            var lpt = Conn.RAW.ExecuteQuery($"SELECT * FROM information_schema.TABLES WHERE `TABLE_SCHEMA` ='{database}' AND `TABLE_NAME` ='setting';");
            if (lpt.Count == 0)
            {//设置表不存在,现在创建
                Conn.RAW.ExecuteNonQuery("CREATE TABLE `setting` ( `selector` VARCHAR(64) NOT NULL COMMENT '设置项目' , `property` TEXT NOT NULL COMMENT '设置内容' ) COMMENT = '设置表';ALTER TABLE `setting` ADD PRIMARY KEY(`selector`);");
                sb.AppendLine("设置表 `setting` 已创建");
            }
            else
            {
                sb.AppendLine("设置表 `setting` 已存在 跳过");
            }

            //文章表
            lpt = Conn.RAW.ExecuteQuery($"SELECT * FROM information_schema.TABLES WHERE `TABLE_SCHEMA` ='{database}' AND `TABLE_NAME` ='post';");
            if (lpt.Count == 0)
            {//不存在,现在创建
                Conn.RAW.ExecuteNonQuery("CREATE TABLE `post` ( `Pid` INT NOT NULL AUTO_INCREMENT COMMENT '文章id' , `name` VARCHAR(256) NOT NULL COMMENT '文章名' , `shortname` VARCHAR(64) NOT NULL COMMENT '短名 唯一' , `excerpt` VARCHAR(512) NOT NULL COMMENT '摘录/简介' , `content` MEDIUMTEXT NOT NULL COMMENT '内容' , `author` INT NOT NULL COMMENT '作者id' , `postdate` DATETIME NOT NULL COMMENT '发布日期' , `modifydate` DATETIME NOT NULL COMMENT '修改日期' , `classify` VARCHAR(256) NOT NULL COMMENT '分类目录' , `state` TINYINT NOT NULL DEFAULT '0' COMMENT '文章类型' , `attachment` TINYTEXT NULL DEFAULT NULL COMMENT '附图' , `password` VARCHAR(32) NULL DEFAULT NULL COMMENT '密码md5s' , `anzhtml` BOOLEAN NOT NULL DEFAULT FALSE COMMENT '是否分析html' , `allowcomments` BOOLEAN NOT NULL DEFAULT TRUE COMMENT '是否允许评论' , `readers` INT NOT NULL DEFAULT '0' COMMENT '阅读量' , `likes` INT NOT NULL DEFAULT '0' COMMENT '赞同数' , PRIMARY KEY (`Pid`), INDEX (`name`), UNIQUE (`shortname`)) COMMENT = '文章表';");
                sb.AppendLine("文章表 `post` 已创建");
                Conn.RAW.ExecuteNonQuery(@"INSERT INTO `post` (`Pid`, `name`, `shortname`, `excerpt`, `content`, `author`, `postdate`, `modifydate`, `classify`, `state`, `attachment`, `password`, `anzhtml`, `allowcomments`, `readers`, `likes`) VALUES (NULL, '你好,世界 又一个WWCMS站点', 'helloworld', '一篇系统生成的默认文章\n这是系统自动生成的演示文章。编辑或者删除它，然后开始您的博客！', '## 欢迎使用 WordWebCMS。\n这是系统自动生成的演示文章。编辑或者删除它，然后开始您的博客！\n\n## WordWebCMS支持Markdown\n**Markdown**是一种轻量级的「标记语言」\n**Markdown**是一种可以使用普通文本编辑器编写的标记语言，通过简单的标记语法，它可以使普通文本内容具有一定的*格式*。它允许人们使用易读易写的纯文本格式编写文档，然后转换成格式丰富的HTML页面\n\n## 关于WordWebCMS\n由[洛里斯杨远](https://zoujin.exlb.org)编写的一款内容管理网站\n项目地址:[https://github.com/LorisYounger/WordWebCMS](https://github.com/LorisYounger/WordWebCMS)\n\r\n', '1', NOW(), NOW(), '未分类', '5', NULL, NULL, '0', '1', '100', '20');");
                sb.AppendLine("文章 HelloWorld 已创建");
            }
            else
            {
                sb.AppendLine("文章表 `post` 已存在 跳过");
            }
            //评论表
            lpt = Conn.RAW.ExecuteQuery($"SELECT * FROM information_schema.TABLES WHERE `TABLE_SCHEMA` ='{database}' AND `TABLE_NAME` ='review';");
            if (lpt.Count == 0)
            {//不存在,现在创建
                Conn.RAW.ExecuteNonQuery("CREATE TABLE `review` ( `Rid` INT NOT NULL AUTO_INCREMENT COMMENT '评论id' , `Pid` INT NOT NULL COMMENT '文章id' , `content` TEXT NOT NULL COMMENT '内容' , `author` INT NOT NULL COMMENT '作者id' , `postdate` DATETIME NOT NULL COMMENT '发布日期' , `modifydate` DATETIME NOT NULL COMMENT '修改日期' , `state` TINYINT NOT NULL DEFAULT '0' COMMENT '评论类型' , `anzhtml` BOOLEAN NOT NULL DEFAULT FALSE COMMENT '是否分析html' , `likes` INT NOT NULL DEFAULT '0' COMMENT '赞同数' , PRIMARY KEY (`Rid`)) COMMENT = '评论表';");
                sb.AppendLine("评论表 `review` 已创建");
                Conn.RAW.ExecuteNonQuery("INSERT INTO `review` (`Rid`, `Pid`, `content`, `author`, `postdate`, `modifydate`, `state`, `anzhtml`, `likes`) VALUES (NULL, '1', '你好 我也觉得WordWebCMS最棒了\r\n# 首先 评论也支持Markdown\r\n我觉得这点很棒\r\n\r\n我特别喜欢WWCMS,**你呢?**', '1', NOW(), NOW(), '7', '0', '10');");
                sb.AppendLine("评论 HelloWorld 已创建");
            }
            else
            {
                sb.AppendLine("评论表 `review` 已存在 跳过");
            }
            //用户table
            database = ConfigurationManager.ConnectionStrings["connUsrStr"].ConnectionString.ToLower().Split(';').FirstOrDefault(x => x.Trim().StartsWith("database")).Split('=').Last().Trim();
            //用户表
            lpt = Conn.RAWUser.ExecuteQuery($"SELECT * FROM information_schema.TABLES WHERE `TABLE_SCHEMA` ='{database}' AND `TABLE_NAME` ='users';");
            if (lpt.Count == 0)
            {//用户表不存在,现在创建
                Conn.RAWUser.ExecuteNonQuery("CREATE TABLE `users` ( `Uid` INT NOT NULL AUTO_INCREMENT COMMENT '用户id' , `name` VARCHAR(30) NOT NULL COMMENT '用户名' , `email` VARCHAR(40) NOT NULL COMMENT '电子邮件' , `password` VARCHAR(32) NOT NULL COMMENT '密码md5s' , `isroot` BOOLEAN NOT NULL DEFAULT FALSE COMMENT '是超级管理员' , `money` INT NOT NULL DEFAULT '0' COMMENT '金钱' , `exp` INT NOT NULL DEFAULT '0' COMMENT '经验值' , `lv` INT NULL DEFAULT '1' COMMENT '等级' , `headport` TINYTEXT NULL DEFAULT NULL COMMENT '头像url' , PRIMARY KEY (`Uid`), INDEX (`name`)) COMMENT = '用户表';");
                sb.AppendLine("用户表 `users` 已创建");
            }
            else
            {
                sb.AppendLine("用户表 `users` 已存在 跳过");
            }
            sb.AppendLine("所有数据表安装成功");
            pcreateinfo.InnerHtml = $"<p style=\"color: #444; padding: 0; font-family: Consolas;\">{sb.ToString().Replace("\n", "<br />")}</p>";
#if !DEBUG
        }
            catch (Exception ex)
            {
                pcreateinfo.InnerHtml = "<p style=\"color:#e88;\">发生错误<br />" + ex.Message.Replace("\n", "<br />") + "</p>";
            }
#endif
        }

        protected void Buttoncreateadmin_Click(object sender, EventArgs e)
        {
            if (Users.GetUser(1) != null)
            {
                return;
            }
            Buttoncreateadmin.Visible = false;
            padminmessage.Visible = true;
            Conn.RAWUser.ExecuteNonQuery($"INSERT INTO `users` (`Uid`, `name`, `email`, `password`, `isroot`, `money`, `exp`, `lv`, `headport`) VALUES (1, '{TextBoxadminname.Text}', '{TextBoxadminemail.Text}', '{Function.MD5salt(TextBoxadminpassword.Text)}', '1', '100', '0', '10', NULL);");
        }

        protected void Buttonpasswordmd5_Click(object sender, EventArgs e)
        {
            TextBoxnewadminpassword.Text = Function.MD5salt(TextBoxnewadminpassword.Text);
        }

        protected void CheckBoxemail_CheckedChanged(object sender, EventArgs e)
        {
            divemail.Visible = CheckBoxemail.Checked;
        }

        protected void ButtonwebUpdate_Click(object sender, EventArgs e)
        {
            bool webwfmdf = Setting.WebInfo != TextBoxwebinfo.Text;
            Setting.WebTitle = TextBoxwebtitle.Text;
            Setting.WebSubTitle = TextBoxwebsubtitle.Text;
            Setting.WebsiteURL = TextBoxweburl.Text;
            Setting.ContactLink = TextBoxwebconnlink.Text;
            if (CheckBoxemail.Checked)
            {
                Setting.EnabledEmail = true;
                Setting.SMTPEmail = TextBoxwebemailsmtp.Text;
                Setting.SMTPPassword = TextBoxwebemailpass.Text;
                Setting.SMTPURL = TextBoxwebemaillink.Text;
            }
            else
                Setting.EnabledEmail = true;

            Setting.Icon = TextBoxwebicon.Text;
            if (webwfmdf && Setting.WebInfo != TextBoxwebinfo.Text)
                Setting.WebInfo = TextBoxwebinfo.Text;
            if (CheckBoxwebwwcms.Checked)
            {
                if (!TextBoxwebinfo.Text.ToLower().Contains("wordwebcms"))
                {
                    Setting.WebInfo += "Power by <a href=\"https://github.com/LorisYounger/WordWebCMS\">WordWebCMS</a>";
                }
            }
            Setting.AllowRegister = CheckBoxweballowreg.Checked;
            int.TryParse(TextBoxwebnewmoney.Text, out int tmp);
            Setting.NewUserMoney = tmp;
            int.TryParse(TextBoxwebuserAuth.Text, out tmp);
            Setting.UserAuthorityDefault = (Setting.AuthLevel)tmp;
            int.TryParse(TextBoxpoststatie.Text, out tmp);
            Setting.PostDefault = (Posts.PostState)tmp;
            int.TryParse(TextBoxreviewstatie.Text, out tmp);
            Setting.ReviewDefault = (Review.ReviewState)tmp;
            Setting.EnabledUrlRewrite = CheckBoxweburlrewrite.Checked;
            pwebsuccess.Visible = true;
            pwebsuccess.InnerText = "设置保存成功 " + DateTime.Now;
        }

        protected void ButtonStop_Click(object sender, EventArgs e)
        {
            Setting.DisableSetup = true;
            Application["setupfastcheck"] = true;
            Response.Redirect(Setting.WebsiteURL + "/Setup.aspx");
            Response.End();
        }
    }
}