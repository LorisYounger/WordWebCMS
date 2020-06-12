<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Post.aspx.cs" Inherits="WordWebCMS.Post" %>

<asp:Literal ID="LHeader" runat="server" />
<div id="page" class="site">
    <div id="content" class="site-content">

        <div id="primary" class="content-area">
            <main id="main" class="site-main" role="main">
                <asp:Literal ID="LContentPage" runat="server" />

                <form id="respond" class="comments-area paging-navigation" runat="server">
                    <footer class="entry-footer">
                        <asp:Literal ID="LCatLink" runat="server"></asp:Literal>
                        <div class="nav-next">
                            <asp:Label runat="server" Text="114514" ID="LikeNumber"></asp:Label>个赞 <asp:Button runat="server" id="Like" style="border-style: none;width:30px;height:30px;" OnClick="Like_Click" />
                        </div>
                    </footer>
                    <asp:Panel id="commentspanel" class="comment-respond" visible="false" runat="server">
                        <h3 id="reply-title" class="comment-reply-title">发表评论</h3>
                        <p class="comment-notes">请自觉遵守互联网相关的政策法规，严禁发布色情、暴力、反动的言论。  支持<img src="Picture/markdown.png" width="20" /></p>
                        <p class="comment-form-comment">
                            <label for="comment">评论</label>
                            <asp:TextBox runat="server" id="comment" name="comment" cols="45" rows="8" maxlength="65525" TextMode="MultiLine"></asp:TextBox>
                        </p>
                        <p class="captcha">
                            <label for="captcha-answer">请输入算式的答案：</label>
                            <asp:Label id="captcha_question" runat="server" Text="19 + 19 = "></asp:Label><asp:TextBox id="captcha_anser" runat="server"></asp:TextBox>
                        </p>
                        <p>
                            <asp:CheckBox runat="server" id="comment_mail_notify" checked="true" Text="有人回复时邮件通知我"></asp:CheckBox>
                        </p>
                        <p class="form-submit">
                            <asp:Button runat="server" name="submit" type="submit" id="submit" class="submit" text="发表评论" OnClick="submit_Click" />
                        </p>
                    </asp:Panel>
                </form>
            </main>
        </div>
        <div id="secondary" class="widget-area">
            <asp:Literal ID="LSecondary" runat="server" />
        </div>
    </div>
</div>
<asp:Literal ID="LFooter" runat="server" />
