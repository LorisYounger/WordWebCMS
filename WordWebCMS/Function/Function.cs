using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace WordWebCMS
{
    public static class Function
    {
        /// <summary>
        /// MD5字符串加盐
        /// </summary>
        /// <param name="txt">需要加密的文本</param>
        /// <returns>加密后字符串</returns>
        public static string MD5salt(string txt)
        {
            txt = txt.GetHashCode().ToString() + txt + txt.GetHashCode().ToString();
            using (MD5 mi = MD5.Create())
            {
                byte[] buffer = Encoding.Default.GetBytes(txt);
                //开始加密
                byte[] newBuffer = mi.ComputeHash(buffer);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < newBuffer.Length; i++)
                {
                    sb.Append(newBuffer[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
        /// <summary>
        /// 清除文本里面的特殊符号 包括script|iframe|object|embed|form
        /// </summary>
        /// <param name="html">要清除html的文本</param>
        /// <returns></returns>
        public static string SanitizeHtml(string html)
        => Westwind.Web.Markdown.Utilities.MarkdownUtils.SanitizeHtml(html);


        private static readonly string[] HeaderStyles = new string[]{"widget-index-H1", "widget-index-H2", "widget-index-H3", "widget-index-H4", "widget-index-H5", "widget-index-H6" };
        /// <summary>
        /// 生成侧边栏的目录系统
        /// </summary>
        /// <param name="html">HTML文件</param>
        /// <param name="limit">当index大于此不进行生成</param>
        /// <returns>生成的目录 可以直接使用</returns>
        public static string GenerateIndex(string html, int limit = 3)
        {
            Regex regex = new Regex(@"<h(?<level>[1-6])[\s\S]*?>[\s\S]*?</h([1-6])>", RegexOptions.IgnoreCase);
            Regex regexId = new Regex("(id|name)=\"(?<id>[\\s\\S]*?)\"", RegexOptions.IgnoreCase);
            MatchCollection headerMatches = regex.Matches(html);

            int previousLevel = 1;

            StringBuilder indexBuilder = new StringBuilder();
            indexBuilder.Append("<aside id=\"widget-title\" class=\"widget widget-index\"><h2 class=\"widget-title\">目录</h2><ul>");
            foreach (Match headerMatch in headerMatches)
            {
                int currentLevel = int.Parse(headerMatch.Groups["level"].Value);
                string header = Regex.Replace(headerMatch.Value, @"<[\s\S]*?>", string.Empty);

                Match idMatch = regexId.Match(headerMatch.Value);
                string id = idMatch.Success ? idMatch.Groups["id"].Value : null;

                string link = string.IsNullOrEmpty(id) ? header : string.Format("<a href=\"#{0}\">{1}</a>", id, header);

                if (currentLevel == previousLevel)
                {
                    indexBuilder.AppendFormat("<li style=\"{1}\">{0}</li>", link, HeaderStyles[currentLevel - 1]);
                }
                else if (currentLevel > previousLevel && currentLevel <= limit)
                {
                    indexBuilder.AppendFormat("<ul><li style=\"{1}\">{0}</li>", link, HeaderStyles[currentLevel - 1]);
                    previousLevel = currentLevel;
                }
                else if (currentLevel < previousLevel)
                {
                    indexBuilder.AppendFormat("</ul><li style=\"{1}\">{0}</li>", link, HeaderStyles[currentLevel - 1]);
                    previousLevel = currentLevel;
                }
            }
            indexBuilder.Append("</ul></aside>");
            return indexBuilder.ToString();
        }

    }
}