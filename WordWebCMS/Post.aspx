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
                        <div id="postlike" class="nav-next">
                            <asp:Literal ID="Lpostlike" runat="server"></asp:Literal>
                        </div>
                    </footer>
                    <asp:Literal ID="LComments" runat="server" />
                    <asp:Panel id="commentspanel" class="comment-respond" visible="false" runat="server">
                        <h3 id="reply-title" class="comment-reply-title">发表评论</h3>
                        <p class="comment-notes">请自觉遵守互联网相关的政策法规，严禁发布色情、暴力、反动的言论。  支持<img src="Picture/markdown.png" width="20" /></p>
                        <p class="comment-form-comment">
                            <label for="comment">评论</label>
                            <asp:TextBox runat="server" id="comment" name="comment" cols="45" rows="8" maxlength="65525" TextMode="MultiLine"></asp:TextBox>
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
<script>
    function LikePost(pid) {
        var xmlhttp = new XMLHttpRequest();
        xmlhttp.onreadystatechange = function () {
            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {
                document.getElementById("postlike").innerHTML = xmlhttp.responseText;
            }
        }
        xmlhttp.open("GET", "ajax.ashx?action=postlike&ID=" + pid, true);
        xmlhttp.send();
    }
    function Reply(username) {
        document.getElementById("comment").innerText = "--回复:" + username + "--\n" + document.getElementById("comment").innerText
    }
    function Indexfold() {
        if (document.getElementById("index-fold").innerText == "折叠") {
            document.getElementById("index-fold").innerText = "展开";
            document.getElementById("ul-index").style.visibility = "hidden";
        } else {
            document.getElementById("index-fold").innerText = "折叠";
            document.getElementById("ul-index").style.visibility = "visible";
        }
    }
</script>
<asp:Literal ID="LFooter" runat="server" />
