# 部署与运行

新版要求 .NET 10 ASP.NET Core 运行时，以及预先创建的 MySQL 数据库。采用标准发布目录部署。应用使用 FreeSql.Provider.MySqlConnector；不运行旧版 setup.sql。

## 1. 数据库与配置

推荐单独创建账号库和站点库，并使用 `utf8mb4`。账号库可由多个新版站点共用，各站点库必须独立，否则就会共享同一站点的角色和内容。本地单站测试允许两条连接配置指向同一空库：程序会复用同一个 FreeSql 实例，各实体表名不冲突。

示例连接结构（替换占位值，不要提交真实凭据）：

```text
Server=database-host;Port=3306;Database=cms_accounts;User ID=cms;Password=replace-me;SslMode=Required
Server=database-host;Port=3306;Database=cms_site;User ID=cms;Password=replace-me;SslMode=Required
```

| 配置键 / 环境变量 | 用途 |
| --- | --- |
| `ConnectionStrings__Accounts` | 账号、密码哈希、验证/重置凭证 |
| `ConnectionStrings__Site` | 本站内容、角色、评论、设置等 |
| `Setup__Token` | 运维人员生成的随机安装令牌；安装后可以移除 |
| `Storage__Path` | 持久目录；相对路径基于应用内容根目录，默认 `App_Data` |
| `Site__CookieName` | 站点独立 Cookie 名称，也是 Data Protection 应用标识；安装后保持稳定 |
| `Mail__Host` / `Mail__Port` | SMTP 地址与端口 |
| `Mail__Security` | 默认 `StartTls`；也支持 `SslOnConnect`；`None` 仅用于本地测试捕获服务 |
| `Mail__From` / `Mail__User` / `Mail__Password` | 发件地址与 SMTP 凭据 |
| `ReverseProxy__KnownProxies__0` | 实际反向代理 IP；可继续配置 `__1` 等 |
| `AllowedHosts` | 生产域名，分号分隔 |

安装后，管理员可在「后台 → 设置 → 邮件服务（SMTP）」填写服务器、端口、连接加密、发件地址、用户名和密码/授权码，点击「保存邮件设置」后立即用于注册验证和密码找回，无需重启。通常 587 配 `StartTls`，465 配 `SslOnConnect`，以服务商要求为准。密码不会回显；留空保留原值，填写新值替换，勾选清除则删除密码。同时清空服务器和发件地址可停用邮件。

后台保存的配置优先于上表的 `Mail` 部署配置；首次保存前仍使用配置文件/环境变量。后台配置保存在 `Storage:Path/mail.json`，密码使用现有 ASP.NET Core Data Protection 密钥加密。迁移和备份时必须同时保留该文件、`Storage:Path/keys` 和原 `Site:CookieName`；这项功能无需数据库结构升级。

没有 SMTP 配置时仍可安装管理员和写作，但注册页面明确显示暂不可注册，找回流程不能发信。公开根地址在安装/后台设置中填写，用于邮件链接、RSS、sitemap 和 canonical，不从请求 Host 拼接。

开发机私有配置放在仓库根目录 `Local/appsettings.local.json`，只有 `--local` 才加载；环境变量和命令行仍可覆盖它。示例：

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

`--local` 仅用于源码工作目录，不用于部署产物；生产环境使用部署配置或环境变量。不要把整个工作区当作发布目录。

## 2. 标准发布与安装

```text
dotnet publish WordWebCMS/WordWebCMS.csproj -c Release -o artifacts/publish
```

在发布目录中设置上述配置，然后执行：

```text
dotnet WordWebCMS.dll --initialize
dotnet WordWebCMS.dll --urls http://127.0.0.1:8080
```

`--initialize` 显式同步当前新版实体结构，写入 `schema_version=3`，然后退出。它拒绝含有其他旧表而没有新版版本记录的数据库，也拒绝比程序更新的版本记录。已存在的新版共享账号不会被清空或重新设定密码。DDL 不能保证跨库原子性；初始化失败时先修正错误，再重新执行，**不要手工伪造版本记录**。首次安装/结构更新时停止站点，执行命令后重新启动；普通 HTTP 请求不执行 DDL。

访问 `/Setup`，输入安装令牌、站点名、公开根地址和管理员资料。已有共享账号的邮箱必须匹配已验证账号，并验证其原密码，才可授予本站管理员；不会覆盖账号密码或其他站点角色。安装完成后 `site_settings` 的单例主键和每次安装 POST 检查共同锁定入口。

首次安装在同一个站点事务中创建“未分类”、旧版欢迎文章、对应草稿和已发布修订，文章归属本次安装的管理员。标题、摘要和正文沿用旧版，短名为 `helloworld`，可在后台编辑或删除。阅读量和点赞从真实记录开始；普通启动、程序更新和重复安装不会补写或覆盖已有文章。

启动时将发布包内 `theme/default` 初始化到持久目录 `theme/default`。后续启动会更新内置主题文件，保留其他主题目录；自定义修改应使用新的主题 ID。

程序日志写标准输出/错误输出。常规异常给用户通用错误页，并在日志中保留诊断信息和请求编号。不要把生产异常日志或数据库备份放在 `wwwroot`。

## 3. 持久目录与更新

通过 `Storage__Path` 指定独立于发布目录的持久目录，并授予站点运行账号写权限。该目录包含：

```text
media/                上传文件
theme/                内置和用户安装主题
keys/                 Data Protection 认证密钥
languages/            自定义 CultureInfo JSON 语言包
```

在发布目录执行 `dotnet WordWebCMS.dll`；运行环境无需 SDK，上传主题也无需重新编译程序。公开访问通过 HTTPS 反向代理提供。

部署新版本时重新生成发布产物，停站替换程序，保留持久目录、部署配置及稳定的 CookieName。只有发行说明要求结构更新时才在停站窗口执行对应显式命令；当前 v3 支持从现代化 v1/v2 保留数据升级，不支持旧 Web Forms 数据结构。详见 [语言、注册与短链接](Languages-and-Registration.md)。

## 4. HTTPS 与账号边界

反向代理需要转发 `X-Forwarded-Proto`，并在 `KnownProxies` 中配置真实代理 IP；程序不信任任意来源的转发头。生产访问使用 HTTPS。Cookie 使用 HttpOnly、SameSite=Lax，HTTPS 请求下设置 Secure。每个已登录请求重新读取共享账号状态及本站角色；重置密码后所有持旧 security stamp 的票据失效，本站封禁仅影响本站。

账号接口有按 IP 的请求速率限制（POST 10 次/分钟），其他写表单为 120 次/分钟；读取页面不使用此限制。部署在代理后必须正确配置转发地址，否则多个访问者可能共用代理 IP 配额。

## 5. 备份与恢复

备份必须作为同一恢复点包含：共享账号库、所有相关站点库、各站的整个持久目录、部署配置和稳定的 CookieName。共享账号库丢失会破坏作者/角色关联；仅恢复数据库会留下损坏的媒体链接；丢失密钥会使已有 Cookie 无法解密。

建议操作：

1. 停止需要备份的站点写入；共享账号库有多站写入时，一并暂停这些站点。
2. 用运维已有的 MySQL 备份工具导出账号库和站点库。例如在 Linux 中使用 `mysqldump --single-transaction --routines --triggers --databases cms_accounts cms_site > databases.sql`，凭据放受保护的客户端配置中。
3. 备份实际 `Storage:Path` 完整目录以及私有部署配置。对密钥、账号库和 SMTP 凭据使用受限访问和加密备份。
4. 恢复到空的新数据库及新的持久目录，恢复目录所有权，指向恢复库启动同一程序版本。
5. 核对公开文章与修订、媒体地址、当前主题、导航、账号登录、本站角色、评论和点赞。恢复环境与线上隔离，确认前不公开流量。

此前已将用户指定测试库和文件实际备份，再恢复到隔离 Linux 环境的新库和持久目录，验证文章、媒体、主题、角色、点赞及原 Cookie。正式上线前仍需针对实际生产备份位置、加密方式与运维账号做恢复演练。

实现参考：[FreeSql CodeFirst](https://freesql.net/en/guide/code-first.html)。
