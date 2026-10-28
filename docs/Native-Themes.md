# 原生主题约定 v1

前台采用 [Fluid](https://github.com/sebastienros/fluid) 的 Liquid 模板。模板在运行时读取，不能执行 C#、加载 DLL 或直接访问数据库；后台始终使用编译期 Razor Pages 布局。主题作者可以控制前台 HTML、CSS 和 JavaScript，因此仅安装信任来源的主题。

## 包结构

ZIP 根目录必须直接包含下列文件，不要再包一层目录：

```text
theme.json
layout.liquid
list.liquid
post.liquid
page.liquid           可选，缺省使用内置页面正文模板
error.liquid
assets/
  site.css
  site.js
```

```json
{"id":"my-theme","name":"我的主题","version":1}
```

`id` 为 1–64 位小写字母、数字或连字符，首字符不能为连字符。`version` 是主题接口版本，当前为 1。`default` 由程序管理，不可上传覆盖；已安装 ID 不覆盖，更新主题使用新 ID，确认后在后台切换。

最多 200 个文件，ZIP 最大 20 MB、解压声明总大小最大 50 MB、单模板最大 256 KB。禁止绝对路径、父级路径、反斜线、重复文件及符号链接。`assets/` 只接受 CSS、JS、PNG、JPEG、GIF、WebP、WOFF2。路径区分大小写，扩展名使用小写；主题清单和模板不通过资源路由公开。

模板先渲染 `list` / `post` / `page` / `error`，再把结果作为 `body` 交给 `layout`。没有文件 include/load 扩展，也没有任意文件读取能力。所有普通值默认 HTML 编码；正文、评论及页面片段仅在注明的字段使用 `raw`。不要对标题、摘要、用户名、URL 或错误信息使用 `raw`。

## 所有页面共有的数据

| 值 | 含义 |
| --- | --- |
| `site.title`, `site.description`, `site.url` | 本站公开设置；另有 `site.avatar` 头像 URL |
| `title`, `description`, `canonical`, 可选 `cover` | 当前页面分享信息 |
| `theme_url` | `/theme-assets/<id>`，CSS 地址例：`{{ theme_url }}/assets/site.css` |
| `navigation` | 顶层项数组，每项为 `text`、`url`、`new_window`、`children`；子项也有 `text`、`url`、`new_window` |
| `signed_in`, `user_name`, `can_manage` | 当前请求登录状态、用户名与是否可进入管理后台 |
| `categories` | 分类数组，每项为 `name`、`url` |
| `recent_posts` | 最近公开的 5 篇文章，每项为 `title`、`url`；不包含草稿、撤下内容或独立页面 |
| `language`, `languages` | 当前 CultureInfo 名称；可切换语言的 `name`、`code`、`url` |
| `is_page`, `registration_open`, `user_avatar` | 是否独立页面、是否接受注册、当前用户头像 |
| `client_messages` | 使用安全 JSON 编码生成的 JS 界面文本，可在 application/json 数据节点中 raw 输出 |
| `csrf` | 当前请求防伪令牌，所有 POST 表单必须提交 |
| `body` | 仅 layout 使用：已渲染的页面片段，用 `{{ body \| raw }}` 输出 |

布局示例（完整可用样例见 `WordWebCMS/theme/default`）：

```liquid
<!doctype html>
<html lang="{{ language }}"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>{{ title }} · {{ site.title }}</title>
<link rel="stylesheet" href="{{ theme_url }}/assets/site.css">
</head><body><a href="/">{{ site.title }}</a>
<main>{{ body | raw }}</main></body></html>
```

## list.liquid

首页、分类/标签/日期归档及搜索共用模板。输入为 `heading`、`query`、`total`、`page`、`previous`、`next`、`categories` 和 `posts`。空分页链接是空字符串。

- `categories`：`name`、`url`。
- `posts`：`id`、`title`、`summary`、`url`、`cover`、`date`、`author`、`avatar`、`likes`、`views`。
- 首页只显示已公开文章；独立页面不出现在文章列表中，可通过导航访问。

搜索向 `/` 提交 GET 参数 `q`。归档参数为 `category`（分类 ID）、`tag`（标签 ID）、`year`、`month`；分页为 `p`。日期基于首次实际发布时间（UTC）。

## post.liquid

文章地址 `/<slug>`；`/p/<slug>` 永久重定向到该根路径。`post` 包含 `title`、`summary`、`html`、`url`、`cover`、`author`、`date`、`archive_url`、`views`、`likes`、`liked`、`category`、`category_url`。`post.html` 已经过统一文章允许列表，可使用 `raw`。

`tags` 数组包含 `name`、`url`。`comments` 数组包含 `id`、`parent_id`、`author`、`avatar`、`html`、`date`、`likes`、`liked`；`html` 由禁止原始 HTML 的 Markdown 管线和同一清洗器生成，可使用 `raw`。评论每页 30 条，分页链接为 `comment_previous` 和 `comment_next`。回复显示目标评论 ID，目标可能在其他分页；已删除目标的回复会转为顶层评论。

`comments_open`、`moderated` 表达当前评论规则，`notice` 是本次操作提示。UI 是否显示按钮不能代替服务端权限判断。

所有写入使用 POST：

| 地址 | 额外字段 |
| --- | --- |
| `{{ post.url }}?handler=Comment` | `body`、可选 `parentId` |
| `{{ post.url }}?handler=Like` | `liked=true/false`；评论点赞再传 `commentId` |
| `/Account/Logout` | 无 |

每个 POST 表单加入：

```liquid
<input type="hidden" name="__RequestVerificationToken" value="{{ csrf }}">
```

点赞是设置期望状态，不是盲目翻转；重复提交 `liked=true` 不会叠加数量。只允许登录且在本站未封禁的用户写入。

## page.liquid

独立页面专用，`is_page=true`。`post` 包含 `title`、`summary`、`html`、`cover`、`url`。不提供评论、点赞和文章元数据；布局应在 `is_page` 为 true 时省略侧栏并使用单栏。自定义主题未提供此模板时使用内置页面正文，外层仍用自定义布局。

## 翻译与链接

使用 `{{ '首页' | t }}` 翻译界面文本，勿翻译用户内容。语言包见 [自定义语言](Languages-and-Registration.md)。RSS、sitemap 和 `new_window` 导航项应添加 `target="_blank" rel="noopener noreferrer"`。

## error.liquid

提供 `code`、`message`、`title`，支持主题化的 403/404 等错误页。500 使用独立 Razor 错误页，避免数据库/主题故障导致错误页再次失败。错误页不包含异常堆栈。

## 内容允许列表

文章允许 `p br hr h1–h6 blockquote pre code strong em del s ul ol li a img table thead tbody tr th td`，属性仅 `href src alt title colspan rowspan`，URL 协议为 HTTP、HTTPS、mailto 及相对地址。没有脚本、iframe、内联 style、事件属性或 data URI。管理员文章也遵守此规则。

评论 Markdown 在解析阶段禁用原始 HTML，再执行同一允许列表。预览与公开文章使用同一 `MarkdownService`。图片地址和导航地址在保存时另做 Web URL 检查。媒体库支持 PNG/JPEG/GIF/WebP 和下载型 PDF，不允许 HTML/SVG 上传。
