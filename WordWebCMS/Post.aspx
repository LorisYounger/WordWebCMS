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
                    <div id="commentspanel" class="comment-respond" visible="false" runat="server">
                        <h3 id="reply-title" class="comment-reply-title">发表评论</h3>
                        <p class="comment-notes">请自觉遵守互联网相关的政策法规，严禁发布色情、暴力、反动的言论。  支持<img src="Picture/markdown.png" width="20" /></p>
                        <div id="editor"></div>
                        <%--<asp:CheckBox runat="server" id="comment_mail_notify" checked="false" visible="false" Text="有人回复时邮件通知我"></asp:CheckBox>--%>
                        <div id="commentssubmit" runat="server"></div>
                    </div>
                </form>
            </main>
        </div>
        <div id="secondary" class="widget-area">
            <asp:Literal ID="LSecondary" runat="server" />
        </div>
    </div>
</div>
<script>
    const editor = new toastui.Editor({
        el: document.querySelector('#editor'),
        height: '300px',
        initialValue: '',
        initialEditType: 'wysiwyg',
        previewStyle: 'vertical',
    });
    function LikePost(pid) {
        var xmlhttp = new XMLHttpRequest();
        xmlhttp.onreadystatechange = function () {
            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {
                document.getElementById("postlike").innerHTML = xmlhttp.responseText;
            }
        }
        xmlhttp.open("GET", "ajax.ashx?action=postlike&ID=" + pid);
        xmlhttp.send();
    }
    function LikeReview(pid) {
        var xmlhttp = new XMLHttpRequest();
        xmlhttp.onreadystatechange = function () {
            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {
                document.getElementById("reviewlike" + pid).innerHTML = xmlhttp.responseText;
            }
        }
        xmlhttp.open("GET", "ajax.ashx?action=reviewlike&ID=" + pid);
        xmlhttp.send();
    }
    function Reply(rid) {
        //document.getElementById("comment").innerHTML = "wwcms:|reply#" + rid + ":|&#13;"
        editor.setHTML("wwcms:|reply#" + rid + ":|<br />");
    }
    function SendReview(pid) {
        //var mail_notify = document.getElementById("comment");
        //var mail_notify_str = "false";
        //if (mail_notify != null) {
        //    if (mail_notify.checked) {
        //        mail_notify_str = "true";
        //    }
        //}//邮件相关功能更改至用户设置
        var xmlhttp = new XMLHttpRequest();
        xmlhttp.onreadystatechange = function () {
            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {
                alert(xmlhttp.responseText);
            }
        }
        //xmlhttp.open("POST", "ajax.ashx?action=sendreview&ID=" + pid + "&mail=" + mail_notify_str);
        xmlhttp.open("POST", "ajax.ashx?action=sendreview&ID=" + pid);
        xmlhttp.setRequestHeader('Content-type', 'raw');
        xmlhttp.send(encodeURI(editor.getMarkdown()));
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
