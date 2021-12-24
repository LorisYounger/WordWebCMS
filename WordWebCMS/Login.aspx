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
        <a href="?Action=Forget" style="text-align: right">忘记密码?通过邮件找回</a>
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
            <button id="bottonsendregemail" style="float: right; font-size: 50%;" onclick="sendregemail()" type="button">获取验证码</button>
            <asp:TextBox runat="server" ID="emailcode" class="singlelineinput"></asp:TextBox>
            <br>
        </div>
        <asp:Button runat="server" Text="注册" ID="buttonregister" OnClick="buttonregister_Click" />
        <br />
        <br />
        <a href="?Action=Login">已有账户?立即登陆</a>
        <a href="?Action=Forget" style="text-align: right">忘记密码?通过邮件找回</a>
    </div>
    <div id="divforget" runat="server" visible="false">
        <h1 style="text-align: center">忘记密码</h1>
        <p id="errorboxforget" runat="server" class="errorbox" visible="false"></p>
        <p class="BoxLable">电子邮件</p>
        <asp:TextBox runat="server" ID="emailforget" class="singlelineinput"></asp:TextBox>
        <p class="BoxLable">请计算: </p>
        <asp:Label runat="server" Text="0+0=" ID="Calforgetkey"></asp:Label>
        <asp:TextBox runat="server" ID="checkforgetkey" class="singlelineinput"></asp:TextBox>
        <br>
        <asp:Button runat="server" Text="发送重置密码链接至邮箱" ID="buttonsendmail" OnClick="buttonsendmail_Click" />
        <br />
        <br />
        <a href="?Action=Login">想起密码?立即登陆</a>
        <a href="?Action=Register" style="text-align: right">没有账户?立即注册</a>
    </div>
    <div id="divfindmy" runat="server" visible="false">
        <h1 style="text-align: center">重置密码</h1>
        <p id="errorboxfindmy" runat="server" class="errorbox" visible="false"></p>
        <br />
        <h3>您的密码已重置,请记录您的新密码</h3>
        <br />
        <p class="BoxLable">账户名称</p>
        <asp:TextBox runat="server" ID="emailfindmy" class="singlelineinput" ReadOnly="True"></asp:TextBox>
        <p class="BoxLable">新密码</p>
        <asp:TextBox runat="server" ID="passwordfindmy" class="singlelineinput" ReadOnly="True"></asp:TextBox>
    </div>
</form>
<script type="text/javascript">    
    function sendregemail() {
        var xmlhttp = new XMLHttpRequest();
        xmlhttp.onreadystatechange = function () {
            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {
                document.getElementById("bottonsendregemail").innerText = xmlhttp.responseText;
            }
        }
        xmlhttp.open("GET", "ajax.ashx?action=regemail&ID=" + document.getElementById("MasterKey").value + "&email=" + document.getElementById("emailreg").value + "&key=" + document.getElementById("checkregisterkey").value, true);
        xmlhttp.send();
    }
</script>
<asp:Literal ID="LFooter" runat="server" />