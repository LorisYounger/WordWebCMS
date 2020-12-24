# WordWebCMS
WordWebCMS是一个简单的内容管理网站 (博客,文章等)
可以理解为一个简单版本的WordPress 虽然说功能更少 但是使用了C#!

# 部署WordWebCMS
WordWebCMS暂时未开发完毕 不推荐进行部署 (虽然说你也可以自己改了直接发)见[#TODO](#todo)
部署方法:
1. 运行 [setup.sql](WordWebCMS/setup.sql) (注:如果是共用用户数据库 不需要运行创建用户表 见[setup.sql](WordWebCMS/setup.sql#L7)里的详细注释
2. 修改 [Web.config](WordWebCMS/Web.config#L13) 中 connStr 和 connUsrStr 为自己的数据库连接方式 用户数据若使用相同数据库 则写一样的即可
3. 将 Release\* 内容上传至网站服务器根目录(不是根目录可能需要在设置内修改目录位置)
<br/>*需要自己生成

# 项目优势
- Power By C#! C# No.1
- 项目不是非常复杂 可以自己魔改后用在自己的网站 还可以手动增加功能或和自己的别的软件进行联动 All in One
- 支持MarkDown编写文章
- 支持多个网站共享用户,一次注册全WordWebCMS可用(需要使用相同的用户数据库,见[Web.config](WordWebCMS/Web.config#L15))
- 支持主题功能,并且主题兼容WordPress主题([css](WordWebCMS/Themes/Default/Style.css),但是还是需要自己改改适应WWCMS)

# TODO
见[Project](https://github.com/LorisYounger/WordWebCMS/projects/1)
- [ ] 实现登陆和注册
- [ ] 实现管理后台
- [ ] 实现写作后台
- [ ] 实现审核后台
- [ ] 实现搜索功能
