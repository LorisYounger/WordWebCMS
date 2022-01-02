<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Setup.aspx.cs" Inherits="WordWebCMS.Setup" MaintainScrollPositionOnPostback="true" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title>部署WordWebCMS</title>
</head>
<body>
    <form id="formmain" runat="server">
        <h1>感谢您使用WordWebCMS</h1>
        <p>本网页会帮助您搭建WordWebCMS</p>
        <br />
        <p>点击链接跳转至相关步骤,如果您是第一次使用,建议按步骤进行部署</p>
        <p>
            <a href="?step=1">第一步:连接数据库服务器</a><br />
            <a href="?step=2">第二步:创建数据表</a><br />
            <a href="?step=3">第三步:创建网站管理员</a><br />
            <a href="?step=4">第四步:初始化网站信息</a><br />
            <a href="?step=5">第五步:禁用Setup.aspx</a><br />
        </p>
        <br />
        <div id="divdisable" runat="server" visible="false">
            <h2>WWCMS Setup 已被禁用</h2>
            <p>您已经成功部署WordWebCMS, 请删除根目录下的Setup.aspx以确保网站安全</p>
            <p>如果您需要重新配置相关功能,请前往数据库删除`Setting`表下面的`disablesetup`键</p>
        </div>
        <div id="divfirst" runat="server" visible="false">
            <h2>第一步:连接数据库服务器</h2>
            <p>
                您需要手动在 web.config 文件修改数据库连接字符串<br />
                但是不用担心,WordWebCMS 将会帮助您生成合适的链接字符串<br />
                您只需要替换&lt;connectionStrings&gt;内的内容,如图所示
            </p>
            <img src="http://lbyun.eicp.net/wwc/webconfig.png" style="width: 600px" />
            <p>您的用户数据库是否使用的其他WordWebCMS的数据库</p>
            <asp:RadioButton AutoPostBack="true" GroupName="user" ID="RadioUserDataYes" runat="server" Text="是, 我和其他的WordWebCMS使用同一套数据库" OnCheckedChanged="RadioUserDataYes_CheckedChanged" /><br />
            <asp:RadioButton AutoPostBack="true" GroupName="user" ID="RadioUserDataNo" runat="server" Checked="True" Text="不, 这是我第一次使用WordWebCMS或我想独立用户数据库" OnCheckedChanged="RadioUserDataYes_CheckedChanged" />
            <div id="divuserdatayes" runat="server" visible="false">
                <p>其他WordWebCMS使用数据库连接字符串是?</p>
                <asp:TextBox ID="TextBoxOtherLink" runat="server" Width="500px"></asp:TextBox>
            </div>
            <h3>请在下方填写数据库相关信息,如果您不确定,可以咨询您的服务商</h3>
            <p>您的数据库服务器地址是? 如果是本机则填写localhost即可</p>
            <asp:TextBox ID="TextBoxServer" runat="server"></asp:TextBox>
            <p>您的数据库使用的端口号是? 默认mysql的端口是3306</p>
            <asp:TextBox ID="TextBoxPort" runat="server"></asp:TextBox>
            <p>您的数据库使用的用户名是? 不建议使用root</p>
            <asp:TextBox ID="TextBoxUserName" runat="server"></asp:TextBox>
            <p>您的数据库使用的密码是? </p>
            <asp:TextBox ID="TextBoxPass" runat="server"></asp:TextBox>
            <p>您打算使用的数据库名是?</p>
            <asp:TextBox ID="TextBoxTable" runat="server"></asp:TextBox>
            <p>其他连接字符串内容,如无特殊需要无需修改</p>
            <asp:TextBox ID="TextBoxotherconn" runat="server"></asp:TextBox>
            <br />
            <p>
                以上便是数据库所需的所有内容,点击 "生成连接字符串" 按钮 生成 &lt;connectionStrings&gt;
            </p>
            <div id="div1output" runat="server" visible="false" style="background-color: #eee;">
                <h3>生成结果</h3>
                <p>您只需要打开根目录下 web.config 文件替换 &lt;connectionStrings&gt; 内容为以下内容即可</p>
                <hr />
                <code id="TextBoxOutputConn" runat="server" style="color: #444; padding: 0; font-family: Consolas;"></code>
                <hr />
            </div>
            <br />
            <asp:Button ID="ButtonGenConn" runat="server" Text="生成连接字符串" OnClick="ButtonGenConn_Click" />
            <br />
            <p>如果您的连接字符串无需修改或已经进行过这一步操作,请<a href="?step=2">点击链接</a>跳转至第二步</p>
        </div>
        <div id="divsecond" runat="server" visible="false">
            <h2>第二步:创建数据表</h2>
            <p>
                如果您未创建数据表,并且账号拥有'create table'权限, WordWebCMS将会帮助您创建表格<br />
                点击下方按钮一键创建表格
            </p>
            <asp:Button ID="ButtonCreateTable" runat="server" Text="创建WordWebCMS所需表格" OnClick="ButtonCreateTable_Click" />
            <div style="background-color: #eee;" id="pcreateinfo" runat="server" visible="false">
            </div>
            <br />
            <p>如果您已经创建过或已经进行过这一步操作,请<a href="?step=3">点击链接</a>跳转至第三步</p>
        </div>
        <div id="divThird" runat="server" visible="false">
            <h2>第三步:创建网站管理员</h2>
            <p>
                此操作将会创建一个网站管理员 可以管理全部功能与数据 (包括使用这个用户表的其他网站)
            </p>
            <div id="divhaveadmin" runat="server" visible="false">
                <h2>此用户数据库已存在管理员</h2>
                <p>如需修改管理员信息,请手动修改数据库</p>
                <h3>当前 用户id:1 信息</h3>
                <p id="padmininfo" runat="server"></p>
                <h3>如需修改密码 点击按钮通过生成密码秘钥(md5s)</h3>
                <asp:TextBox ID="TextBoxnewadminpassword" runat="server" Text="WWCMSpassword" Width="300"></asp:TextBox><br />
                <asp:Button ID="Buttonpasswordmd5" runat="server" Text="生成密码秘钥" OnClick="Buttonpasswordmd5_Click" />
                <p>之后再将生成的秘钥替换至数据库即可</p>
                <br />
                <p>如果您对管理员账号没有异议,请<a href="?step=4">点击链接</a>跳转至第四步</p>
            </div>
            <div id="divCreateAdmin" runat="server" visible="false">
                <p>管理员 用户名</p>
                <asp:TextBox ID="TextBoxadminname" runat="server" Text="admin"></asp:TextBox>
                <p>管理员 邮箱</p>
                <asp:TextBox ID="TextBoxadminemail" runat="server" Text="admin@exlb.org" TextMode="Email"></asp:TextBox>
                <p>管理员 密码</p>
                <asp:TextBox ID="TextBoxadminpassword" runat="server" Text="WWCMSpassword"></asp:TextBox><br />
                <asp:Button ID="Buttoncreateadmin" runat="server" Text="创建管理员" OnClick="Buttoncreateadmin_Click" />
                <p style="background-color: #eee;" id="padminmessage" runat="server" visible="false">
                    管理员创建成功,请注意保存账号密码等信息
                    <br />
                    您可以随时<a href="?step=4">点击链接</a>跳转至第四步
                </p>
            </div>
        </div>
        <div id="divfourth" runat="server" visible="false">
            <h2>第四步:初始化网站信息</h2>
            <p>
                在这里将会设置网站相关设置,包括网站名称,网站链接等
            </p>
            <p>网站标题</p>
            <asp:TextBox ID="TextBoxwebtitle" runat="server" Width="400"></asp:TextBox>
            <p>网站副标题</p>
            <asp:TextBox ID="TextBoxwebsubtitle" runat="server" Width="500"></asp:TextBox>
            <p>网站链接 如果wwcms部署在非根目录上,请加上目录 例如 'https://www.exlb.org/wwcms'</p>
            <asp:TextBox ID="TextBoxweburl" runat="server" Width="400"></asp:TextBox>
            <p>联系方式 方便用户联系客服/管理员的文本或链接,支持html</p>
            <asp:TextBox ID="TextBoxwebconnlink" runat="server" ValidateRequestMode="Disabled" Width="400"></asp:TextBox>
            <p>是否启用邮件功能 启用邮件功能将支持 邮件<a>注册验证</a> <a>邮件找回密码等功能</a></p>
            <asp:CheckBox ID="CheckBoxemail" runat="server" Text="启用邮件功能" AutoPostBack="true" OnCheckedChanged="CheckBoxemail_CheckedChanged" />
            <div id="divemail" runat="server" visible="false">
                <p>邮箱SMTP使用的邮箱名</p>
                <asp:TextBox ID="TextBoxwebemailsmtp" runat="server" Width="400"></asp:TextBox>
                <p>邮箱SMTP使用的密码</p>
                <asp:TextBox ID="TextBoxwebemailpass" runat="server" Width="400"></asp:TextBox>
                <p>邮箱SMTP服务器的链接 一般是 smtp.xxx.com</p>
                <asp:TextBox ID="TextBoxwebemaillink" runat="server" Width="400"></asp:TextBox>
                <p>如果你没有smtp邮箱,可以去qq或者163等邮箱申请一个 见<a href="https://wwcms.exlb.org/GetSMTPEmail">获取SMTP邮件</a></p>
            </div>
            <p>网站图标链接 如果在服务器上直接填写绝对地址即可 其他可以填写链接</p>
            <asp:TextBox ID="TextBoxwebicon" runat="server" Width="500"></asp:TextBox>
            <p>网站底部信息 支持HTML</p>
            <asp:TextBox ID="TextBoxwebinfo" runat="server" Width="500" Height="50" ValidateRequestMode="Disabled" TextMode="MultiLine"></asp:TextBox>
            <p>自豪的使用WordWebCMS 添加PowerByWWCMS链接至网页底部信息 详见<a href="https://wwcms.exlb.org/powerbywwcms">使用WWCMS的网站列表</a></p>
            <asp:CheckBox ID="CheckBoxwebwwcms" runat="server" Text="添加WWCMS链接 (若网页底部信息已添加过,则不会添加)" />
            <p>允许注册账户</p>
            <asp:CheckBox ID="CheckBoxweballowreg" runat="server" Text="允许注册" />
            <p>新用户默认携带的积分</p>
            <asp:TextBox ID="TextBoxwebnewmoney" runat="server" TextMode="Number"></asp:TextBox>
            <p>新用户默认权限 具体数字对应权限见 <a href="https://wwcms.exlb.org/PostState">用户权限</a></p>
            <asp:TextBox ID="TextBoxwebuserAuth" runat="server" TextMode="Number"></asp:TextBox>
            <p>文章默认权限 (是否需要审核) 具体数字对应权限见 <a href="https://wwcms.exlb.org/PostState">文章状态</a></p>
            <asp:TextBox ID="TextBoxpoststatie" runat="server" TextMode="Number"></asp:TextBox>
            <p>评论默认权限 (是否需要审核等) 具体数字对应权限见 <a href="https://wwcms.exlb.org/ReviewState">评论状态</a></p>
            <asp:TextBox ID="TextBoxreviewstatie" runat="server" TextMode="Number"></asp:TextBox>
            <p>启用 URLRewrite 注:需要服务器支持UrlRewrite 并且修改Web.config 详见<a href="https://wwcms.exlb.org/UrlRewrite">URL重写</a></p>
            <asp:CheckBox ID="CheckBoxweburlrewrite" runat="server" Text="启用美观链接功能 (URLRewrite)" />
            <br />
            <br />
            <asp:Button ID="ButtonwebUpdate" runat="server" Text="应用设置" OnClick="ButtonwebUpdate_Click" />
            <p style="background-color: #eee;" id="pwebsuccess" runat="server" visible="false"></p>
            <p>如果您已经创建过或已经进行过这一步操作,请<a href="?step=5">点击链接</a>跳转至第五步</p>
        </div>
        <div id="divfifth" runat="server" visible="false">
            <h2>第五步:禁用Setup.aspx</h2>
            <p>
                在确定初始设置完成后, 点击下方按钮禁用 Setup.aspx 以防止被他人使用<br />
                如需修改设置, 请通过管理员账号登录网站后台修改设置
            </p>
            <asp:Button ID="ButtonStop" runat="server" Text="禁用 Setup.aspx" OnClick="ButtonStop_Click" />
            <br />
            <p>如果您已经创建过或已经进行过这一步操作,请在服务器上删除 Setup.aspx 以确保安全性</p>
        </div>
    </form>

</body>
</html>
