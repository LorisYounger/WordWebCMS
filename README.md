# WordWebCMS

<img src="WWC.png" alt="WWC" style="height:150px" />

WordWebCMS是一个简单的内容管理网站 (博客,文章等)
可以理解为一个简单版本的WordPress 虽然说功能更少 但是使用了C#!

新版使用 .NET 10 + ASP.NET Core Razor Pages, 数据库使用 MySQL + FreeSql.
自己写博客可以用, 也可以叫上其他人一起投稿.

## 项目优势

- Power By **C#**! **C# No.1**
- 项目不是非常复杂 可以自己魔改后用在自己的网站, 也可以加功能或和自己的其他软件联动 All in One
- 支持Markdown编写文章和页面, 可以预览、自动保存草稿、查看和恢复历史修订
- 支持多人投稿和审核, 改草稿不会直接把已经发布的文章改掉
- 支持多个网站共享用户 (需要使用相同的用户数据库), 各个网站的管理员和编辑权限单独设置
- 支持主题功能, 可以修改自带主题, 也可以在后台上传自己的Liquid主题
- 支持多语言界面和文章译文, 可以自己加JSON语言包
- 评论、回复、点赞, 分类、标签、搜索、媒体库、RSS这些也都有

## 部署WordWebCMS

需要 .NET 10 ASP.NET Core 运行时和MySQL数据库. 自己编译则需要 .NET 10 SDK.

1. 编译并发布网站

   ```powershell
   dotnet publish WordWebCMS/WordWebCMS.csproj -c Release -o artifacts/publish
   ```

2. 将 `artifacts/publish` 里的内容上传到服务器, 准备空数据库, 设置以下环境变量:

   - `ConnectionStrings__Accounts`: 用户数据库连接
   - `ConnectionStrings__Site`: 本站数据库连接, 不需要共享用户的话和上面填一样的即可
   - `Setup__Token`: 自己生成的随机安装令牌, 安装时会用到
   - `Storage__Path`: 媒体、主题和登录密钥的保存目录, 建议放在发布目录外, 更新程序时保留

3. 在发布目录初始化数据库, 然后启动网站

   ```powershell
   dotnet WordWebCMS.dll --initialize
   dotnet WordWebCMS.dll --urls http://127.0.0.1:8080
   ```

4. 打开 `/Setup`, 填写安装令牌和管理员信息. 安装完成后就可以去后台写文章了.

新站会有一篇「你好,世界 又一个WWCMS站点」, 可以编辑或者删除它, 然后开始你的博客.
注册和找回密码需要配置SMTP. 反向代理、HTTPS、邮件和备份的具体配置见 [部署说明](docs/Deployment.md).

新版重新做了表结构, 旧版数据库不能直接使用. 主题现在使用Liquid模板, 不兼容WordPress模板.

## 本地开发

```powershell
dotnet build WordWebCMS.sln -c Release
```

本机数据库等配置可以放在仓库根目录的 `Local/appsettings.local.json`, 然后运行:

```powershell
dotnet run --project WordWebCMS/WordWebCMS.csproj --no-launch-profile -- --local
```

`Local/` 不会提交到Git, 也不会随网站发布. 配置格式见 [部署说明](docs/Deployment.md).

## 相关文档

- [部署、配置与备份](docs/Deployment.md)
- [语言包、注册管理与短链接](docs/Languages-and-Registration.md)
- [自己制作主题](docs/Native-Themes.md)
- [新版功能与测试记录](docs/Modernization-Implementation.md)
- [改造前的评估和设计](docs/Modernization-Assessment.md)
