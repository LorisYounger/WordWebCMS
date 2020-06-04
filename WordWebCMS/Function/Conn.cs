using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using LinePutScript;
using LinePutScript.SQLHelper;

namespace WordWebCMS
{
    public static class Conn
    {
        /// <summary>
        /// 公共的用户数据
        /// </summary>
        public static MySQLHelper RAWUser = new MySQLHelper(ConfigurationManager.ConnectionStrings["connUsrStr"].ConnectionString);
        /// <summary>
        /// 该系统的全部数据 (包括文章啥的)
        /// </summary>
        public static MySQLHelper RAW = new MySQLHelper(ConfigurationManager.ConnectionStrings["connStr"].ConnectionString);



        

    }
}