<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="WordWebCMS.Login" %>

<asp:Literal ID="LHeader" runat="server" />
<form runat="server" class="CenterBox">
    <asp:TextBox runat="server" ID="MasterKey" style="display: none"></asp:TextBox>
    <div id="divlogin" runat="server" visible="false">
        <h1 style="text-align: center">登录</h1>
        <p id="errorboxlogin" runat="server" class="errorbox" visible="false"></p>
        <p class="BoxLable">用户名或电子邮件地址</p>
        <asp:TextBox runat="server" ID="usernamelogin" class="singlelineinput"></asp:TextBox>
        <p class="BoxLable">密码</p>
        <asp:TextBox runat="server" ID="passwordlogin" TextMode="Password" class="singlelineinput"></asp:TextBox>
        <p class="BoxLable">请计算: </p>
        <asp:Label runat="server" Text="0+0=" ID="CalloginKey"></asp:Label>
        <asp:TextBox runat="server" ID="checkloginkey" class="singlelineinput"></asp:TextBox>
        <br>
        <asp:Button runat="server" Text="登陆" ID="buttonlogin" OnClick="buttonlogin_Click" />
        <br />
        <br />
        <a href="?Action=Register">没有账户?立即注册</a>
    </div>
    <div id="divregister" runat="server" visible="false">
        <h1 style="text-align: center">注册</h1>
        <p id="errorboxregister" runat="server" class="errorbox" visible="false"></p>
        <p class="BoxLable">用户名</p>
        <asp:TextBox runat="server" ID="usernamereg" class="singlelineinput"></asp:TextBox>
        <p class="BoxLable">电子邮件</p>
        <asp:TextBox runat="server" ID="emailreg" class="singlelineinput"></asp:TextBox>
        <p class="BoxLable">密码</p>
        <asp:TextBox runat="server" ID="passwordreg" TextMode="Password" class="singlelineinput"></asp:TextBox>
        <p class="BoxLable">请计算: </p>
        <asp:Label runat="server" Text="0+0=" ID="CalregistKey"></asp:Label>
        <asp:TextBox runat="server" ID="checkregisterkey" class="singlelineinput"></asp:TextBox>
        <br>
        <div id="emailcheck" runat="server" visible="false">
            <p class="BoxLable">邮箱验证码</p>
            <asp:Button runat="server" Text="获取验证码" style="float: right; font-size: 50%;" />
            <asp:TextBox runat="server" ID="TextBox5" class="singlelineinput"></asp:TextBox>
            <br>
        </div>
        <asp:Button runat="server" Text="注册" ID="buttonregister" />
        <br />
        <br />
        <a href="?Action=Login">已有账户?立即登陆</a>
    </div>
</form>
<asp:Literal ID="LFooter" runat="server" />