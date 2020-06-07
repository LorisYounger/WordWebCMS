using System;
using System.Collections.Generic;
using LinePutScript;
using LinePutScript.SQLHelper;
using static WordWebCMS.Conn;
using Westwind.Web.Markdown;

namespace WordWebCMS
{//TODO:摘录要清空html
    //文章表格式:select LAST_INSERT_ID()  gifts
    //   -post 
    //     Pid(int)|name(string100)|content(midtext)|author(int)|excerpt(string400)|postdate(date)|modifydate(date)|classify(string200)|state(byte)|attachment(tinytext)|password(string32) |   anzhtml  |allowcomments|readers(int)|likes(int)|
    //     文章id  |    文章名     |      内容      |   作者id  |   摘录/简介      |   发布日期   |    修改日期    |     分类目录      |  文章类型 |         附图       |      密码md5s     |是否分析html|   允许评论  |   阅读量   |  赞同数  |
    //     主键递增|   --------------------------------------------------------------------------------------------------------------- |   0-默认  |   空-无图片 or url |   空-不需要密码   |    false   |    true     |    0       |   0      |
    public class Posts
    {
        #region "辅助构建函数"
        /// <summary>
        /// 通过文章id获得文章
        /// </summary>
        /// <param name="PostID">文章id</param>
        /// <returns></returns>
        public static Posts GetPost(int PostID)
        {
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE Pid=@pid", new MySQLHelper.Parameter("pid", PostID));
            if (data.Assemblage.Count == 0)
                return null;
            return new Posts(PostID, data.First());
        }
        //Todo: 通过  发布/修改日期(天)(月)
        /// <summary>
        /// 通过文章名称获得文章
        /// </summary>
        /// <param name="Name">文章名</param>
        /// <returns>文章列表</returns>
        public static List<Posts> GetPostFormName(string Name)
        {
            List<Posts> Posts = new List<Posts>();
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE name=@pid ORDER BY modifydate DESC", new MySQLHelper.Parameter("pid", Name));
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            return Posts;
        }
        /// <summary>
        /// 通过分类目录获得文章
        /// </summary>
        /// <param name="classify">分类目录名称</param>
        /// <returns>文章列表</returns>
        public static List<Posts> GetPostFormClassify(string classify)
        {
            List<Posts> Posts = new List<Posts>();
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE classify=@pid ORDER BY modifydate DESC", new MySQLHelper.Parameter("pid", classify));
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            return Posts;
        }
        /// <summary>
        /// 通过分类目录顶级获得文章
        /// eg: 关于我-获得荣誉-国家级  获得(关于我-)下面的所有文章
        /// </summary>
        /// <param name="classify">分类目录顶级名称</param>
        /// <returns>文章列表</returns>
        public static List<Posts> GetPostFormTopClassify(string classify)
        {
            List<Posts> Posts = new List<Posts>();
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE classify LIKE @pid ORDER BY modifydate DESC", new MySQLHelper.Parameter("pid", classify + '%'));
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            return Posts;
        }
        /// <summary>
        /// 通过搜索相似文章名称获得文章
        /// </summary>
        /// <param name="key">关键字 要求%作为通配符</param>
        /// <returns>文章列表</returns>
        public static List<Posts> GetPostLikeName(string key)
        {
            List<Posts> Posts = new List<Posts>();
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE name LIKE @pid ORDER BY modifydate DESC", new MySQLHelper.Parameter("pid", key));
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }

            return Posts;
        }
        /// <summary>
        /// 获得所有可以展示出来的文章 (可被一般用户看到)
        /// </summary>
        /// <returns>文章列表 按发布顺序和优先级排序</returns>
        public static List<Posts> GetAllAvailablePost()
        {
            List<Posts> Posts = new List<Posts>();
            //获得超级置顶文章
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE state=9 ORDER BY modifydate DESC");
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            //获得置顶文章
            data = RAW.ExecuteQuery("SELECT * FROM post WHERE state=7 ORDER BY modifydate DESC");
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            //获得普通的已发布文章
            data = RAW.ExecuteQuery("SELECT * FROM post WHERE state=5 ORDER BY modifydate DESC");
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            //获得置底文章
            data = RAW.ExecuteQuery("SELECT * FROM post WHERE state=8 ORDER BY modifydate DESC");
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            return Posts;
        }
        /// <summary>
        /// 获得所有文章(一般用于后台)
        /// </summary>
        /// <returns>文章列表 按发布顺序和优先级排序</returns>
        public static List<Posts> GetAllPost()
        {
            List<Posts> Posts = new List<Posts>();
            //获得所有文章
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post ORDER BY modifydate DESC");
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            return Posts;
        }
        /// <summary>
        /// 通过搜索相似文章内容获得文章
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns>文章列表</returns>
        public static List<Posts> GetPostLikeContent(string key)
        {
            List<Posts> Posts = new List<Posts>();
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE content LIKE @pid ORDER BY modifydate DESC", new MySQLHelper.Parameter("pid", '%' + key + "%"));
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            return Posts;
        }
        /// <summary>
        /// 通过作者id称获得文章
        /// </summary>
        /// <param name="Authorid">作者id</param>
        /// <returns>文章列表</returns>
        public static List<Posts> GetPostByAuthor(int Authorid)
        {
            List<Posts> Posts = new List<Posts>();
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE author=@pid ORDER BY modifydate DESC", new MySQLHelper.Parameter("pid", Authorid));
            foreach (Line line in data)
            {
                Posts.Add(new Posts(line.InfoToInt, line));
            }
            return Posts;
        }
        /// <summary>
        /// 创建新文章
        /// </summary>
        public static Posts CreatPost(string name, string cont, int authid, string excerpt, DateTime postdate, DateTime modifydate, string classify, PostState state = PostState.Default, string password = "", string attachment = "", bool anzhtml = false)
        {
            RAW.ExecuteNonQuery($"INSERT INTO post VALUES (NULL,@name,@content,@author,@excerpt,@postdate,@modifydate,@class,@state,@attach,@password,@anzhtml)",
                new MySQLHelper.Parameter("name", name), new MySQLHelper.Parameter("content", cont), new MySQLHelper.Parameter("author", authid),
                new MySQLHelper.Parameter("excerpt", excerpt), new MySQLHelper.Parameter("postdate", postdate), new MySQLHelper.Parameter("modifydate", modifydate),
                new MySQLHelper.Parameter("class", classify), new MySQLHelper.Parameter("state", ((int)state).ToString()), new MySQLHelper.Parameter("attach", attachment),
                new MySQLHelper.Parameter("password", (password == "" ? "" : Function.MD5salt(password))), new MySQLHelper.Parameter("anzhtml", anzhtml));
            return new Posts(RAW.ExecuteQuery("select LAST_INSERT_ID()").First().InfoToInt);//如果这个没有生效就使用 SELECT MAX(ID) FROM post
        }
        #endregion

        #region 数据库Data

        /// <summary>
        /// 从数据库获得元数据
        /// </summary>
        public Line Data
        {
            get => RAW.ExecuteQuery("SELECT * FROM post WHERE Pid=@pid", new MySQLHelper.Parameter("pid", pID)).First();
            //之所以没有个 data进行缓存是 post数据蛮重要的 不能缓存 也不需要 或许以后的文章数据可以加上缓存
        }
        /// <summary>
        /// 从缓存中获取的元数据
        /// </summary>
        public Line DataBuff //用于读取用 如果要写啥 禁止使用这个 需手动获取最新数据
        {
            get
            {
                if (databf == null)
                    databf = Data;
                return databf;
            }
            //没有set是因为这是操作整个行 太费了
        }
        private Line databf;//用于读取用 如果要写啥 禁止使用这个 需手动获取最新数据
        #endregion


        /// <summary>
        /// Postid
        /// </summary>
        public readonly int pID;
        /// <summary>
        /// 文章作者ID
        /// </summary>
        public int AuthorID
        {
            get => DataBuff.Find("author").InfoToInt;
            set
            {
                databf = null;
                author = null;
                RAW.ExecuteNonQuery($"UPDATE post SET author=@auth WHERE Pid=@pid", new MySQLHelper.Parameter("auth", value), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 文章作者
        /// </summary>
        public Users Author
        {
            get
            {
                if (author == null)
                    author = Users.GetUser(DataBuff.Find("author").InfoToInt);
                return author;
            }
            set
            {
                databf = null;
                author = null;
                RAW.ExecuteNonQuery($"UPDATE post SET author=@auth WHERE Pid=@pid", new MySQLHelper.Parameter("auth", value.uID), new MySQLHelper.Parameter("pid", pID));
            }
        }
        private Users author;
        /// <summary>
        /// 发布日期
        /// </summary>
        public DateTime PostDate
        {
            get
            {
                return Convert.ToDateTime(DataBuff.Find("postdate").Info);
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET postdate=@date WHERE Pid=@pid", new MySQLHelper.Parameter("date", value), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 修改日期
        /// </summary>
        public DateTime Modifydate
        {
            get
            {
                return Convert.ToDateTime(DataBuff.Find("modifydate").Info);
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET modifydate=@date WHERE Pid=@pid", new MySQLHelper.Parameter("date", value), new MySQLHelper.Parameter("pid", pID));
            }
        }

        /// <summary>
        /// 阅读量
        /// </summary>
        public int Readers
        {
            get
            {
                return DataBuff.Find("readers").InfoToInt;
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET readers=@rds WHERE Pid=@pid", new MySQLHelper.Parameter("rds", value), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 点赞赞同数量
        /// </summary>
        public int Likes
        {
            get
            {
                return DataBuff.Find("likes").InfoToInt;
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET likes=@rds WHERE Pid=@pid", new MySQLHelper.Parameter("rds", value), new MySQLHelper.Parameter("pid", pID));
            }
        }

        /// <summary>
        /// 一个值指示 这篇文章内含的html是否能被解析
        /// 默认为false 慎用true
        /// 仅限管理员=9能修改此值
        /// </summary>
        public bool AnalyzeHtml
        {
            get
            {
                return Convert.ToBoolean(DataBuff.Find("anzhtml").info);
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET anzhtml=@cont WHERE Pid=@pid", new MySQLHelper.Parameter("cont", value), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 指示这篇文章是否允许评论
        /// </summary>
        public bool AllowComments
        {
            get
            {
                return DataBuff.Find("allowcomments").info == "1";
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET allowcomments=@cont WHERE Pid=@pid", new MySQLHelper.Parameter("cont", value), new MySQLHelper.Parameter("pid", pID));
            }
        }

        /// <summary>
        /// 文章名称
        /// </summary>
        public string Name
        {
            get
            {
                return DataBuff.Find("name").Info;
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET name=@name WHERE Pid=@pid", new MySQLHelper.Parameter("name", Function.SanitizeHtml(value)), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 内容
        /// </summary>
        public string Content
        {
            get
            {
                return DataBuff.Find("content").Info;
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET content=@cont WHERE Pid=@pid", new MySQLHelper.Parameter("cont", value), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 简介/摘要
        /// </summary>
        public string Excerpt
        {
            get
            {
                return DataBuff.Find("excerpt").Info;
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET excerpt=@cont WHERE Pid=@pid", new MySQLHelper.Parameter("cont", Function.SanitizeHtml(value)), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 分类目录
        /// </summary>
        public string Classify
        {
            get
            {
                return DataBuff.Find("classify").Info;
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET classify=@cont WHERE Pid=@pid", new MySQLHelper.Parameter("cont", value), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 附图
        /// </summary>
        public string Attachment
        {
            get
            {
                return DataBuff.Find("attachment").Info;
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET attachment=@cont WHERE Pid=@pid", new MySQLHelper.Parameter("cont", value), new MySQLHelper.Parameter("pid", pID));
            }
        }
        /// <summary>
        /// 将文章内容转换成html
        /// </summary>
        /// <returns>转换成的HTML</returns>
        public string ContentToHtml() => Markdown.Parse(Content, false, false, !AnalyzeHtml);
        /// <summary>
        /// 将文章内容转换成html 附带行号
        /// </summary>
        /// <returns>转换成的HTML</returns>
        public string ContentToHtmlLine() => Markdown.Parse(Content, true, false, !AnalyzeHtml);
        /// <summary>
        /// 将文章内容转换成Index
        /// </summary>
        /// <returns>转换成的HTML</returns>
        public string ContentToIndex() => Function.GenerateIndex(ContentToHtml());

        /// <summary>
        /// 密码确认 确认这个密码是否符合
        /// </summary>
        /// <param name="pw">要确认的密码</param>
        /// <returns></returns>
        public bool PasswordCheck(string pw)
        {
            string md5pw = Data.Find("password").info;
            if (md5pw == "")
                return true;
            return Function.MD5salt(pw) == md5pw;
        }
        /// <summary>
        /// 设置新密码
        /// </summary>
        /// <param name="pw">新密码</param>
        public void PasswordSet(string pw)
        {
            if (pw == "")
                RAW.ExecuteNonQuery($"UPDATE post SET password=@pw WHERE Pid=@pid", new MySQLHelper.Parameter("pw", ""), new MySQLHelper.Parameter("pid", pID));
            else
                RAW.ExecuteNonQuery($"UPDATE post SET password=@pw WHERE Pid=@pid", new MySQLHelper.Parameter("pw", Function.MD5salt(pw)), new MySQLHelper.Parameter("pid", pID));
        }


        /// <summary>
        /// 文章类型
        /// </summary>
        public PostState State
        {
            get
            {
                return (PostState)Convert.ToSByte(DataBuff.Find("state").info);
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE post SET state=@state WHERE Pid=@pid", new MySQLHelper.Parameter("state", ((short)value).ToString()), new MySQLHelper.Parameter("pid", pID));
            }
        }
        public enum PostState : byte
        {
            /// <summary>
            /// 封禁/删除 不会被显示
            /// </summary>
            Delete = 0,
            /// <summary>
            /// 默认类型
            /// </summary>
            Default = 1,
            /// <summary>
            /// 无 无处置
            /// </summary>
            None = 2,
            /// <summary>
            /// 等待审核 - 等待审核通过
            /// </summary>
            Pending = 3,
            /// <summary>
            /// 已发布 - 文章已发布
            /// </summary>
            Published = 5,
            /// <summary>
            /// 置顶文章 - 文章放在顶端
            /// </summary>
            Topping = 7,
            /// <summary>
            /// 置底文章 - 文章放在底端
            /// </summary>
            Bottoming = 8,
            /// <summary>
            /// 超级置顶 - 文章放在最顶端
            /// </summary>
            ToppingSupper = 9,

            //9以上的由不同功能 魔改 不做统一
        }


        #region 构造函数
        public Posts(int postID)
        {
            pID = postID;
        }
        public Posts(int postID, Line bf)
        {
            pID = postID;
            databf = bf;
        }
        #endregion

        #region 转换成HTML
        public string ToIndex()
            => $"<article id=\"post-{pID}\" class=\"post-{pID} post hentry State-{State}\">	<header class=\"entry-header\"> <h1 class=\"entry-title\">" +
            $"<a href=\"{Setting.WebsiteURL}/Post.aspx?ID={pID}\">{Name}</a></h1><div class=\"entry-meta\">" +
            $"<span class=\"posted-on\">时间:<a href=\"{Setting.WebsiteURL}/Index.aspx?Date={PostDate.ToShortDateString()}\">{PostDate.ToShortDateString()}" +
            $"</a> </span><span class=\"poster-author\" id=user-{Author}> <span class=\"author vcard\"> 作者:<a href=\"{Setting.WebsiteURL}/User.aspx?ID={AuthorID}\">{Author.UserName}</a></span></span>" +
           (AllowComments ? $"<span class=\"comments-link\"><a href=\"{Setting.WebsiteURL}/Post.aspx?ID={pID}#respond\">发表回复</a></span>" : "") +
            $"</div></header><div class=\"entry-content\">{(Attachment == "" ? "" : $"<img width=\"150\" height=\"150\" src=\"{Attachment}\" class=\"wp-post-image\">")}<p>{Excerpt.Replace("\n", "<br />")}</p></div></article>";

        public string ToPost()
        {
            Readers += 1;
            return $"<article class=\"post hentry post-{pID} {State}\"><header class=\"entry-header\"><h1 class=\"entry-title\">" +
                   $"{Name}</h1><div class=\"entry-meta\"><span class=\"posted-on\">在 <a href=\"{Setting.WebsiteURL}/Index.aspx?Date={PostDate.ToShortDateString()}\" rel=\"bookmark\">" +
                   $"{PostDate.ToShortDateString()}</a> 上张贴</span><span class=\"poster-author\" id=user-{Author}> 由 <span class=\"author vcard\">" +
                   $"<a href=\"{Setting.WebsiteURL}/User.aspx?ID={AuthorID}\"><img src={Author.HeadPortraitURL} width=\"20\" height=\"20\">{Author.UserName}</a></span></span>" +
                  (AllowComments ? $"<span class=\"comments-link\"><a href=\"#respond\">发表回复</a></span>" : "") +
                   $"</div></header><div class=\"entry-content\">{ContentToHtml()}</div></article><footer class=\"entry-footer\"><span class=\"cat-links\">张贴在" +
               $"<a href=\"{Setting.WebsiteURL}/Index.aspx?class={Classify}\" rel=\"category tag\">{Classify}</a></span></footer>";
        }

        #endregion
    }
}