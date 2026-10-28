# lbnet 迁移工具

此工具针对本次已检查的 `lbnet → wwcms_lbnet` 数据集，不是通用 WordPress 导入器。迁移结果及待决定内容见 [迁移报告](../../docs/Lbnet-Migration.md)。不修改 WordWebCMS 运行时代码或旧站文件。

私有配置位于仓库忽略的 `Local/lbnet-migration/config.local.json`。字段为 `SourceConnection`、`TargetConnection`、`SourcePath`、`OutputPath`、`BaseUrl`、`DeploymentPath`。不能提交此文件或 `Local` 下的快照和凭据。

```powershell
dotnet build tools/WordPressMigration
dotnet tools/WordPressMigration/bin/Debug/net10.0/WordPressMigration.dll verify Local/lbnet-migration/config.local.json
dotnet tools/WordPressMigration/bin/Debug/net10.0/WordPressMigration.dll smoke Local/lbnet-migration/config.local.json
```

| 操作 | 作用 |
| --- | --- |
| `preflight` | 读取数据库可见性和授权，隐藏密码哈希 |
| `inspect` | 只读盘点，写入 `inspection.json` |
| `snapshot` | 一致性只读事务导出业务快照，只导出已批准评论，不导出登录密码哈希；拒绝覆盖已有快照 |
| `prepare` | 从快照生成媒体文件、正文摘要和待处理关系；首次生成新站管理员密码 |
| `import` | 仅允许目标库无表时初始化；所有账号与站点数据在一个事务内写入，不覆盖、不重复导入 |
| `verify` | 校验正文、草稿、历史修订、可见性、发布时间、标签、分类策略、媒体、账号及排除项 |
| `theme` | 在已导入站点上启用已准备好的 `lbnet` 主题 |
| `mail-inspect` | 只读检查 SMTP 类型及凭据是否存在，不输出凭据 |
| `migrate-mail` | 经单独授权后验证并解密旧站 SMTP 密码，以新站 Data Protection 密钥加密保存；只认证，不发信；拒绝覆盖已有邮件配置 |
| `smoke` | 校验运行站点的公开页面、媒体哈希、私有路径隔离、管理员登录及编辑页面；会增加被访问文章的阅读计数 |

`Deploy-Lbnet.ps1` 只向本次指定且尚不存在的目录部署，拒绝覆盖已有目录；复制后校验全部发布文件哈希。`Start-Preview.ps1` 与 `Stop-Preview.ps1` 放在部署根目录使用，只管理其记录的进程。当前没有安装开机自启服务。

DDL 不受内容事务回滚保护。如果首次 `import` 在 DDL 后失败，新库可能留下空表；工具会拒绝再次导入。应先检查新库、保留故障记录，再另行处理，不要绕过空库保护或清空已有站点。

准备阶段归一化 Githuber 的 CR CR LF 换行，保留 Markdown、静态 HTML 和历史正文；站内已迁移文章地址改为相对地址。多分类关系和父分类不会猜测映射。WordPress 修订未保存历史分类和标签，导入历史版本不会伪造这些信息。
