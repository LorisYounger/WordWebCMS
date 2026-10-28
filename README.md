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
- 支持主题功能, 内置 default 和 milky 两款主题, 也可以在后台上传自己的Liquid主题
- 支持多语言界面和文章译文, 可以自己加JSON语言包
- 评论、回复、点赞, 分类、标签、搜索、媒体库、RSS这些也都有

## 部署WordWebCMS

服务器需要 .NET 10 ASP.NET Core 运行时和 MySQL，自己编译还需要 .NET 10 SDK。

1. 编译并发布网站

   ```powershell
   dotnet publish WordWebCMS/WordWebCMS.csproj -c Release -o artifacts/publish
   ```

2. 把 `artifacts/publish` 上传到服务器，准备好 MySQL。数据库信息稍后在 `/Setup` 填写；账号有权限时，程序会自动创建数据库。

3. 在发布目录启动网站

   ```powershell
   dotnet WordWebCMS.dll --urls http://127.0.0.1:8080
   ```

4. 打开 `/Setup`，复制启动日志里的 Setup Token。填好数据库地址、名称、账号和密码，点击「保存并连接」，程序会自动建表。再填写站点和管理员信息，就能去后台写文章。

Token 会在控制台或容器日志中打印，浏览器验证后记住授权 7 天，刷新、保存都不用重填。也可以自己设置 Token；没有设置时，程序会自动生成并保存到 `Storage:Path/setup.json`。

数据库连接也保存在这个文件里，保存后立即生效，重启后仍保留，并优先于同名部署配置。请保护并备份它。已保存的数据库密码不会在页面显示，留空即可保留；多个站点共享账号时，展开「多个站点共享账号」填写账号库。

更新程序或重建容器后，站点会自动检查并升级数据库，不需要另跑初始化命令。未能完成时，在 `/Setup` 点击重试即可。已有站点不会重复创建管理员或欢迎文章。

<details>
<summary>使用环境变量或命令行安装</summary>

也可以提前设置环境变量，启动后自动初始化，省去在页面填写连接的步骤：

- `ConnectionStrings__Accounts`：用户数据库连接。
- `ConnectionStrings__Site`：本站数据库连接，不共享用户时可以和上面填一样的。
- `Setup__Token`：自己生成的随机安装令牌。不设置也可以，程序会自动生成。
- `Storage__Path`：媒体、主题和登录密钥的保存目录，建议放在发布目录外，更新程序时保留。

仍保留手动命令 `dotnet WordWebCMS.dll --initialize`，但普通安装和更新不需要执行它。

</details>

新站会有一篇「你好,世界 又一个WWCMS站点」，可以编辑或删除它，开始写自己的博客。

需要开放注册和找回密码时，在「后台 → 设置 → 邮件服务（SMTP）」中填写邮件配置，保存后立即生效。反向代理、HTTPS、邮件和备份见 [部署说明](docs/Deployment.md)。

新版重新设计了表结构，旧版数据库不能直接使用。主题使用 Liquid 模板，不兼容 WordPress 模板。

## 本地开发

```powershell
dotnet build WordWebCMS.sln -c Release
```

本机数据库等配置可以放在仓库根目录的 `Local/appsettings.local.json`，然后运行：

```powershell
dotnet run --project WordWebCMS/WordWebCMS.csproj --no-launch-profile -- --local
```

`Local/` 不会提交到 Git，也不会随网站发布。配置格式见 [部署说明](docs/Deployment.md)。

## 相关文档

- [部署、配置与备份](docs/Deployment.md)
- [语言包、注册管理与短链接](docs/Languages-and-Registration.md)
- [自己制作主题](docs/Native-Themes.md)
- [新版功能与测试记录](docs/Modernization-Implementation.md)
- [改造前的评估和设计](docs/Modernization-Assessment.md)
