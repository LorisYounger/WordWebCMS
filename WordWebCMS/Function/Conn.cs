using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Linq;
using LinePutScript;
using LinePutScript.SQLHelper;

namespace WordWebCMS
{
    public static class Conn
    {
        private static IConfiguration? _configuration;
        
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
            RAWUser = new MySQLHelper(_configuration.GetConnectionString("connUsrStr") ?? "");
            RAW = new MySQLHelper(_configuration.GetConnectionString("connStr") ?? "");
        }
        
        /// <summary>
        /// 公共的用户数据
        /// </summary>
        public static MySQLHelper RAWUser { get; private set; } = null!;
        /// <summary>
        /// 该系统的全部数据 (包括文章啥的)
        /// </summary>
        public static MySQLHelper RAW { get; private set; } = null!;        

    }
}