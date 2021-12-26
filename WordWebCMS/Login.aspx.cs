using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using static WordWebCMS.Function;
namespace WordWebCMS
{
    public partial class Login : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            //Important:把缓存交给设置
            Setting.Application = this.Application;

            //TODO:判断是不是已经登陆了,如果已经登陆了就直接跳转到用户中心,目前为了debug先不写
#if !DEBUG
            if (Session["User"] != null)
                //已经登陆过了,直接跳转到用户信息页面
                Response.Redirect(Setting.WebsiteURL + "/User.aspx");
#endif

            //Header
            if (Application["MasterHeader"] == null)
                Application["MasterHeader"] = SMaster.GetHeaderHTML();
            LHeader.Text = ((string)Application["MasterHeader"]);

            string qus; string SessionID;
            if (MasterKey.Text == "" || Session[MasterKey.Text + "qus"] == null)
            {
                qus = RndQuestion(out int anser);
                SessionID = Rnd.Next().ToString("x");
                MasterKey.Text = SessionID;
                Session[SessionID + "qus"] = qus;
                Session[SessionID + "ans"] = anser;
            }
            else
            {
                SessionID = MasterKey.Text;
                qus = (string)Session[SessionID + "qus"];
                // = (int)Session[SessionID + "ans"];
            }

            ////int.TryParse(Request.QueryString["Error"], out int errorid);
            //if (Request.QueryString["Error"] != null && Session["err" + Request.QueryString["Error"]] != null)
            //{
            //    string errormessage = (string)Session["err" + Request.QueryString["Error"]];
            //    Session["err" + Request.QueryString["Error"]] = null;//清空错误信息缓存;

            //    errorboxlogin.Visible = true;
            //    errorboxregister.Visible = true;
            //    errorboxlogin.InnerText = errormessage;
            //    errorboxregister.InnerText = errormessage;
            //}



            //看看action是注册还是登陆
            switch (Request.QueryString["Action"])
            {
                case "Register":
                    CalregistKey.Text = qus;
                    divregister.Visible = true;
                    if (Setting.AlowRegister == false)
                    {//如果不允许注册
                        errorboxregister.Visible = true;
                        errorboxregister.InnerHtml = "当前网站未开放注册,如需注册请联系网站管理员<br/>如已有账户,请<a href=\"?Action=Login\">前往登陆</a>";
                        buttonregister.Enabled = false;
                        CalregistKey.Enabled = false;
                        passwordreg.Enabled = false;
                    }
                    else if (Setting.EnabledEmail)
                    {
                        emailcheck.Visible = true;//只有开启发送邮件功能,会发送验证码

                    }
                    LHeader.Text = LHeader.Text.Replace("<!--WWC:head-->", $"<title>{Setting.WebTitle} - 注册</title>");
                    break;
                case "Forget":
                    Calforgetkey.Text = qus;
                    divforget.Visible = true;
                    if (!Setting.EnabledEmail)
                    {
                        errorboxforget.Visible = true;
                        errorboxforget.InnerHtml = "当前网站未启用邮件服务,无法通过邮箱找回<br/>请联系 " + Setting.ContactLink;
                        Calforgetkey.Enabled = false;
                    }
                    break;
                case "FindMy":
                    string msk = Request.QueryString["MasterKey"];
                    string key = Request.QueryString["Key"];
                    if (string.IsNullOrEmpty(msk) || string.IsNullOrEmpty(key))
                    {
                        //直接跳转到登录,不提示
                        goto default;
                    }
                    divfindmy.Visible = true;
                    string ip = HttpContext.Current.Request.UserHostAddress;
                    if (Setting.BanIPCheck(ip))
                    {
                        errorboxfindmy.Visible = true;
                        errorboxfindmy.InnerText = "由于申请重置次数过多,今日已无法重新尝试重置";
                        //TODO:永久性的黑名单,使用数据库
                        //TODO:储存错误尝试到数据库,给后台看
                    }
                    else if (Application[msk + "FindMy"] == null || Application[msk + "FindMyUser"] == null)
                    {
                        Setting.BanIP(ip, 1);
                        errorboxfindmy.Visible = true;
                        errorboxfindmy.InnerText = "您的密码重置链接已过期,请重新<a href=\"?Action=Forget\">找回密码</a>";
                    }
                    else if (!int.TryParse(key, out int k))
                    {
                        Setting.BanIP(ip, 1);
                        errorboxfindmy.Visible = true;
                        errorboxfindmy.InnerText = "您的密码重置链接有误,请检查链接是否正确 <br />或 重新<a href=\"?Action=Forget\">找回密码</a>";
                    }
                    else if ((int)Application[msk + "FindMy"] != k)
                    {
                        Setting.BanIP(ip, 2);
                        errorboxfindmy.Visible = true;
                        errorboxfindmy.InnerText = "您的密码重置链接有误,请检查链接是否正确 <br />或 重新<a href=\"?Action=Forget\">找回密码</a>";
                    }
                    else
                    {
                        //全部通过,可以重置密码
                        string newpass;
                        Users usr = (Users)Application[msk + "FindMyUser"];
                        if (Application[msk + "FindMyPass"] == null)
                        {
                            newpass = Rnd.Next().ToString("x") + Rnd.Next().ToString("x");
                            Application[msk + "FindMyPass"] = newpass;
                            usr.PasswordSet(newpass);
                        }
                        else
                        {
                            newpass = (string)Application[msk + "FindMyPass"];
                        }
                        emailfindmy.Text = usr.UserName;
                        passwordfindmy.Text = newpass;
                    }
                    break;
                default:
                    divlogin.Visible = true;
                    CalloginKey.Text = qus;
                    LHeader.Text = LHeader.Text.Replace("<!--WWC:head-->", $"<title>{Setting.WebTitle} - 登录</title>");
                    break;
            }

            //Footer
            if (Application["MasterFooter"] == null)
                Application["MasterFooter"] = SMaster.GetFooterHTML();
            LFooter.Text = ((string)Application["MasterFooter"]);
        }

        //TODO: 通过cookie记录登陆信息 长时间登陆
        protected void buttonlogin_Click(object sender, EventArgs e)
        {
            if (MasterKey.Text == "" || Session[MasterKey.Text + "ans"] == null)
            //连接丢失,请重试
            {
                //Response.Redirect(Setting.WebsiteURL + "/login.aspx?" + SendERRORMSG("验证码缓存已丢失,请重试"));
                //Response.End();
                errorboxlogin.Visible = true;
                errorboxlogin.InnerText = "验证码缓存已丢失,请重试";
            }
            else if (int.TryParse(checkloginkey.Text, out int ans))
            {
                if ((int)Session[MasterKey.Text + "ans"] == ans)
                {
                    //全部正确,开始判断能否登陆
                    if (usernamelogin.Text == "")
                    {
                        errorboxlogin.Visible = true;
                        errorboxlogin.InnerText = "请输入账号";
                        return;
                    }
                    if (passwordlogin.Text == "")
                    {
                        errorboxlogin.Visible = true;
                        errorboxlogin.InnerText = "请输入密码";
                        return;
                    }

                    string ip = HttpContext.Current.Request.UserHostAddress;

                    if (Setting.BanIPCheck(ip))
                    {
                        errorboxlogin.Visible = true;
                        errorboxlogin.InnerText = "由于错误次数过多,今日已无法重新尝试登陆";
                        return;//TODO:永久性的黑名单,使用数据库
                        //TODO:储存错误尝试到数据库,给后台看
                    }

                    Users usr = Users.GetUser(usernamelogin.Text);
                    if (usr == null)
                    {
                        errorboxlogin.Visible = true;
                        errorboxlogin.InnerHtml = "未找到账号信息,请检查输入或<a href=\"?Action=Register\">注册新账号</a>";
                        //由于涉及到了判断,清空验证码要求重新输入
                        string qus = RndQuestion(out int anser);
                        string SessionID = Rnd.Next().ToString("x");
                        MasterKey.Text = SessionID;
                        Session[SessionID + "qus"] = qus;
                        Session[SessionID + "ans"] = anser;
                        CalloginKey.Text = qus;

                        Setting.BanIP(ip, 1);
                        return;
                    }

                    if (!usr.PasswordCheck(passwordlogin.Text))
                    {
                        errorboxlogin.Visible = true;
                        errorboxlogin.InnerText = "密码输入错误,请检查输入";
                        //由于涉及到了判断,清空验证码要求重新输入
                        string qus = RndQuestion(out int anser);
                        string SessionID = Rnd.Next().ToString("x");
                        MasterKey.Text = SessionID;
                        Session[SessionID + "qus"] = qus;
                        Session[SessionID + "ans"] = anser;
                        CalloginKey.Text = qus;

                        Setting.BanIP(ip, 2);//验证密码错误的惩罚多来点
                        return;
                    }
                    //登陆成功: 把数据存Session

                    Session["User"] = usr;

                    Response.Write($"<script language='javascript'>alert('登陆成功!\\n欢迎回来,{usr.UserName}');window.location.href='{(Request.UrlReferrer == null || Request.UrlReferrer.ToString().ToLower().Contains("login.aspx") ? "\\index.aspx" : Request.UrlReferrer.ToString())}'</script>");
                }
                else
                {
                    //Response.Redirect(Setting.WebsiteURL + "/login.aspx?" + SendERRORMSG("验证码错误,请重新计算"));
                    //Response.End();
                    errorboxlogin.Visible = true;
                    errorboxlogin.InnerText = "验证码错误,请重新计算";
                }
            }
            else
            {
                errorboxlogin.Visible = true;
                errorboxlogin.InnerText = "验证码为纯数字,请检查输入";
            }
        }

        protected void buttonregister_Click(object sender, EventArgs e)
        {
            if (MasterKey.Text == "" || Session[MasterKey.Text + "ans"] == null)
            //连接丢失,请重试
            {
                errorboxregister.Visible = true;
                errorboxregister.InnerText = "验证码缓存已丢失,请重试";
            }
            else if (Setting.AlowRegister == false)
            {
                errorboxregister.Visible = true;
                errorboxregister.InnerHtml = "当前网站未开放注册,如需注册请联系网站管理员<br/>如已有账户,请<a href=\"?Action=Login\">前往登陆</a>";
                buttonregister.Enabled = false;
                CalregistKey.Enabled = false;
                passwordreg.Enabled = false;
            }
            else if (int.TryParse(checkregisterkey.Text, out int ans))
            {
                if ((int)Session[MasterKey.Text + "ans"] == ans)
                {
                    string ip = HttpContext.Current.Request.UserHostAddress;

                    if (Setting.BanIPCheck(ip))
                    {
                        errorboxregister.Visible = true;
                        errorboxregister.InnerText = "由于错误次数过多,今日已无法重新尝试登陆";
                        return;//TODO:永久性的黑名单,使用数据库
                        //TODO:储存错误尝试到数据库,给后台看
                    }

                    if (usernamereg.Text == "")
                    {
                        errorboxregister.Visible = true;
                        errorboxregister.InnerText = "请输入账号";
                        return;
                    }
                    else if (passwordreg.Text == "")
                    {
                        errorboxregister.Visible = true;
                        errorboxregister.InnerText = "请输入密码";
                        return;
                    }
                    else if (passwordreg.Text.Length < 8)
                    {
                        errorboxregister.Visible = true;
                        errorboxregister.InnerText = "密码长度必须大于等于8位";
                        return;
                    }


                    //判断邮箱激活
                    if (Setting.EnabledEmail)
                    {
                        if (string.IsNullOrEmpty(emailcode.Text) || !int.TryParse(emailcode.Text, out int emc))
                        {
                            errorboxregister.Visible = true;
                            errorboxregister.InnerText = "请输入邮件验证码";
                            return;
                        }
                        else if (emc != (int)Session[MasterKey.Text + "mail"])
                        {
                            errorboxregister.Visible = true;
                            errorboxregister.InnerText = "邮件验证码有误,请重试";
                            Setting.BanIP(ip, 1);
                            return;
                        }
                    }

                    //全部正确,开始判断注册
                    Users usr = Users.GetUser(usernamereg.Text);
                    if (usr == null)
                        usr = Users.GetUser(emailreg.Text);
                    if (usr != null)
                    {
                        errorboxregister.Visible = true;
                        errorboxregister.InnerHtml = "该用户名或邮件账户已存在";
                        return;
                    }
                    //开始注册
                    usr = Users.CreatUser(usernamereg.Text, emailreg.Text, passwordreg.Text);
                    //把数据存Session
                    Session["User"] = usr;
                    Response.Write($"<script language='javascript'>alert('注册成功!\\n欢迎新用户,{usr.UserName}');window.location.href='{(Request.UrlReferrer == null || Request.UrlReferrer.ToString().ToLower().Contains("login.aspx") ? "\\index.aspx" : Request.UrlReferrer.ToString())}'</script>");
                }
                else
                {
                    errorboxregister.Visible = true;
                    errorboxregister.InnerText = "验证码错误,请重新计算";
                }
            }
            else
            {
                errorboxregister.Visible = true;
                errorboxregister.InnerText = "验证码为纯数字,请检查输入";
            }
        }

        protected void buttonsendmail_Click(object sender, EventArgs e)
        {
            string email = emailforget.Text;
            if (MasterKey.Text == "" || Session[MasterKey.Text + "ans"] == null)
            //连接丢失,请重试
            {
                errorboxforget.Visible = true;
                errorboxforget.InnerText = "验证码缓存已丢失,请重试";
            }
            else if (!Setting.EnabledEmail)
            {
                errorboxforget.Visible = true;
                errorboxforget.InnerHtml = "当前网站未启用邮件服务,无法通过邮箱找回<br/>请联系 " + Setting.ContactLink;
                Calforgetkey.Enabled = false;
            }
            else if (!TypeEmail(email))
            {
                errorboxforget.Visible = true;
                errorboxforget.InnerText = "请输入正确的邮件格式";
            }
            else if (int.TryParse(checkforgetkey.Text, out int ans))
            {
                if ((int)Session[MasterKey.Text + "ans"] == ans)
                {
                    string ip = HttpContext.Current.Request.UserHostAddress;

                    if (Setting.BanIPCheck(ip))
                    {
                        errorboxforget.Visible = true;
                        errorboxforget.InnerText = "由于申请重置次数过多,今日已无法重新尝试重置";
                        return;//TODO:永久性的黑名单,使用数据库
                        //TODO:储存错误尝试到数据库,给后台看
                    }
                    if (!(Session["MailTimeing"] == null))
                    {
                        int waitsec = (int)DateTime.Now.Subtract((DateTime)Session["MailTimeing"]).TotalSeconds;
                        if (waitsec < 60)
                        {
                            errorboxforget.Visible = true;
                            errorboxforget.InnerText = $"请等待{60 - waitsec}秒后重试";
                            return;
                        }
                    }
                    //全部正确,开始用户是否存在
                    Users usr = Users.GetUser(emailforget.Text);
                    if (usr == null)
                    {
                        errorboxforget.Visible = true;
                        errorboxforget.InnerHtml = "该邮件账户不存在";
                        Setting.BanIP(ip, 1);
                        return;
                    }
                    Application[MasterKey.Text + "FindMy"] = Rnd.Next(100000, 999999);
                    Application[MasterKey.Text + "FindMyUser"] = usr;
                    string Link = Setting.WebsiteURL + $"/Login.aspx?Action=FindMy&MasterKey={MasterKey.Text}&Key={Application[MasterKey.Text + "FindMy"]}";

                    string msg = SendEmail(Setting.WebTitle + " 重置账户密码", $"您正在找回您的账号密码,这是您的重置密码链接:\n<a href={Link}>{Link}</a>\n点击链接或复制到浏览器打开\n该链接仅用于重置密码,请不要将该链接泄露给他人. \n如果此活动不是您本人操作,请无视该邮件\n如需帮助,请联系网站管理员 {Setting.ContactLink}", email);
                    Session["MailTimeing"] = DateTime.Now;
                    if (string.IsNullOrEmpty(msg))
                        Response.Write($"<script language='javascript'>alert('重置密码邮件发送成功!\\n请注意查收邮件,{usr.UserName}');window.location.href='{(Request.UrlReferrer == null || Request.UrlReferrer.ToString().ToLower().Contains("login.aspx") ? "\\index.aspx" : Request.UrlReferrer.ToString())}'</script>");
                    else
                    {
                        errorboxforget.Visible = true;
                        errorboxforget.InnerHtml = "邮件发送失败:" + msg;
                    }
                }
                else
                {
                    errorboxforget.Visible = true;
                    errorboxforget.InnerText = "验证码错误,请重新计算";
                }
            }
            else
            {
                errorboxforget.Visible = true;
                errorboxforget.InnerText = "验证码为纯数字,请检查输入";
            }
        }
    }
}