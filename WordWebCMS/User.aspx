<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="User.aspx.cs" Inherits="WordWebCMS.UserInfo" %>

<asp:Literal ID="LHeader" runat="server" />
<div id="page" class="site">
    <div id="content" class="site-content">

        <div id="primary" class="content-area">
            <main id="main" class="site-main" role="main">
                <div id="userinfo">
                    <div class="page type-page hentry" runat="server" visible="false">
                    </div>
                </div>
            </main>
        </div>
        <div id="secondary" class="widget-area">
            <asp:Literal ID="LSecondary" runat="server" />
        </div>
    </div>
</div>
<asp:Literal ID="LFooter" runat="server" />