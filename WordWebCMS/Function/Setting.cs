using System;
using System.Collections.Generic;
using System.Web;
using LinePutScript;
using LinePutScript.SQLHelper;
using static WordWebCMS.Conn;

namespace WordWebCMS
{
    //设置表格式:
    //   -setting
    //  selector(string64)|property(text)
    //        设置项目    |   设置内容
    //CREATE TABLE `wwcms`.`setting` ( `selector` VARCHAR(64) NOT NULL COMMENT '设置项目' , `property` TEXT NOT NULL COMMENT '设置内容' ) ENGINE = InnoDB COMMENT = '设置表';ALTER TABLE `setting` ADD PRIMARY KEY(`selector`);
    public static class Setting
    {
        #region 数据库Data
        /// <summary>
        /// 从数据库获得元数据
        /// </summary>
        public static LpsDocument Data
        {
            //get => new LpsDocument();//Debug
            get
            {
                int times = 0;
            ReTry:
                try
                {
                    times++;
                    return RAW.ExecuteQuery("SELECT * FROM setting");
                }
                catch
                {
                    if (times >= 10)
                        throw new Exception("WWCMS:无法连接数据库");
                    System.Threading.Thread.Sleep(1000);
                    goto ReTry;
                }
            }
        }
        /// <summary>
        /// 从缓存中获取的元数据
        /// </summary>
        public static LpsDocument DataBuff //用于读取用 如果要写啥 禁止使用这个 需手动获取最新数据
        {
            get
            {
                if (databf == null)
                {
                    if (Application["dtbfSetting"] == null)
                    {
                        databf = Data;
                        Application["dtbfSetting"] = databf;
                    }
                    else
                        databf = (LpsDocument)Application["dtbfSetting"];
                }
                return databf;
            }
            set
            {
                Application["dtbfSetting"] = null;
                databf = null;
            }
        }
        private static LpsDocument databf;//用于读取用 如果要写啥 禁止使用这个 需手动获取最新数据
        #endregion

        public static System.Web.HttpApplicationState Application = null;


        #region 用户权限信息处理
        /// <summary>
        /// 获得用户的权限辨识
        /// </summary>
        public static AuthLevel GetUserAuthority(int uID)
        {
            Line lin = DataBuff.Assemblage.Find(x => x.info == "auth_" + uID.ToString());
            if (lin == null)
                return UserAuthorityDefault;
            return (AuthLevel)Convert.ToSByte(lin.First().info);
        }//set => RAW.ExecuteNonQuery($"UPDATE user SET auth=@auth WHERE Uid=@uid", new MySQLHelper.Parameter("auth", (byte)value), new MySQLHelper.Parameter("uid", uID));
        /// <summary>
        /// 获得用户的权限辨识
        /// </summary>
        public static void SetUserAuthority(int uID, AuthLevel auth)
        {
            Line lin = DataBuff.Assemblage.Find(x => x.info == "auth_" + uID.ToString());
            if (lin == null)
            {
                RAW.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "auth_" + uID.ToString()), new MySQLHelper.Parameter("prop", ((short)auth).ToString()));
            }
            else
                RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "auth_" + uID.ToString()), new MySQLHelper.Parameter("prop", ((short)auth).ToString()));
        }
        /// <summary>
        /// 新用户默认角色
        /// </summary>
        public static AuthLevel UserAuthorityDefault
        {
            get
            {
                var line = DataBuff.FindLineInfo("defaultuserauth");
                if (line == null)
                    return AuthLevel.Subscriber;
                return (AuthLevel)Convert.ToSByte(line.First().info);
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("defaultuserauth");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "defaultuserauth"), new MySQLHelper.Parameter("prop", ((short)value).ToString()));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "defaultuserauth"), new MySQLHelper.Parameter("prop", ((short)value).ToString()));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 用户权限类别
        /// </summary>
        public enum AuthLevel : byte
        {
            /// <summary>
            /// 封禁用户 - 无法登陆
            /// </summary>
            Ban = 0,
            /// <summary>
            /// 默认权限 会根据设置给予权限
            /// </summary>
            Default = 1,
            /// <summary>
            /// 无权限
            /// </summary>
            None = 2,
            /// <summary>
            /// 订阅者 - 可以写评论
            /// </summary>
            Subscriber = 3,
            /// <summary>
            /// 作者 - 写的文章不能自动通过
            /// </summary>
            Author = 5,
            /// <summary>
            /// 信任作者 - 写的文章可以自动通过 (也可以用在论坛模块)
            /// </summary>
            AuthorCertificate = 6,
            /// <summary>
            /// 审核者 - 可以写文章和通过文章
            /// </summary>
            Auditor = 7,
            /// <summary>
            /// 文章管理员 - 可以查看和修改文章
            /// </summary>
            PostManager = 8,
            /// <summary>
            /// 管理员 - 最高权限 可以改任何东西
            /// </summary>
            Admin = 9,

            //9以上的由不同功能 魔改 不做统一
        }
        #endregion

        /// <summary>
        /// 网站标题
        /// </summary>
        public static string WebTitle
        {
            get
            {
                var line = DataBuff.FindLineInfo("webtitle");
                if (line != null)
                    return line.First().Info;
                return "WordWebCMS的网站标题";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("webtitle");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "webtitle"), new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "webtitle"), new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 网站副标题
        /// </summary>
        public static string WebSubTitle
        {
            get
            {
                var line = DataBuff.FindLineInfo("websubtitle");
                if (line != null)
                    return line.First().Info;
                return "这是网站副标题 - 一个由洛里斯编写的网站";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("websubtitle");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "websubtitle"), new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "websubtitle"), new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 网站URL
        /// </summary>
        public static string WebsiteURL
        {
            get
            {
                var line = DataBuff.FindLineInfo("websiteurl");
                if (line != null)
                    return line.First().Info;
                return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority);//HttpContext.Current.Request.Url.Host
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("websiteurl");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "websiteurl"), new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "websiteurl"), new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }

        /// <summary>
        /// 联系方式 用户联系客服等
        /// </summary>
        public static string ContactLink
        {
            get
            {
                var line = DataBuff.FindLineInfo("contactlink");
                if (line != null)
                    return line.First().Info;
                return "<a href=\"/\">联系信息</a>";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("contactlink");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "contactlink"), new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "contactlink"), new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }

        /// <summary>
        /// 允许注册
        /// </summary>
        public static bool AllowRegister
        {
            get
            {
                var line = DataBuff.FindLineInfo("allowregister");
                if (line != null)
                    return Convert.ToBoolean(line.First().InfoToInt);
                return true;//默认允许注册
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("allowregister");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "allowregister"), new MySQLHelper.Parameter("prop", value ? 1 : 0));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "allowregister"), new MySQLHelper.Parameter("prop", value ? 1 : 0));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 新用户默认携带的积分 默认10
        /// </summary>
        public static int NewUserMoney
        {
            get
            {
                var line = DataBuff.FindLineInfo("newusermoney");
                if (line != null)
                    return line.First().InfoToInt;
                return 10;
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("newusermoney");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('newusermoney',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='newusermoney'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 使用 URLRewrite
        /// </summary>
        public static bool EnabledUrlRewrite
        {
            get
            {
                var line = DataBuff.FindLineInfo("enableurlrewrite");
                if (line != null)
                    return Convert.ToBoolean(line.First().InfoToInt);
                return false;//默认未使用URLRewrite
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("enableurlrewrite");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "enableurlrewrite"), new MySQLHelper.Parameter("prop", value ? 1 : 0));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "enableurlrewrite"), new MySQLHelper.Parameter("prop", value ? 1 : 0));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 指示能否运行Setup 是第二重保险
        /// </summary>
        public static bool DisableSetup
        {
            get
            {
                var line = DataBuff.FindLineInfo("disablesetup");
                if (line != null)
                    return Convert.ToBoolean(line.First().InfoToInt);
                return false;
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("disablesetup");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('disablesetup',@prop)", new MySQLHelper.Parameter("prop", value ? 1 : 0));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='disablesetup'", new MySQLHelper.Parameter("prop", value ? 1 : 0));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 指示有无启用邮件功能 需要设置正确的SMTP才能使用
        /// </summary>
        public static bool EnabledEmail
        {
            get
            {
                var line = DataBuff.FindLineInfo("enabledemail");
                if (line != null)
                    return Convert.ToBoolean(line.First().InfoToInt);
                return false;//默认未开启邮件功能
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("enabledemail");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('enabledemail',@prop)", new MySQLHelper.Parameter("prop", value ? 1 : 0));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='enabledemail'", new MySQLHelper.Parameter("prop", value ? 1 : 0));
                DataBuff = null;
            }
        }

        /// <summary>
        /// 邮箱SMTP使用的邮箱名
        /// </summary>
        public static string SMTPEmail
        {
            get
            {
                var line = DataBuff.FindLineInfo("smtpemail");
                if (line != null)
                    return line.First().Info;
                return "no-reply@exlb.net";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("smtpemail");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('smtpemail',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='smtpemail'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }

        /// <summary>
        /// 邮箱SMTP使用的密码
        /// </summary>
        public static string SMTPPassword
        {
            get
            {
                var line = DataBuff.FindLineInfo("smtppassword");
                if (line != null)
                    return line.First().Info;
                return "SMTPPassword";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("smtppassword");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('smtppassword',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='smtppassword'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 邮箱SMTP服务器的链接 一般是 smtp.xxx.com
        /// </summary>
        public static string SMTPURL
        {
            get
            {
                var line = DataBuff.FindLineInfo("smtpurl");
                if (line != null)
                    return line.First().Info;
                return "smtp.exlb.net";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("smtpurl");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('smtpurl',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='smtpurl'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 网站使用的主题 (Themes\..)
        /// </summary>
        public static string Themes
        {
            get
            {
                var line = DataBuff.FindLineInfo("themes");
                if (line != null)
                    return line.First().Info;
                return "default";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("themes");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('themes',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='themes'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 网站图标地址
        /// </summary>
        public static string Icon
        {
            get
            {
                var line = DataBuff.FindLineInfo("icon");
                if (line != null)
                    return line.First().Info;
                return "Picture/WWC.png";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("icon");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('icon',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='themes'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }

        /// <summary>
        /// 评论默认权限 (是否需要审核)
        /// </summary>
        public static Review.ReviewState ReviewDefault
        {
            get
            {
                var line = DataBuff.FindLineInfo("defaultreview");
                if (line == null)
                    return Review.ReviewState.Pending;
                return (Review.ReviewState)Convert.ToSByte(line.First().info);
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("defaultuserauth");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('defaultreview',@prop)", new MySQLHelper.Parameter("prop", ((short)value).ToString()));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='defaultreview'", new MySQLHelper.Parameter("prop", ((short)value).ToString()));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 文章默认权限 (是否需要审核)
        /// </summary>
        public static Posts.PostState PostDefault
        {
            get
            {
                var line = DataBuff.FindLineInfo("defaultpost");
                if (line == null)
                    return Posts.PostState.Pending;
                return (Posts.PostState)Convert.ToSByte(line.First().info);
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("defaultpost");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('defaultpost',@prop)", new MySQLHelper.Parameter("prop", ((short)value).ToString()));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='defaultpost'", new MySQLHelper.Parameter("prop", ((short)value).ToString()));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 网站使用的logo链接 若无 不显示logo
        /// </summary>
        public static string TopLogo
        {
            get
            {
                var line = DataBuff.FindLineInfo("toplogo");
                if (line != null)
                    return line.First().Info;
                return "";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("toplogo");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('toplogo',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='toplogo'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }

        /// <summary>
        /// 获得主界面的Menu列表 格式为 Name:名称 Info:URL
        /// </summary>
        public static List<Sub> MenuList
        {
            get
            {
                var line = DataBuff.FindLineInfo("menulist");
                if (line != null)
                    return new Line(line.First().Info).Subs;
                return new List<Sub>();
            }
            set
            {
                Line data = new Line("menu", "", "", value.ToArray());
                Line lin = DataBuff.FindLineInfo("menulist");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('menulist',@prop)", new MySQLHelper.Parameter("prop", data.ToString()));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='menulist'", new MySQLHelper.Parameter("prop", data.ToString()));
                DataBuff = null;
            }
        }

        /// <summary>
        /// 用户自定义界面内容1(HTML)
        /// </summary>
        public static string SideBar1
        {
            get
            {
                var line = DataBuff.FindLineInfo("sidebar1");
                if (line != null)
                    return line.First().Info;
                return "";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("sidebar1");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('sidebar1',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='sidebar1'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 用户自定义界面内容2(HTML)
        /// </summary>
        public static string SideBar2
        {
            get
            {
                var line = DataBuff.FindLineInfo("sidebar2");
                if (line != null)
                    return line.First().Info;
                return "";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("sidebar2");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('sidebar2',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='sidebar2'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 用户自定义界面内容3(HTML)
        /// </summary>
        public static string SideBar3
        {
            get
            {
                var line = DataBuff.FindLineInfo("sidebar3");
                if (line != null)
                    return line.First().Info;
                return "";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("sidebar3");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('sidebar3',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='sidebar3'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 网页信息
        /// </summary>
        public static string WebInfo
        {
            get
            {
                var line = DataBuff.FindLineInfo("webinfo");
                if (line != null)
                    return line.First().Info;
                return $"Copyright {DateTime.Now.Year} , {WebTitle} , Power by <a href=\"https://github.com/LorisYounger/WordWebCMS\">WordWebCMS</a>";
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("webinfo");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('webinfo',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='webinfo'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 网站使用的默认主页 -1为主页 其他会自动跳转post
        /// </summary>
        public static int NomalIndex
        {
            get
            {
                var line = DataBuff.FindLineInfo("nomalindex");
                if (line != null)
                    return line.First().InfoToInt;
                return -1;
            }
            set
            {
                Line lin = DataBuff.FindLineInfo("nomalindex");
                if (lin == null)
                {
                    RAW.ExecuteNonQuery($"INSERT INTO setting VALUES ('nomalindex',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='nomalindex'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }



        /// <summary>
        /// 为IP提供等级警告封禁
        /// </summary>
        /// <param name="ip">封禁IP</param>
        /// <param name="level">封禁等级</param>
        public static void BanIP(string ip, int level)
        {
            if (Application["BAN" + ip] == null)
            {
                Application["BAN" + ip] = level;
            }
            else
            {
                Application["BAN" + ip] = (int)Application["BAN" + ip] + level;
            }
        }
        /// <summary>
        /// 判断该ip是否能够通过验证
        /// </summary>
        /// <param name="ip">IP</param>
        /// <param name="maxlevel">允许最多的封禁等级</param>
        /// <returns></returns>
        public static bool BanIPCheck(string ip, int maxlevel = 10)
        {
            return Application["BAN" + ip] != null && (int)Application["BAN" + ip] > maxlevel;
            //TODO:永久性的黑名单,使用数据库
            //TODO:储存错误尝试到数据库,给后台看
        }
    }
}