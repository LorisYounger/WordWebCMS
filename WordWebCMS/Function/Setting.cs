using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http.Headers;
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
            ReTry: try
                {
                    return RAW.ExecuteQuery("SELECT * FROM setting");
                }
                catch
                {
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
        }//set => RAWUser.ExecuteNonQuery($"UPDATE user SET auth=@auth WHERE Uid=@uid", new MySQLHelper.Parameter("auth", (byte)value), new MySQLHelper.Parameter("uid", uID));
        /// <summary>
        /// 获得用户的权限辨识
        /// </summary>
        public static void SetUserAuthority(int uID, AuthLevel auth)
        {
            Line lin = DataBuff.Assemblage.Find(x => x.info == "auth_" + uID.ToString());
            if (lin == null)
            {
                RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "auth_" + uID.ToString()), new MySQLHelper.Parameter("prop", ((short)auth).ToString()));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "defaultuserauth");
                if (line == null)
                    return AuthLevel.Default;
                return (AuthLevel)Convert.ToSByte(line.First().info);
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "defaultuserauth");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "defaultuserauth"), new MySQLHelper.Parameter("prop", ((short)value).ToString()));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "webtitle");
                if (line != null)
                    return line.First().Info;
                return "WordWebCMS的网站标题";
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "webtitle");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "webtitle"), new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "websubtitle");
                if (line != null)
                    return line.First().Info;
                return "这是网站副标题 - 一个由洛里斯编写的网站";
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "websubtitle");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "websubtitle"), new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "websiteurl");
                if (line != null)
                    return line.First().Info;
                return HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority);//HttpContext.Current.Request.Url.Host
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "websideurl");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "websiteurl"), new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "websiteurl"), new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
        /// <summary>
        /// 允许注册
        /// </summary>
        public static bool AlowRegister
        {
            get
            {
                var line = DataBuff.Assemblage.Find(x => x.info == "alowregister");
                if (line != null)
                    return Convert.ToBoolean(line.First().info);
                return false;//HttpContext.Current.Request.Url.Host
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "alowregister");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES (@sele,@prop)", new MySQLHelper.Parameter("sele", "alowregister"), new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector=@sele", new MySQLHelper.Parameter("sele", "alowregister"), new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "themes");
                if (line != null)
                    return line.First().Info;
                return "default";
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "themes");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('themes',@prop)", new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "defaultreview");
                if (line == null)
                    return Review.ReviewState.Pending;
                return (Review.ReviewState)Convert.ToSByte(line.First().info);
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "defaultuserauth");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('defaultreview',@prop)", new MySQLHelper.Parameter("prop", ((short)value).ToString()));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "defaultpost");
                if (line == null)
                    return Posts.PostState.Pending;
                return (Posts.PostState)Convert.ToSByte(line.First().info);
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "defaultpost");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('defaultpost',@prop)", new MySQLHelper.Parameter("prop", ((short)value).ToString()));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "toplogo");
                if (line != null)
                    return line.First().Info;
                return "";
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "toplogo");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('toplogo',@prop)", new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "menulist");
                if (line != null)
                    return new Line(line.First().Info).Subs;
                return new List<Sub>();
            }
            set
            {
                Line data = new Line("menu", "", "", value.ToArray());
                Line lin = DataBuff.Assemblage.Find(x => x.info == "menulist");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('menulist',@prop)", new MySQLHelper.Parameter("prop", data.ToString()));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "sidebar1");
                if (line != null)
                    return line.First().Info;
                return "";
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "sidebar1");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('sidebar1',@prop)", new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "sidebar2");
                if (line != null)
                    return line.First().Info;
                return "";
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "sidebar2");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('sidebar2',@prop)", new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "sidebar3");
                if (line != null)
                    return line.First().Info;
                return "";
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "sidebar3");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('sidebar3',@prop)", new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "webinfo");
                if (line != null)
                    return line.First().Info;
                return $"Copyright {DateTime.Now.Year} , {WebTitle} , Power by WordWebCMS";
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "webinfo");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('webinfo',@prop)", new MySQLHelper.Parameter("prop", value));
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
                var line = DataBuff.Assemblage.Find(x => x.info == "nomalindex");
                if (line != null)
                    return line.First().InfoToInt;
                return -1;
            }
            set
            {
                Line lin = DataBuff.Assemblage.Find(x => x.info == "nomalindex");
                if (lin == null)
                {
                    RAWUser.ExecuteNonQuery($"INSERT INTO setting VALUES ('nomalindex',@prop)", new MySQLHelper.Parameter("prop", value));
                }
                else
                    RAW.ExecuteNonQuery($"UPDATE setting SET property=@prop WHERE selector='nomalindex'", new MySQLHelper.Parameter("prop", value));
                DataBuff = null;
            }
        }
    }
}