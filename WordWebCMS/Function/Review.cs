using System;
using System.Collections.Generic;
using System.Text;
using LinePutScript;
using LinePutScript.SQLHelper;
using static WordWebCMS.Conn;
using System.Linq;
namespace WordWebCMS
{
    //评论表格式:
    //   -review 
    //     Rid(int)|Pid(int)|content(text)|author(int)|postdate(date)|modifydate(date)|state(byte)|   anzhtml  |likes(int)|
    //     评论id  | 评论id |     内容     |   作者id  |   发布日期    |    修改日期    |  评论类型 |是否分析html |  赞同数  |
    //     主键递增|   -------------------------------------------------------------- |   0-默认  |    false    |    0     |

    public class Review
    {
        #region "辅助构建函数"
        /// <summary>
        /// 通过评论id获得评论
        /// </summary>
        /// <param name="ReviewID">评论id</param>
        /// <returns></returns>
        public static Review GetReviewByID(int ReviewID)
        {
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM review WHERE Rid=@rid", new MySQLHelper.Parameter("rid", ReviewID));
            if (data.Assemblage.Count == 0)
                return null;
            return new Review(ReviewID, data.First());
        }
        /// <summary>
        /// 通过文章id获得评论 带排序
        /// </summary>
        /// <param name="PostID">文章id</param>
        /// <returns>文章列表</returns>
        public static List<Review> GetReviewByPostID(int PostID)
        {
            List<Review> Reviews = new List<Review>();
            //获得超级置顶评论
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM review WHERE Pid=@pid AND state=9 ORDER BY Rid", new MySQLHelper.Parameter("pid", PostID));
            foreach (Line line in data)
            {
                Reviews.Add(new Review(line.InfoToInt, line));
            }
            //获得置顶评论
            data = RAW.ExecuteQuery("SELECT * FROM review WHERE Pid=@pid AND state=7 ORDER BY Rid", new MySQLHelper.Parameter("pid", PostID));
            foreach (Line line in data)
            {
                Reviews.Add(new Review(line.InfoToInt, line));
            }
            //获得普通的已发布评论
            data = RAW.ExecuteQuery($"SELECT * FROM review WHERE Pid=@pid AND (state=5{(Setting.ReviewDefault == ReviewState.Published ? " OR state=1" : "")}) ORDER BY Rid", new MySQLHelper.Parameter("pid", PostID));
            foreach (Line line in data)
            {
                Reviews.Add(new Review(line.InfoToInt, line));
            }
            //获得置底评论
            data = RAW.ExecuteQuery("SELECT * FROM review WHERE Pid=@pid AND state=8 ORDER BY Rid", new MySQLHelper.Parameter("pid", PostID));
            foreach (Line line in data)
            {
                Reviews.Add(new Review(line.InfoToInt, line));
            }
            return Reviews;
        }


        /// <summary>
        /// 通过搜索相似评论内容获得评论
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns>评论列表</returns>
        public static List<Review> GetReviewLikeName(string key)
        {
            List<Review> Reviews = new List<Review>();
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM review WHERE content LIKE @pid ORDER BY Rid DESC", new MySQLHelper.Parameter("pid", '%' + key + '%'));
            foreach (Line line in data)
            {
                Reviews.Add(new Review(line.InfoToInt, line));
            }
            return Reviews;
        }

        /// <summary>
        /// 获得所有可以展示出来的评论 (可被一般用户看到)
        /// </summary>
        /// <returns>评论列表 按发布顺序排序</returns>
        public static List<Review> GetAllAvailableReview()
        {
            List<Review> Reviews = new List<Review>();
            //获得评论
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM review WHERE state=9 OR state=7 OR state=5 OR state=8 ORDER BY Rid DESC");
            foreach (Line line in data)
            {
                Reviews.Add(new Review(line.InfoToInt, line));
            }
            return Reviews;
        }
        /// <summary>
        /// 获得所有评论(一般用于后台)
        /// </summary>
        /// <returns>文章列表</returns>
        public static List<Review> GetAllReview()
        {
            List<Review> Reviews = new List<Review>();
            //获得所有文章
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM review ORDER BY Rid DESC");
            foreach (Line line in data)
            {
                Reviews.Add(new Review(line.InfoToInt, line));
            }
            return Reviews;
        }
        /// <summary>
        /// 通过作者id称获得评论
        /// </summary>
        /// <param name="Authorid">作者id</param>
        /// <returns>文章列表</returns>
        public static List<Review> GetReviewByAuthor(int Authorid)
        {
            List<Review> Reviews = new List<Review>();
            LpsDocument data = RAW.ExecuteQuery("SELECT * FROM post WHERE author=@pid ORDER BY Rid DESC", new MySQLHelper.Parameter("pid", Authorid));
            foreach (Line line in data)
            {
                Reviews.Add(new Review(line.InfoToInt, line));
            }
            return Reviews;
        }
        /// <summary>
        /// 创建新评论
        /// </summary>
        public static Review CreatReview(int pid, string cont, int authid, DateTime postdate, DateTime modifydate, ReviewState state = ReviewState.Default, bool anzhtml = false)
        {
            RAW.ExecuteNonQuery($"INSERT INTO review VALUES (NULL,@pid,@content,@author,@postdate,@modifydate,@state,@anzhtml,0)",
                new MySQLHelper.Parameter("pid", pid), new MySQLHelper.Parameter("content", cont), new MySQLHelper.Parameter("author", authid),
                new MySQLHelper.Parameter("postdate", postdate), new MySQLHelper.Parameter("modifydate", modifydate),
                new MySQLHelper.Parameter("state", (int)state), new MySQLHelper.Parameter("anzhtml", anzhtml));
            return new Review(RAW.ExecuteQuery("SELECT MAX(Rid) FROM review").First().InfoToInt);//如果这个没有生效就使用 SELECT MAX(ID) FROM post
        }
        #endregion

        #region 数据库Data

        /// <summary>
        /// 从数据库获得元数据
        /// </summary>
        public Line Data
        {
            get => RAW.ExecuteQuery("SELECT * FROM review WHERE Rid=@rid", new MySQLHelper.Parameter("rid", rID)).First();
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
        /// Reviewid
        /// </summary>
        public readonly int rID;
        /// <summary>
        /// Postid
        /// </summary>
        public int pID
        {
            get
            {
                return DataBuff.Find("Pid").InfoToInt;
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE review SET Pid=@pid WHERE Rid=@rid", new MySQLHelper.Parameter("pid", value), new MySQLHelper.Parameter("rid", rID));
            }
        }
        /// <summary>
        /// 评论作者ID
        /// </summary>
        public int AuthorID
        {
            get => DataBuff.Find("author").InfoToInt;
            set
            {
                databf = null;
                author = null;
                RAW.ExecuteNonQuery($"UPDATE review SET author=@auth WHERE Rid=@rid", new MySQLHelper.Parameter("auth", value), new MySQLHelper.Parameter("rid", rID));
            }
        }
        /// <summary>
        /// 评论作者
        /// </summary>
        public Users Author
        {
            get
            {
                if (author == null)
                {
                    author = Users.GetUser(AuthorID);
                    if (author == null)
                        author = Users.GetRemovedUser(AuthorID);
                }
                return author;
            }
            set
            {
                databf = null;
                author = null;
                RAW.ExecuteNonQuery($"UPDATE review SET author=@auth WHERE Rid=@rid", new MySQLHelper.Parameter("auth", value.uID), new MySQLHelper.Parameter("rid", rID));
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
                RAW.ExecuteNonQuery($"UPDATE review SET postdate=@date WHERE Rid=@rid", new MySQLHelper.Parameter("date", value), new MySQLHelper.Parameter("rid", rID));
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
                RAW.ExecuteNonQuery($"UPDATE review SET modifydate=@date WHERE Rid=@rid", new MySQLHelper.Parameter("date", value), new MySQLHelper.Parameter("rid", rID));
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
                RAW.ExecuteNonQuery($"UPDATE review SET likes=@rds WHERE Rid=@rid", new MySQLHelper.Parameter("rds", value), new MySQLHelper.Parameter("rid", rID));
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
                RAW.ExecuteNonQuery($"UPDATE review SET anzhtml=@cont WHERE Rid=@rid", new MySQLHelper.Parameter("cont", value), new MySQLHelper.Parameter("rid", rID));
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
                RAW.ExecuteNonQuery($"UPDATE review SET content=@cont WHERE Rid=@rid", new MySQLHelper.Parameter("cont", value), new MySQLHelper.Parameter("rid", rID));
            }
        }
        /// <summary>
        /// 将文章内容转换成html 经过降级处理
        /// </summary>
        /// <returns>转换成的HTML</returns>
        public string ContentToHtml()
        {
            string[] cont = Content.Split(new char[] { '\n' }, 2);
            Line first = new Line(cont[0]);
            if (first.Name == "wwcms")
            {
                string output = "";
                Sub reply = first.Find("reply");
                if (reply != null)//引用了评论
                {
                    int rid = reply.InfoToInt;
                    if (rid >= rID)
                    {
                        output += $"<blockquote><span class=\"error\">引用评论不能为将来的评论 Rid:{reply.info}>={rID}</span></blockquote>";
                    }
                    else
                    {
                        Review rv = GetReviewByID(rid);
                        if (rv == null)
                            output += $"<blockquote><span class=\"error\">引用评论为空 Rid:{reply.info}</span></blockquote>";
                        else
                        {
                            var state = rv.State;
                        switchstate:
                            switch (state)
                            {
                                case ReviewState.Delete:
                                    output += $"<blockquote><b>{rv.Author.UserName}</b><br /><span class=\"error\">评论已被删除</span></blockquote>";
                                    break;
                                case ReviewState.Default:
                                    if (Setting.ReviewDefault == ReviewState.Default)
                                        goto case default;
                                    state = Setting.ReviewDefault;
                                    goto switchstate;
                                case ReviewState.None:
                                case ReviewState.Pending:
                                    output += $"<blockquote><b>{rv.Author.UserName}</b><br /><span class=\"error\">评论等待审核</span></blockquote>";
                                    break;
                                default:
                                    output += $"<blockquote><b>{rv.Author.UserName}</b><br />{rv.ContentToHtml()}</blockquote>";
                                    break;
                            }
                        }
                    }
                }
                if (cont.Length == 2)
                {
                    output += Function.TitleDownGrade(Function.MarkdownParse(cont[1], false, AnalyzeHtml));
                }
                return output;
            }
            else
                return Function.TitleDownGrade(Function.MarkdownParse(Content, false, AnalyzeHtml));
        }

        /// <summary>
        /// 文章类型
        /// </summary>
        public ReviewState State
        {
            get
            {
                return (ReviewState)Convert.ToSByte(DataBuff.Find("state").info);
            }
            set
            {
                databf = null;
                RAW.ExecuteNonQuery($"UPDATE review SET state=@state WHERE Rid=@rid", new MySQLHelper.Parameter("state", ((short)value).ToString()), new MySQLHelper.Parameter("rid", rID));
            }
        }
        public enum ReviewState : byte
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
            /// 置顶评论 - 评论放在顶端
            /// </summary>
            Topping = 7,
            /// <summary>
            /// 置底评论 - 评论放在底端
            /// </summary>
            Bottoming = 8,
            /// <summary>
            /// 超级置顶 - 评论放在最顶端
            /// </summary>
            ToppingSupper = 9,

            //9以上的由不同功能 魔改 不做统一
        }


        #region 构造函数
        public Review(int ReviewID)
        {
            rID = ReviewID;
        }
        public Review(int ReviewID, Line bf)
        {
            rID = ReviewID;
            databf = bf;
        }
        #endregion

        #region 转换成HTML
        public string ToPostReview(Users usr = null) => $"<li id=\"comment-{rID}\"><article id=\"div-comment-{rID}\" class=\"comment-body\"><footer class=\"comment-meta\">" +
                $"<div class=\"comment-author vcard\"><img src=\"{Author.AvatarURL}\" class=\"avatar avatar-60 photo\" height=\"60\" width=\"60\">" +
                $"<a href=\"{Setting.WebsiteURL}/User.aspx?ID={AuthorID}\"><b class=\"fn\">{Author.UserName}</b></a><span class=\"says\">评论道：</span></div>" +
                $"<div class=\"comment-metadata\"><a href=\"#comment-{rID}\">{Modifydate.ToShortDateString()} {Modifydate.ToShortTimeString()}</a></div></footer>" +
                $"<div class=\"comment-content\">{ContentToHtml()}</div><div class=\"reply\"><button type=\"button\" onclick=\"Reply('{rID}')\"/>回复</button>" +
                $"<div class=\"nav-next\" id=\"reviewlike{rID}\">{Likes}个赞<button ID=\"Like\" type=\"button\" onclick=\"LikeReview({rID})\" class=\"like-review\" style=\"" +
                $"{(usr == null || (Setting.Application[$"Liker{rID}u{usr.uID}"] == null) ? "background:url(Picture/like.png);" : "background:url(Picture/likeup.png);")}background-size:cover;\" /></div></div></article></li>";
        #endregion
    }
}