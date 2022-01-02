# WordWebCMS

<img src="WWC.png" alt="WWC" style="height:150px" />

WordWebCMS是一个简单的内容管理网站 (博客,文章等)
可以理解为一个简单版本的WordPress 虽然说功能更少 但是使用了C#!

详细文档参见 [WordWebCMS 帮助与支持文档](wwcms.exlb.org)

## 部署WordWebCMS

WordWebCMS暂时未开发完毕 不推荐进行部署 (虽然说你也可以自己改了直接发)见 [Project](https://github.com/LorisYounger/WordWebCMS/projects/1)

### 方法一 自动部署

1. 将 app.publish\* 内容上传至网站服务器
   <br/>*自己生成或见[releases](https://github.com/LorisYounger/WordWebCMS/releases)
2. 前往 setup.aspx 按步骤进行部署

### 方法二 手动部署

1. 运行 [setup.sql](WordWebCMS/setup.sql) (注:如果是共用用户数据库 不需要运行创建用户表 见[setup.sql](WordWebCMS/setup.sql#L7)里的详细注释
2. 修改 [Web.config](WordWebCMS/Web.config#L13) 中 connStr 和 connUsrStr 为自己的数据库连接方式 用户数据若使用相同数据库 则写一样的即可
3. 将 app.publish\* 内容上传至网站服务器根目录(不是根目录可能需要在设置内修改目录位置)
    <br/>*自己生成或见[releases](https://github.com/LorisYounger/WordWebCMS/releases)

## 项目优势

- Power By **C#**!  **C# No.1**
- 项目不是非常复杂 可以自己魔改后用在自己的网站 还可以手动增加功能或和自己的别的软件进行联动 All in One
- 支持MarkDown编写文章
- 支持多个网站共享用户,一次注册全WWCMS和兼容网站可用(需要使用相同的用户数据库,见[Web.config](WordWebCMS/Web.config#L15))
- 支持主题功能,并且主题兼容WordPress主题(css,但是还是需要自己改改适应WWCMS) 

## 例子 Example

* [WordWebCMS 帮助与支持文档](http://wwcms.exlb.org/)

  查看WWCMS相关文档,包括部署,写作说明,参数设置等


* [WWCMS测试网站](https://wwcms.exlb.org/test/)

  亲自尝试wwcms后台等功能 管理员账号 admin 管理员密码 WWCMSpassword

## 与WWCMS兼容的项目

* [Software Version Manager](https://github.com/LorisYounger/SoftwareVersionManager)

  软件版本管理器是一个让软件支持更新和激活检查的网站和类库  
  通过使用SVM,您可以很方便的通知用户升级他们的软件并告知升级项目  
  以及为您的软件设置激活码,并且可以随时撤销或修改激活信息  
  该项目兼容 [WordWebCMS](https://github.com/LorisYounger/WordWebCMS) 您可以共享用户信息从您的博客/论坛至软件版本管理器
