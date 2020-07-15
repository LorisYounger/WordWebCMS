<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="WordWebCMS.Login" %>

<asp:Literal ID="LHeader" runat="server" />
<form id="login" runat="server" class="CenterBox">    
    <h1 style="text-align:center">登录</h1>
      <p class="BoxLable">用户名或电子邮件地址</p>
      <asp:TextBox runat="server" ID="username" class="singlelineinput"></asp:TextBox>
      <p class="BoxLable">密码</p>
      <asp:TextBox runat="server" ID="password" TextMode="Password"  class="singlelineinput"></asp:TextBox>
      <p class="BoxLable">验证码</p>
      <asp:Label runat="server" Text="0+0=" ID="CalKey"></asp:Label>
      <asp:TextBox runat="server" ID="checkkey" class="singlelineinput"></asp:TextBox>
      <br>
      <asp:Button runat="server" Text="登陆" style="float: left;"/>  <asp:Button runat="server" Text="注册" style="float: right;" />
</form>
<asp:Literal ID="LFooter" runat="server" />