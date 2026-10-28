# 部署与运行

服务器需要 .NET 10 ASP.NET Core 运行时和 MySQL。先创建数据库，再部署发布目录中的文件。程序使用 FreeSql.Provider.MySqlConnector，不运行旧版 setup.sql。

## 1. 数据库与配置

建议分别创建账号库和站点库，字符集使用 `utf8mb4`。多个新版站点可以共用账号库，但每个站点要有自己的站点库，否则角色和内容也会共用。本地单站测试可以让两条连接指向同一个空库，程序会复用 FreeSql 实例，表名不会冲突。

连接字符串示例，使用时替换占位值，不要把真实凭据提交到仓库：

```text
Server=database-host;Port=3306;Database=cms_accounts;User ID=cms;Password=replace-me;SslMode=Required
Server=database-host;Port=3306;Database=cms_site;User ID=cms;Password=replace-me;SslMode=Required
```

连接可以在安装页面填写，也可以通过环境变量配置。

<details>
<summary>环境变量配置表</summary>

| 配置键 / 环境变量 | 用途 |
| --- | --- |
| `ConnectionStrings__Accounts` | 账号、密码哈希、验证/重置凭证 |
| `ConnectionStrings__Site` | 本站内容、角色、评论、设置等 |
| `Setup__Token` | 运维人员生成的随机安装令牌；未提供时自动生成至 `Storage:Path/setup.json` |
| `Storage__Path` | 持久目录；相对路径基于应用内容根目录，默认 `App_Data` |
| `Site__CookieName` | 站点独立 Cookie 名称，也是 Data Protection 应用标识；安装后保持稳定 |
| `Mail__Host` / `Mail__Port` | SMTP 地址与端口 |
| `Mail__Security` | 默认 `StartTls`；也支持 `SslOnConnect`；`None` 仅用于本地测试捕获服务 |
| `Mail__From` / `Mail__User` / `Mail__Password` | 发件地址与 SMTP 凭据 |
| `ReverseProxy__KnownProxies__0` | 实际反向代理 IP；可继续配置 `__1` 等 |
| `AllowedHosts` | 生产域名，分号分隔 |

</details>

不配置 SMTP 也能安装管理员和写文章，但无法开放注册或发送找回密码邮件。公开根地址在安装页面或后台设置中填写，用于邮件链接、RSS、sitemap 和 canonical，不从请求 Host 拼接。

<details>
<summary>邮件设置</summary>

安装后，在「后台 → 设置 → 邮件服务（SMTP）」填写服务器、端口、连接加密、发件地址、用户名和密码或授权码，点击「保存邮件设置」即可生效，无需重启。通常 587 配 `StartTls`，465 配 `SslOnConnect`，以服务商要求为准。

页面不会显示已保存的密码：留空保留原值，填新值则替换，勾选清除则删除。清空服务器和发件地址可停用邮件。

后台保存的配置优先于上表的 `Mail` 配置；首次保存前，使用配置文件或环境变量。邮件配置保存在 `Storage:Path/mail.json`，密码由 ASP.NET Core Data Protection 密钥加密。迁移和备份时，要一起保留这个文件、`Storage:Path/keys` 和原 `Site:CookieName`。配置邮件不需要升级数据库。

没有 SMTP 配置时，注册页面会提示暂不可注册，找回密码流程也无法发信。

</details>

<details>
<summary>本地开发配置</summary>

开发机配置放在仓库根目录 `Local/appsettings.local.json`，加上 `--local` 才会加载。环境变量和命令行可以覆盖其中的值。示例：

```json
{
  "ConnectionStrings": { "Accounts": "填写账号库连接", "Site": "填写站点库连接" },
  "Storage": { "Path": "../Local/data" },
  "Site": { "CookieName": "WordWebCMS-Local" },
  "Setup": { "Token": "运维生成的随机令牌" },
  "Urls": "http://127.0.0.1:5191"
}
```

```powershell
dotnet run --project WordWebCMS/WordWebCMS.csproj --no-launch-profile -- --local --initialize
dotnet run --project WordWebCMS/WordWebCMS.csproj --no-launch-profile -- --local
```

`--local` 只用于源码工作目录。生产环境请使用部署配置或环境变量，上传发布文件，不要直接部署整个工作区。

</details>

## 2. 标准发布与安装

```text
dotnet publish WordWebCMS/WordWebCMS.csproj -c Release -o artifacts/publish
```

发布后，可以先设置持久目录并启动站点，再打开 `/Setup`：

1. 填写安装令牌和两条 MySQL 连接，点击「验证并保存连接」。数据库要提前创建，连接中须包含 `Server`、`Database`、`User ID`。两条连接都通过验证后才会保存。已有连接可留空保留，也可直接进入下一步。
2. 点击「执行 initialize」。也可以展开「手动初始化（命令行）」，在服务器执行命令，再点击「重新检查数据库」。首次安装不需要重启站点；命令行要使用相同的 `Storage:Path`。
3. 填写站点资料和管理员账号，点击「完成安装」。每次提交都要填写安装令牌。页面不会显示已填写的连接字符串、密码和令牌。

<details>
<summary>手动初始化与数据库版本</summary>

如果已在发布目录配置好数据库连接，也可以先用命令行初始化，再启动站点：

```text
dotnet WordWebCMS.dll --initialize
dotnet WordWebCMS.dll --urls http://127.0.0.1:8080
```

`--initialize` 会创建或更新新版数据表，写入 `schema_version=4` 后退出。它不会清空已有的新版共享账号，也不会重设密码。普通页面访问和状态检查不会执行 DDL。

以下数据库不能直接初始化：含有旧表但没有新版版本记录的数据库，以及版本比程序更新的数据库。

跨库 DDL 可能只完成一部分。失败后先修正错误，再重新执行，**不要手工伪造版本记录**。已有站点升级表结构时，要先停站，执行命令后再启动。

</details>

没有设置 `Setup:Token` 时，首次启动会自动生成随机令牌，保存在 `Storage:Path/setup.json`。请在服务器上读取，启动日志只会提示文件位置，浏览器不会公开令牌。

页面保存的连接也在这个文件中，优先于同名环境变量、命令行和配置文件，保存后立即生效。重启和执行 `--initialize` 时也会读取。要改回部署配置中的连接，停站后删除文件中的 `ConnectionStrings` 节点。应用账号要有配置目录的写入权限；这个文件含有数据库凭据，请限制读取权限，不要放在 `wwwroot` 下。

使用已有共享账号时，要填写该账号已验证的邮箱和原密码，才能成为本站管理员。安装不会覆盖账号密码或其他站点的角色。完成后，`site_settings` 的单例主键和每次安装 POST 检查会锁定安装入口。

首次安装会在同一个站点事务中创建“未分类”、欢迎文章、对应草稿和已发布修订，文章属于本次安装的管理员。标题、摘要和正文沿用旧版，短名为 `helloworld`，可以在后台编辑或删除。阅读量和点赞从真实记录开始。以后启动、更新程序或重复安装，都不会补写或覆盖已有文章。

启动时，程序会把发布包中的 `theme/default` 写入持久目录 `theme/default`，以后启动也会更新内置主题文件。其他主题目录会保留；要修改主题，请使用新的主题 ID。

日志写到标准输出和错误输出。一般异常会显示通用错误页，日志中保留诊断信息和请求编号。不要把生产异常日志或数据库备份放在 `wwwroot`。

## 3. 持久目录与更新

用 `Storage__Path` 指定发布目录之外的持久目录，并给站点运行账号写入权限。里面包括：

```text
media/                上传文件
theme/                内置和用户安装主题
keys/                 Data Protection 认证密钥
languages/            自定义 CultureInfo JSON 语言包
setup.json            页面保存的数据库连接及自动生成的安装令牌（若有）
```

在发布目录执行 `dotnet WordWebCMS.dll` 即可运行，不需要 SDK。上传主题也不需要重新编译。对外访问通过 HTTPS 反向代理提供。

更新时，先重新发布，停站替换程序，保留持久目录和部署配置，CookieName 也要保持不变。只有发行说明要求更新表结构时，才需要在停站期间执行对应命令。当前 v4 可以从现代化 v1/v2/v3 保留数据升级，不支持旧 Web Forms 表结构。详见 [语言、注册与短链接](Languages-and-Registration.md)。

## 4. HTTPS 与账号边界

生产环境使用 HTTPS。反向代理要转发 `X-Forwarded-Proto`，并在 `KnownProxies` 中填写实际代理 IP。程序不信任任意来源的转发头。

Cookie 使用 HttpOnly、SameSite=Lax，在 HTTPS 下设置 Secure。每个已登录请求都会重新检查共享账号状态和本站角色。重置密码后，所有使用旧 security stamp 的登录票据都会失效；本站封禁只影响本站。

账号接口按 IP 限制 POST 请求，每分钟 10 次；其他写入表单每分钟 120 次，读取页面不受此限制。使用反向代理时，要正确配置转发地址，否则多个访问者可能共用代理 IP 的配额。

## 5. 备份与恢复

备份要包含同一恢复点的共享账号库、所有相关站点库、各站完整的持久目录和部署配置，并保留原 CookieName。缺少账号库会丢失作者和角色关联，只恢复数据库会导致媒体链接损坏，丢失密钥则无法解密已有 Cookie。

建议操作：

1. 暂停站点写入。多个站点共用账号库时，一并暂停这些站点。
2. 用现有的 MySQL 备份工具导出账号库和站点库。例如 Linux 下可用 `mysqldump --single-transaction --routines --triggers --databases cms_accounts cms_site > databases.sql`，凭据放在受保护的客户端配置中。
3. 备份实际 `Storage:Path` 的完整目录和私有部署配置。密钥、账号库和 SMTP 凭据要加密备份，并限制访问。
4. 恢复到新的空数据库和持久目录，恢复目录所有权，再用同一程序版本连接恢复后的数据库。
5. 检查公开文章与修订、媒体地址、当前主题、导航、登录、本站角色、评论和点赞。恢复环境要与线上隔离，检查完成前不对外开放。

此前已备份用户指定的测试库和文件，并在隔离的 Linux 环境中恢复到新库和持久目录，验证了文章、媒体、主题、角色、点赞和原 Cookie。正式上线前，仍需用实际的生产备份位置、加密方式和运维账号演练恢复。

实现参考：[FreeSql CodeFirst](https://freesql.net/en/guide/code-first.html)。

### 2026-10-29 更新

本次新增共享账号的 `cms_user.Bio` 字段，数据库结构版本为 4。停站并备份后，使用原有配置运行 `dotnet WordWebCMS.dll --initialize`，再启动新版本。账号库和站点库都需完成初始化；这次升级不会对既有 UTC 时间做时差平移。

内置 default/milky 主题会随启动更新。独立上传的主题保持原文件，请按 [主题说明](Native-Themes.md) 接入作者链接、个人主页和浏览器时间显示。

时间继续以 UTC 写入数据库；HTML 使用带 `Z` 的完整时间，由浏览器按本地时区显示。禁用 JavaScript 时显示带 UTC 标记的回退时间。月份归档按 UTC 月份划分，以保持共享 URL 的结果一致。

文章编辑页新增「转换内容类型」。转换保留 ID、作者、链接、首次发布时间、正文和译文，并分别保留已发布正文与当前草稿。转为页面会清除当前分类、标签、置顶、评论和点赞；历史修订保留，但恢复到页面时会忽略文章专用字段。待审核的旧类型修订会被驳回。转回文章时评论默认开启，已丢弃的数据不恢复。已发布内容只有编辑或管理员可以转换。
