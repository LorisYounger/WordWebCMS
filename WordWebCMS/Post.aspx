<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Post.aspx.cs" Inherits="WordWebCMS.Post" %>

<asp:Literal ID="LHeader" runat="server" />
<div id="page" class="site">
    <div id="content" class="site-content">

        <div id="primary" class="content-area">
            <main id="main" class="site-main" role="main">                
                <asp:Literal ID="LContentPage" runat="server" />
            </main>
        </div>
        <div id="secondary" class="widget-area">            
            <asp:Literal ID="LSecondary" runat="server" />
        </div>
    </div>
</div>
<asp:Literal ID="LFooter" runat="server" />
