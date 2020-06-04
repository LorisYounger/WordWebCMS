#本sql是创建WWCMS所需的数据库
#创建完成后别忘了在web.config改设置

#创建设置表
CREATE TABLE `setting` ( `selector` VARCHAR(64) NOT NULL COMMENT '设置项目' , `property` TEXT NOT NULL COMMENT '设置内容' ) ENGINE = InnoDB COMMENT = '设置表';ALTER TABLE `setting` ADD PRIMARY KEY(`selector`);

#---注意:如果是和其他WWCMS使用相同的用户表 无需创建用户表

#创建用户表
CREATE TABLE `users` ( `Uid` INT NOT NULL AUTO_INCREMENT COMMENT '用户id' , `name` VARCHAR(30) NOT NULL COMMENT '用户名' , `email` VARCHAR(40) NOT NULL COMMENT '电子邮件' , `password` VARCHAR(32) NOT NULL COMMENT '密码md5s' , `isroot` BOOLEAN NOT NULL DEFAULT FALSE COMMENT '是超级管理员' , `money` INT NOT NULL DEFAULT '0' COMMENT '金钱' , `exp` INT NOT NULL DEFAULT '0' COMMENT '经验值' , `lv` INT NULL DEFAULT '1' COMMENT '等级' , `headport` TINYTEXT NULL DEFAULT NULL COMMENT '头像url' , PRIMARY KEY (`Uid`), INDEX (`name`)) ENGINE = InnoDB COMMENT = '用户表';

#创建第一个默认用户
#账号 admin 密码 WWCMSpassword    Md5s=>5b3bc38e260129ec8a2f478f64b6b4a7
INSERT INTO `users` (`Uid`, `name`, `email`, `password`, `isroot`, `money`, `exp`, `lv`, `headport`) VALUES (NULL, 'admin', 'admin@exlb.org', '5b3bc38e260129ec8a2f478f64b6b4a7', '1', '100', '0', '10', NULL);

#--------------------------------------------

#创建文章表
CREATE TABLE `wwcms`.`post` ( `Pid` INT NOT NULL AUTO_INCREMENT COMMENT '文章id' , `name` VARCHAR(100) NOT NULL COMMENT '文章名' , `content` MEDIUMTEXT NOT NULL COMMENT '内容' , `author` INT NOT NULL COMMENT '作者id' , `excerpt` VARCHAR(400) NOT NULL COMMENT '摘录/简介' , `postdate` DATETIME NOT NULL COMMENT '发布日期' , `modifydate` DATETIME NOT NULL COMMENT '修改日期' , `classify` VARCHAR(200) NOT NULL COMMENT '分类目录' , `state` TINYINT NOT NULL DEFAULT '0' COMMENT '文章类型' , `attachment` TINYTEXT NULL DEFAULT NULL COMMENT '附图' , `password` VARCHAR(32) NULL DEFAULT NULL COMMENT '密码md5s' , `anzhtml` BOOLEAN NOT NULL DEFAULT FALSE COMMENT '是否分析html' , `readers` INT NOT NULL DEFAULT '0' COMMENT '阅读量' , `likes` INT NOT NULL DEFAULT '0' COMMENT '赞同数' , PRIMARY KEY (`Pid`)) ENGINE = InnoDB COMMENT = '文章表';

#文章 HelloWord
INSERT INTO `post` (`Pid`, `name`, `content`, `author`, `excerpt`, `postdate`, `modifydate`, `classify`, `state`, `password`, `anzhtml`, `readers`, `likes`) VALUES (NULL, '你好,世界 又一个WWCMS站点',  '# 欢迎使用 WordWebCMS。\r\n这是系统自动生成的演示文章。编辑或者删除它，然后开始您的博客！\r\n\r\n# WordWebCMS支持Markdown\r\n**Markdown**是一种轻量级的「标记语言」\r\n**Markdown**是一种可以使用普通文本编辑器编写的标记语言，通过简单的标记语法，它可以使普通文本内容具有一定的*格式*。它允许人们使用易读易写的纯文本格式编写文档，然后转换成格式丰富的HTML页面\r\n\r\n# 关于WordWebCMS\r\n由[洛里斯杨远](https://zoujin.exlb.org)编写的一款内容管理网站\r\n项目地址:[https://github.com/LorisYounger/WordWebCMS](https://github.com/LorisYounger/WordWebCMS)\r\n', '1', '一篇系统生成的默认文章\r\n这是系统自动生成的演示文章。编辑或者删除它，然后开始您的博客！', NOW(), NOW(), '未分类', '5', NULL, '0', '0', '20');

#创建评论表
CREATE TABLE `wwcms`.`review` ( `Rid` INT NOT NULL AUTO_INCREMENT COMMENT '评论id' , `Pid` INT NOT NULL COMMENT '文章id' , `content` TEXT NOT NULL COMMENT '内容' , `author` INT NOT NULL COMMENT '作者id' , `postdate` DATETIME NOT NULL COMMENT '发布日期' , `modifydate` DATETIME NOT NULL COMMENT '修改日期' , `state` TINYINT NOT NULL DEFAULT '0' COMMENT '评论类型' , `anzhtml` BOOLEAN NOT NULL DEFAULT FALSE COMMENT '是否分析html' , `likes` INT NOT NULL DEFAULT '0' COMMENT '赞同数' , PRIMARY KEY (`Rid`)) ENGINE = InnoDB COMMENT = '评论表';

#评论 HelloWord
INSERT INTO `review` (`Rid`, `Pid`, `content`, `author`, `postdate`, `modifydate`, `state`, `anzhtml`, `likes`) VALUES (NULL, '1', '你好 我也觉得WordWebCMS最棒了\r\n#首先 评论也支持Markdown\r\n我觉得这点很棒\r\n\r\n我特别喜欢WWCMS,**你呢?**', '1', NOW(), NOW(), '7', '0', '10');

