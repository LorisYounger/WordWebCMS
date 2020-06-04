<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="index.aspx.cs" Inherits="WordWebCMS.Index" %>

<asp:Literal ID="LHeader" runat="server" />
<div id="page" class="site">
    <div id="content" class="site-content">

        <div id="primary" class="content-area">
            <main id="main" class="site-main" role="main">
                <asp:Literal ID="LContentPage" runat="server" />
                <nav class="paging-navigation" role="navigation">
                    <h1 class="screen-reader-text">文章导航</h1>
                    <div class="nav-links">
                        <asp:Literal ID="LNavLinks" runat="server" />
                    </div>
                </nav>
            </main>
        </div>
        <div id="secondary" class="widget-area">            
            <asp:Literal ID="LSecondary" runat="server" />
        </div>
    </div>
</div>
<asp:Literal ID="LFooter" runat="server" />