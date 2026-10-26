# 当前工作状态

> 本文件是工作区的一页式入口，只保留当前事实和下一步。详细过程与证据仍进入`PortTraining/Docs/讨论记录/`。状态发生实质变化时更新，普通连续回合无需重复扫描历史文档。

## 当前工程

- 主开发工程：`D:\wuhanxiangmu\YardCraneSimulator`
- Unity版本：`6000.0.42f1`
- 外部参考工程只读使用：`D:\UnityWoke\2026\AnqiaoFangzhenURPV1.9\AnqiaoFangzhenURP`
- 场桥PLC边界：自2026-09-15起不再依赖`PortGatewayService`；只允许`YardCraneS7Integration.unity`中的唯一game4automation `S7Interface`直连PLC，其他场景不得建立S7连接。

## 当前功能状态

- 2026-10-26 GitHub全项目同步本地快照：目标为用户指定的`https://github.com/vvscxiaoxi22/wuhangangkoumoni`，origin已配置；本地快照提交为`4892d01`（14112个新增/更新文件），`1cfd481`保留远端初始化历史，允许正常推送而无需强推。版本树含16349个文件，覆盖各源工程、SDK、项目文档及现有可公开交付包；完整清单见`交付文档/GitHub同步/2026-10-26-同步文件清单.md`。两个大型港口场景已按具体路径纳入LFS，1358份LFS文件本地完整性与指针检查通过，普通Git最大blob为49.44MiB。排除Unity/IDE缓存、测试构建产物、运行成绩、LocalTrainingData及含Copilot会话的7z备份；两份任务State仅在提交版本清空姓名/学号，本机原文件保留为两处预期工作树差异。补充两个手写Harness项目文件的忽略例外，源文件未改；未启动Unity或将备份视作功能验收。Git客户端已完成用户设备授权，当前进行LFS及Git上传；远端推送和SHA核对尚未完成。
- 2026-10-22场桥故障资产同步：按用户要求，将当前`岸桥故障信息.asset`的全部64条、11类故障内容同步到`场桥故障信息.asset`，ID、标号、故障现象、故障点和分类逐条一致；本次实际更新场桥49条故障点地址。保存后核对64个ID及256个字段全部一致，场桥资产头、名称、脚本引用和meta保留，岸桥资产及其meta未变。仅修改场桥资产和本状态，不改Data脚本、UI或场景；数据静态核对通过，未启动Unity，实际页面显示待预览验收。
- 2026-10-22岸桥故障资产更新：以用户最新`C:/Users/yunti/Desktop/副本故障点(1)(1).xlsx`的`Sheet1!A3:D66`为准，展开分类合并单元格并更新`Assets/Data/岸桥故障信息.asset`。仍为64条、11类，ID对应表格序号1–64，标号继续等于ID；本次实际变更49条故障点地址，保留源表地址字符原文，故障现象和分类未变。保存后逐条核对全部256个字段及ID顺序通过，资产头、脚本引用、meta、源xlsx及场桥故障资产保持原样。仅修改岸桥资产和本状态，不改Data脚本、UI、场景或兼容JSON；数据静态校验通过，未启动Unity，实际列表显示待预览验收。
- 2026-10-15交付汇报规则补充：按用户要求，在根目录`AGENTS.md`协作要求中加入文件清单规则；每次任务结束，只要实际新增、修改或删除了文件，最终汇报必须逐项提供可点击路径及改动说明。本轮仅修改根目录`AGENTS.md`和本状态文件。
- 2026-10-15学生端故障分类报错修复：针对`YC_STUDENT_SIDE_SHELL_MISSING_ASSET`中场桥ID17“导板机构”不受支持的问题，将学生端和存在相同限制的教师端场桥第5类由“俯仰机构”改为当前资产实际使用的“导板机构”；岸桥学生端沿用既有11张分类卡片与11类目录。两端类别统一为电源故障/起升机构/大车机构/小车机构/导板机构/前后倾机构/平面回转机构/左右倾机构/夹轮器/吊具泵/元器件损坏，当前场桥、岸桥资产均为64条，分类计数均为2/4/5/5/5/3/3/3/3/1/30；据当前资产和用户日志追加更正此前场桥100条及俯仰分类的旧基线，不改资产、故障ID或点位。既有StudentHome Harness同步当前场桥64条标准，并增加师生代码分类与两份资产逐条兼容性检查；本轮325项/2失败，11类卡片、两机型128条记录及师生分类兼容检查均通过。未完成编译：脚本指定的`D:/App/6000.0.42f1/Editor/Data/DotNetSdkRoslyn/csc.dll`不可用；另一失败为既有StudentHome岸桥目标场景名配置，未扩改场景。工程存在`Temp/UnityLockfile`，未启动Editor或操作用户现场；仅改两份Shell、既有Harness与本状态，代码实现完成/Unity实际载入及分类点击运行待验证。
- 2026-10-12教师端与学生端合并操作手册：在保留教师专用v1.4原文件的基础上，新增独立v2.0合并版Word/PDF（`交付文档/软件操作手册/岸桥场桥集装箱起重机仿真实训系统 操作手册 教师端与学生端`），共48页/35章。第1–12章保留教师登录、任务组卷、故障发布和成绩统计；第13–30章依据《学生端控制.docx》及当前学生端行为补充登录、学习训练、单项/联动/作业/能力提升、故障模拟、自由练习、任务考核、完成结果、6项扣分参考和操作台说明；第31–35章共用岸桥/场桥完整目录。合并版嵌入22张真实截图并设置66个可编辑Word圆形步骤编号，原始图片字节不变。自动检查确认35项目录页码、95组实训目标、128条故障记录、教师原37张表及章节正文保留；明确教师考核仅作用于学生“任务考核”，岸桥与场桥的项目/目标差异、过桩8m口径及现有场桥故障数据限制。独立隐藏Word实例导出PDF，bundled Poppler渲染48页逐页目检，无空白页、截断或遮挡；仅新增合并手册及临时制作/校验文件并更新本状态，不修改代码、场景、UI、Data或成绩。
- 2026-10-12教师手册步骤标识补回：教师专用手册升级为v1.4，在8张真实界面截图上分别添加1、2、3，共24个可编辑Word圆形编号，并配8组图下操作指引；编号对应登录、首页入口、任务创建与勾选、故障组题、成绩查询、日历与详情操作。原始8份截图嵌入字节SHA256一致，95行实训目标和128行故障目录校验保持通过。独立隐藏Word实例同步保存Word并导出PDF，仍为29页/17章；全部页面重新渲染，9页变更逐页目检，其他20页与v1.3像素一致，目录页码、编号位置和正文分页通过检查。更新同名交付Word/PDF，v1.3已临时备份；仅改手册、临时制作/校验文件和本状态。
- 2026-10-11教师手册实图替换：按用户提供的8张截图，将教师专用手册升级为v1.3，图1–8依次为登录、教师首页、任务管理、新建训练任务、故障发布、考核统计、日期筛选、考核详情；原图无裁改嵌入Word，逐一SHA256确认8份嵌入文件与用户原图一致，日期弹窗按比例以3.3英寸单独排版。移除示意图/演示数据说明，任务管理图移至组卷规则章节，列表字段说明移至管理条目章节，使图号按用户顺序排列；完整实训与故障目录保留。独立隐藏Word实例导出PDF，共29页/17章，95行目标、128行故障及自动目录校验通过；重新渲染全部页面，10页变化均逐页目检，其余19页像素与已验收v1.2一致，无截断或遮挡。更新同名交付Word/PDF，v1.2保留于临时备份；仅改手册、临时制作/校验文件和本状态，未修改软件代码、场景、UI资产、Data或成绩。
- 2026-10-11教师端操作手册详细版：按用户最新范围新增教师专用v1.2 Word/PDF（`交付文档/软件操作手册/岸桥场桥集装箱起重机仿真实训系统 教师端操作手册`），29页/17章/8张编号示意图，删除学生登录、学生页面操作及任务State说明；细化教师登录、返回、单项创建、目标/时长/模式、勾选组卷、故障多选与明细、成绩日历/状态/关键词/刷新及维护。实训目录完整列出岸桥16项46方案、场桥17项49方案，目标全文和默认标记95行逐条一致；故障目录完整列出当前两资产各64条，128行ID/现象/点位逐条一致。更正旧状态计数：本轮当前场桥资产已为64条且与岸桥完全相同，ID17–21标为导板机构，与场桥代码要求俯仰机构不一致，静态判断会导致目录校验失败；手册明确标为场桥待核对数据参考，未擅改分类或资产。独立隐藏Word实例更新目录并导出，bundled Poppler渲染29页全部逐页目检；修正第20页装船重复方案的内集卡说明后重新导出，像素比对仅第20页变化并复核，目录/表头/中文/配图无截断。仅新增教师手册、临时制作/校验文件及本状态，保留上一版，不修改代码、UI、场景、Data或成绩，不启动Unity；配图均明确标注操作示意与演示数据。
- 2026-10-11操作手册图文版：登录页面与教师端手册升级为v1.1，Word/PDF均为A4共13页/11章，补充8张带编号的操作示意图及港口封面，统一标题、步骤、表格、图注与分页，更新自动目录页码；教师端返回说明同步为子页面逐级返回、首页返回登录。当前缺少与最新版一致的真实运行截图，所有界面配图明确标注“操作示意图·演示数据”，不作为Unity Play截图。使用独立隐藏Word实例更新目录、保存并导出PDF，bundled Poppler渲染13页并逐页目检，目录、中文、表格、图片及结构检查通过。仅更新交付Word/PDF、临时制作与检查文件及本状态，不修改业务代码、UI资产、场景、Data或成绩，不启动Unity；学生端与实训场景章节留待后续编写。
- 2026-10-11教师顶部返回分层导航：`YardCraneTeacherSideShell`顶部返回按钮保持各页面可见，改为按当前页面处理；任务管理/考核统计返回教师首页，故障发布返回任务管理，首页才通过既有可配置`LoginSceneName`返回登录。故障发布页内返回按钮同步返回任务管理；分发器优先关闭已打开的日历、任务编辑、故障明细或成绩明细，首页重置时补充关闭故障明细，避免残留状态。仅修改教师端C#与本状态，不改场景、UXML/USS、Data或登录脚本。Unity6000.0.42f1真实引用聚焦编译在`Mode_Changqiao`与`Mode_Anqiao`均通过；既有TeacherHome静态Harness384项/3失败，均为当前场桥故障资产数量、ID及兼容JSON一致性校验，与本次返回逻辑无关。工程存在`Temp/UnityLockfile`，未启动第二个Editor或操作用户场景现场；实现及编译完成，实际逐级返回与首页登录切换Play待验收。
- 2026-10-11软件操作手册首版：在`交付文档/软件操作手册/`新增《岸桥场桥仿真实训系统操作手册 登录页面与教师端》Word及PDF，共12页/11章，覆盖学生与教师登录、教师工作台、实训单条创建与目标选择、任务勾选组卷、跨类别故障多选合并一条、故障明细、考核统计、日历与关键词查询、成绩详情/排序/刷新、本机数据位置及常见问题。以当前登录/教师/学生考核代码、UXML及题库Store只读核实，明确教师试卷仅作用于任务考核、学生重进模块读取最新试卷、不同电脑或安装目录不自动同步、教师题库与学生试卷分发区别、故障建议时长当前不写入限时，以及成绩卡片与列表筛选口径。Word后台独立实例导出PDF，12页全部渲染并逐页检查，目录页码、表格和中文显示通过；默认文档渲染器因未配LibreOffice不可用，采用Word导出与bundled Poppler复核。只新增文档及更新本状态，不修改软件代码、UI、场景、Data或成绩文件，不启动Unity；手册内容完成/实际软件操作未在本轮重新Play验收。后续继续学生端与实训场景操作章节。
- 2026-10-11学生故障列表滚动条：StudentHome学习训练→故障排除详情的纵向滚动条由`AlwaysVisible`改为`Auto`，内容未溢出时隐藏，横向滚动条明确设为`Hidden`。仅在`.fault-table-scroll`范围复用教师端青色圆角样式：12px操作区域、8px滑块/深蓝半透明轨道、悬停与拖动高亮，隐藏默认箭头、拖动边框及横向滚动条；未强制纵向scroller的display，保持Unity自动显隐。仅修改学生UXML/USS、既有静态检查及本状态，共4个文件，不改C#、Data、场景或其他列表。Auto/Hidden容器契约、USS语法/资源路径及空白检查通过，完整静态Harness312项/5失败，仍为场桥故障数据校验4项及岸桥目标场景名1项。本轮未编译未改动C#、未启动Unity；少量/大量故障的自动显隐、滚轮/拖动及样式效果待Unity预览验收。
- 2026-10-11学生学习训练故障列表四列：仅调整StudentHome的学习训练→故障排除详情表，表头及运行时每行依次为“序号、故障点、故障现象、分类”；序号使用故障真实ID，分类使用当前目录类别名称。移除线号/标号列、原理图/故障位置图/操作指导按钮及其未提供附件提示和专用样式，故障点/现象两列分配剩余宽度；保留类别筛选、上下滚动、返回及模拟故障练习入口，未改任务State交接、数据资产、教师端或场景文件。仅修改学生首页C#、对应UXML/USS、既有StudentHome静态检查及本状态，共5个文件。Unity6000.0.42f1真实143个引用/13份源码在Mode_Changqiao与Mode_Anqiao均编译通过，四列表头、滚动容器及相关UI静态契约通过，空白检查通过；完整Harness318项/5失败，剩余为当前场桥故障资产数量/分类/ID校验4项及StudentHome岸桥目标场景名校验1项，未扩大修改这些数据或配置。未启动Unity或操作用户场景现场；实现完成/四列实际画面、列宽与滚动Play待预览验收。
- 2026-10-10岸桥故障标号更正：按用户确认，岸桥故障表的“标号”就是故障ID；更正先前因源表无独立标号列而留空的约定。`Assets/Data/岸桥故障信息.asset`全部64条标号现为对应ID字符串`1`–`64`，兼容`StudentFaultCatalog.json`仅同步岸桥64条`lineTag=number`。逐条校验64个ID与标号一一一致，屏蔽标号字段后资产与修改前完全相同，JSON岸桥其他字段及场桥段原文未变，两份meta哈希未变，`git diff --check`通过。未修改Data脚本、UI、场景或场桥资产，未启动Unity；数据静态校验通过，Unity实际显示待预览。
- 2026-10-10师生端UI图片移植适配：统一处理登录、StudentHome、TeacherHome六份UXML及五份USS，将20处图片`resource()`引用替换为包含PNG扩展名的相对`url()`，六处样式表引用改为同目录USS文件名，减少对固定Resources路径及UXML内旧GUID字符串的依赖；保留原有图片、meta、节点、布局及业务C#，共享背景继续复用学生端原图，不复制素材。两份现有UI Harness同步识别相对图片/样式表引用。静态核对六份XML解析、五份USS括号及全部20处图片/六处样式表引用通过，涉及15张不同PNG，文件与meta均有效。本次完整静态Harness实际结果为Student312项/6失败、Teacher384项/3失败：图片和样式表相关项均通过，其余失败集中在当前场桥故障资产记录/分类/ID、与兼容JSON一致性、StudentHome场桥资产绑定及既有岸桥场景名配置；与上条先前运行的Harness结果不同，按本次证据追加记录，未修改这些数据或场景。未启动Unity、未编译业务C#、未执行真实导包或接收工程导入；实现完成/跨工程图片显示待验收。移植仍应完整导入依赖图片并保持相对目录结构，推荐现有排除Build Profiles的完整UnityPackage导出菜单。
- 2026-10-10场桥/岸桥故障目录统一11类：以`C:/Users/yunti/Desktop/故障点(3).xlsx` Sheet1 A3:D66替换`Assets/Data/岸桥故障信息.asset`全部旧记录，共64条，ID严格对应表格序号1–64；合并分类单元格按范围展开，故障点/现象/分类逐字段一致，源表没有标号列，资产标号写空字符串。岸桥11类为电源/起升/大车/小车/导板/前后倾/平面回转/左右倾/夹轮器/吊具泵/元器件损坏，计数2/4/5/5/5/3/3/3/3/1/30。当前场桥资产实际为100条ID1–100（不是旧状态中的90条），保留全部现象、故障点、标号和ID，仅拆分旧合并类别并将明确写“元器件损坏”的68–100归入独立类别；场桥第5类按设备语义为俯仰机构，其余类别与岸桥一致，计数12/10/7/6/8/5/4/6/3/6/33。师生脚本按Build Profile机型选择各自11类；学生学习训练使用11个静态UXML卡片，运行时只填充当前机型类别、数量与说明，三行布局已压缩；教师端沿既有运行目录列表展示11类，教师UXML未改。兼容JSON缓存同步为同一164条结果，`StudentHome.unity`仅修复场桥故障资产已失效的旧GUID引用，不改结构或UI；Data脚本未改。Unity6000.0.42f1真实143个Managed引用/13份源码在Mode_Changqiao与Mode_Anqiao均编译通过，TeacherHome静态Harness378项/0失败；StudentHome相关307项中本轮资产、11类、UXML/USS及场桥绑定全部通过，仅保留既有`QuayCraneSceneName`仍序列化为`YardCrane`的1项失败。未启动Unity，11类实际画面、滚动和点击进入详情Play待验收。
- 2026-10-09登录系统标题保留UI设置：仅删除YardCraneStudentShell对systemTitle.text的机型动态赋值，让登录页标题沿用UI资产设置；其他机型文案、英文标识与登录行为保持原有实现。仅修改登录脚本及本状态，不改UXML、场景或Data。Unity6000.0.42f1真实引用编译Mode_Changqiao/Mode_Anqiao均通过，未启动Editor；标题实际显示待UI预览验收。
- 2026-10-09教师目标编号隐藏与成绩倒序：教师任务目标下拉框通过Unity原生列表/选中值格式化回调隐藏“01 · /02 · ”前缀，提示文本同步隐藏；内部唯一选项与targetId继续保留，兼容场桥已有同描述目标。成绩文件原已按日期文件名倒序，现每份JSON的data数组改为从末条向前读取，使同一天后追加的成绩在前；现有JSON没有逐条时间戳，不增加虚构时间或改写成绩文件。仅修改YardCraneTeacherSideShell.cs及本状态。Unity6000.0.42f1真实引用编译在Mode_Changqiao/Mode_Anqiao均通过，TeacherHome静态Harness378项/0失败；反射执行实际C#格式化及题目快照方法确认编号不显示、原文/占位保留、同描述目标ID仍独立。实际展开/选择及成绩显示顺序Play待验收，未启动额外Editor或修改场景、Data、UXML/USS。
- 2026-10-09教师下拉展开菜单样式：补充原生GenericDropdownMenu的独立菜单节点样式，使用深蓝背景、青色描边、圆角选项、悬停/选中高亮及细青色滚动条；选项文字与任务参数字号一致，保留原生选中勾号。菜单选择器位于教师专用USS，未以DropdownField后代限定，避免菜单挂载到文档根节点后无法匹配；列表高度最多360px，更多目标沿原生滚动浏览。仅改教师USS及本状态，不改C#或任务数据。实际Unity6000.0.42f1程序集确认8个原生菜单样式类，TeacherHome静态Harness378项/0失败；展开、选中及滚动画面待Unity预览验收，未启动Editor或编译C#。
- 2026-10-09教师任务目标下拉图标：仅在教师USS中为任务目标DropdownField右侧原生箭头节点添加青色向下折角，并为目标文本保留收缩空间，沿用现有输入框高度与配色，不改C#、UXML或Data。实际Unity6000.0.42f1程序集确认箭头样式类为`unity-base-popup-field__arrow`，TeacherHome静态Harness378项/0失败；图标实际画面待Unity预览验收，未启动Editor或编译未改动的C#。
- 2026-10-09教师与学生训练目录目标对齐：核对当前Data资产，岸桥16个项目/46个目标选项、场桥17个项目/49个目标选项；两端共享目录读取和分类，教师端项目未漏载，但原只读目标栏及新建时固定targetId=0，导致岸桥30个、场桥32个其他目标无法选用。现教师创建/编辑弹窗使用下拉框展示所选项目全部目标，按原资产索引保存targetId、目标说明及3组字段，编辑恢复原选择，切换新建项目重置目标；仍每次生成一条任务。选项加序号区分场桥两组已有同文案目标，保留其独立ID，不改Data脚本/资产、学生端、场景或试卷格式。仅修改教师脚本、对应UXML/USS及本状态。TeacherHome静态Harness378项/0失败，Unity6000.0.42f1真实143个Managed引用/13份Shell与Editor源码在Mode_Changqiao和Mode_Anqiao均编译通过；反射执行实际C#目标选项与创建/编辑快照方法，95个目标在本地/远程两种模式下编号、说明及3组字段全部一致。实现完成/下拉交互、发布后学生选题和任务State交接Play待验收；当前UnityLockfile存在，未另开Editor或保存用户现场。
- 2026-10-09模式切换布局跳动修正：用户反馈切换本地/远程后下面训练区高度变化。静态定位为上一轮成功回调调用`SetStatus`，把`learning-launch-status`从display:none改为flex；该标签位于正常布局中，最小高度18px及上边距14px占用了`learning-main`空间，导致flex训练行被挤压。现仅删除`SelectLearningScope`成功路径的提示文本更新，以按钮互斥高亮反馈选择，保留模式1/2立即写入及进入训练时沿用模式；失败提示继续保留。只改学生首页脚本，不改UXML/USS、Data或桥接器。两套机型Unity6000.0.42f1真实引用编译通过；正常切换高度稳定的实际画面Play待用户复验。
- 2026-10-09学习训练本地/远程模式：`YardCraneStudentSideShell`学习训练页点击本地/远程按钮时，立即通过`YardCraneLegacyTaskAssetBridge.TryWriteTrainingMode`向所绑定的`taskStateAsset.实训模式`写入1/2，并更新互斥高亮；远程按钮原“尚未接入、保持本地”的占位提示已移除。桥接方法只校验和写入模式字段，不覆盖身份、任务、故障ID或成绩；绑定/字段不正确时保留原高亮并提示失败。训练项目创建状态时沿用当前选择，故障学习训练在完整状态写入后补写当前模式，避免被旧本地默认值覆盖；自由练习和教师考核模式逻辑未改。编辑器预览只切换外观，不写Data资产。仅修改学生首页脚本及既有桥接器，未修改Data脚本/资产、场景或教师端。Unity6000.0.42f1真实引用编译，Mode_Changqiao/Mode_Anqiao均通过，源码空白检查通过；实现完成/按钮点击及切场景后模式保留Play待验收，保留已有Editor现场。
- 2026-10-08跨工程场景名兼容：按用户要求移除`YardCraneTeacherSideShell`和`YardCraneStudentSideShell`内10处当前场景名与固定`SceneName`的比较，覆盖OnEnable/Start/Update及编辑器预览入口；删除学生首页OnEnable中已注释的旧比较。`YardCraneStudentShell`原先已没有当前场景名判断，本轮无需修改。保留公共SceneName常量作为现有工具及默认配置的兼容接口，实际跳转继续使用Inspector配置的目标场景名和构建可加载校验，不改场景、UI或Data。复用既有CompileShell，Unity6000.0.42f1真实143个Managed引用、13份Shell/Editor源码，Mode_Changqiao与Mode_Anqiao均编译通过；三个指定脚本当前场景名判断均已不存在，修改源码无行尾空白。实现完成/改名场景Play待验证；当前有UnityLockfile且无可用Unity控制接口，未启动额外Editor或保存现场。
- 2026-10-02日历记录标记：有成绩记录的日期数字下方显示青色小圆点，选中日期时使用深色圆点保持可辨识。教师端每次打开日历均从全部成绩记录刷新日期集合，不受当前日期/搜索筛选影响；空文件、无记录日期不标记，切换月份由日历重绘对应标记。`DatePicker.SetMarkedDates`按日期去重，标记不改变选中值或触发查询；圆点忽略鼠标拾取，不遮挡日期点击。仅修改日历控件、来源说明、教师脚本及教师USS；场桥/岸桥Unity6000.0.42f1真实引用编译通过，样式结构与增量空白检查通过。实际小圆点位置及鼠标交互Play待验收，未启动额外Editor。
- 2026-10-02考核统计日历接入：`YardCraneTeacherSideShell`版本`v27-calendar-score-query`将此前日期下拉菜单替换为One Mechanic Games的MIT UI Toolkit DatePicker（固定源码提交`03cfd1e4e6fd5d011164ed992e88cb971635a2c2`，保留许可证）。日期按钮打开独立日历层，可切换年月、点日期立即查询、查询今天及恢复全部日期；取消/遮罩/Escape关闭不改变筛选，手输日期回车仍保留。日历采用教师端蓝色样式；成绩列表及扣分明细滚动条改为细圆角青色滑块，隐藏默认箭头与边框。插件以3份Runtime源码加入Shell程序集，适配Unity6000.0 UxmlFactory、中文标签及无外部图片的箭头，修复年月空选择与滚动误关闭；既有编译Harness仅递归包含Shell源码。岸桥/场桥两套Unity6真实引用编译通过，TeacherHome静态检查378项/0失败，实际插件日期网格84个月及2项空选择测试通过。没有改动场景、Data脚本/资产或成绩JSON；日历点击、年月列表滚动和整体画面Play待验收，保留用户Editor现场。成绩全量8条（含2条学号111）展示原则继续有效。
- 2026-10-02日期选择反馈修复：用户反馈点击统计日期右侧`▦`没有反应，原按钮仅查询输入文本，空文本会重复显示全部且不弹出选择。`YardCraneTeacherSideShell`现通过Unity原生`GenericDropdownMenu`列出“全部日期”、已记录日期（去重且倒序），选中后立即读取/筛选；已输入文本也可在菜单中查询，或按Enter/小键盘回车提交，键盘回调配对解绑。日期解析抽为同一函数，接受`yyyy-MM-dd`/`yyyyMMdd`，非法日期保留原列表并提示。只改教师脚本；两套机型Unity 6 Roslyn编译通过，实际C#日期规范化/非法日期/选项排序去重测试通过：当前2026-10-01及2026-09-30各4条，全部8条。菜单显示/鼠标选项/回车Play仍待复验，未改变Data、JSON、场景或页面样式。
- 2026-10-02成绩全量记录更正：用户明确要求`111`等记录也应显示，已取消`YardCraneTeacherSideShell`对空项目/空用时/0分记录的过滤，覆盖此前“排除6条空记录”的默认假设。所有非null记录按文件日期逐条保留，信息不完整的6条显示0分及“待复核”；当前全部日期显示8条，其中学号111在两份日期文件各有1条。只改教师脚本，不改UI、场景、Data或JSON；两套机型Unity 6 Roslyn编译通过，实际C#映射测试确认8条记录、2条111、6条待复核，Unity显示待复验。
- 2026-10-02考核统计空白修复：用户运行反馈点击后无UI，核实`teacher-assessment-page`及`teacher-assessment-detail-overlay`原先嵌套在`teacher-task-page`内，打开统计时隐藏任务管理会同时隐藏二者。现仅把这两个UXML节点移到`teacher-side-shell`直接子级，保留所有节点内容、样式和数据逻辑；新增两项独立父级回归检查，TeacherHome静态Harness为337项、0失败。此前335项通过仅证明当时已覆盖的静态契约，不覆盖此页面层级问题。此次未修改C#、Data、JSON或场景；显示、返回及详情弹窗Play仍待用户复验。
- 2026-10-02教师考核统计：`YardCraneTeacherSideShell`版本`v26-score-json-statistics`改为读取`Application.streamingAssetsPath/成绩/*.json`的`data`数组，展示姓名/学号/班级/专业/项目/用时/成绩及全部扣分明细；不再用旧`TrainingScores`来源或演示记录填充。日期由`yyyyMMdd.json`文件名取得，默认全部日期，支持`yyyy-MM-dd`/`yyyyMMdd`查询及姓名/学号/项目筛选；成绩与扣分列表沿用原有UI样式并支持滚动，不再限4条记录/3条扣分。默认暂排除空项目、空用时、0分且无有效扣分明细的记录，等级暂按90分优秀/60分合格，只用于展示和统计，不修改原始成绩；这两项已向用户询问，尚未收到确认。当前2个文件8条原始记录按默认处理显示2条80分考核、排除6条空记录。两套机型Unity 6 Roslyn编译通过，TeacherHome静态Harness335项全通过；反射执行实际C#映射及0/60/90/越界分数边界通过。未改Data、成绩JSON、场景；Unity现有Editor现场保留，JSON在Unity中的解析、滚动/日期/详情Play待验收。
- `StudentEntry`：学生身份登录与本地教师`admin/admin`入口已实现。
- 2026-10-01学生身份写入：`YardCraneStudentShell`新增Inspector“任务状态”引用；学生登录校验通过后通过既有Data桥接器把`profile.StudentNumber`写入`任务State.学号`、`profile.DisplayName`写入`任务State.姓名`，成功后才进入学生首页。身份字段单独校验与写入，不参与教师试卷字段列表；教师登录和既有训练内容写入均不覆盖学生身份。场桥/岸桥两套Unity 6 Roslyn编译通过，Harness唯一既有失败仍为StudentHome岸桥场景名配置。本轮没有改场景或Data脚本/资产；需在StudentEntry组件“任务状态”中绑定与StudentHome相同的`Assets/Data/任务State.asset`并保存，登录Play待验收，版本`v33-student-task-state-identity`。
- `StudentHome`：学生首页、学习训练、自由练习、任务考核三个入口及Display2已形成当前实现链。
- `TeacherHome`：使用教师端独立Display1/Display2 UIDocument，不与学生端共用文档；实训项目每次单选一个并生成一条任务，故障目录允许跨类别多选，一次把整组故障生成一条“故障排除”任务。故障任务行显示“故障 N 条”并提供“查看故障”明细按钮；每条任务行首勾选框决定整条任务是否纳入当前发布试卷。题库和当前试卷按Build Profile机型分别写入`LocalTrainingData/TeacherTasks/yard-*`或`quay-*` JSON。故障目录继续按`Mode_Anqiao`/`Mode_Changqiao`读取对应`Assets/Data`资产，两套目录均完整分入11类（岸桥64条、场桥100条、待归类均为0）。直接播放`TeacherHome`仍自动建立`admin/admin`教师身份，顶部“返回登录”按钮保留且登录场景名可配置。
- 多题考核闭环（2026-09-20最新）：当前发布载体已改为机型独立的`assessment-question-bank.json`与`assessment-paper.json`；不再把`Assets/Data/Resources/试卷类型.asset`作为本轮发布或学生选题链的读写目标。学生点击“任务考核”先进入当前试卷题目列表，选择一道题后才启动训练场景：实训题把该题完整快照直接写入`任务State.asset`，一条故障任务会把该组全部故障ID写入`任务State.asset`的故障内容，随后直接加载对应场桥/岸桥场景。故障题时长固定为600秒；完成并返回学生首页后可再次进入任务考核继续选题。学生可读的paper格式已升级到v2，写出时逐条清空故障答案字段`faultPoint`/`lineTag`，并会自动把已有v1文件升级重写为安全的v2。
- 训练任务目录与设备配置（2026-09-21）：`StudentEntry`不再让学生选择或在Inspector勾选场桥/岸桥；产品机型改由当前Build Profile的脚本宏决定：`Mode_Anqiao`映射岸桥，`Mode_Changqiao`映射场桥，二者同时定义会编译失败，均未定义时为兼容旧导入默认岸桥并在Play时警告。`SelectedCraneKind`在类型初始化时即取该构建配置，学生端和教师端据此读取对应任务与故障资产。两端场景仍序列化绑定场桥任务资产（17项：4/4/4/4/1）和岸桥任务资产（16项：5/4/2/4/1）；学生选择任务后会在加载前把任务字段写入`任务State.asset`运行时实例。`StudentHome`三个训练入口继续直接执行`LoadSceneAsync`：场桥默认进入`YardCrane`，岸桥代码默认进入`港口场景`，不再绕行`Bootstrap`。为补齐直接加载后的HUD返回链，发起训练时会创建只负责会话与页面回传的持久导航桥；该桥不要求`Bootstrap`存在，返回时回到`StudentHome`对应的学习训练、自由训练或任务考核页面，并清理本次会话。外部实训工程即使只用普通`LoadScene`返回`StudentHome`，首页也会先从旧会话恢复学生身份，再于首帧释放旧桥、会话和训练身份，避免第二次启动被“已有训练会话”拦截。两种宏均通过Unity 6 Roslyn编译；真实Play返回点击仍待接收工程验收。
- `StudentHome`岸桥场景配置待处理（2026-09-20最新静态证据）：代码字段默认值仍为`港口场景`，但当前已保存的`StudentHome.unity`在`QuayCraneSceneName`上序列化覆盖为`YardCrane`，因此最新`StudentHomeUiHarness`为`307/308`通过并只失败该项。Unity当前存在`Temp/UnityLockfile`，本轮没有从外部覆盖可能未保存的场景现场；需在Editor确认岸桥目标后保存场景再复跑Harness。
- 登录场景跨工程交付（2026-09-20）：接收工程`AnqiaoFangzhenURP`实际使用Unity `6000.0.16f1`，导包触发的`BuildProfileContext.OnDisable`报错属于Unity 6早期版本的自定义Build Profile持久化问题；该工程两个自定义Profile还覆盖了`Active Input Handling`，与官方同类问题触发条件一致。`YardCraneBuildAutomation`已移除每次脚本域重载反复改Build Settings的行为：五个必需场景实际导入/移动时增量登记，新后处理器首次随包导入时另在每个Editor会话的第一次域重载执行一次兜底检查；登记目标改为`EditorBuildSettings.globalScenes`，避免修改当时激活的自定义Profile，并保留手动`Register Required Scenes`入口。新增`Tools/PortTraining/Yard Crane/Export UnityPackage Without Build Profiles`，从`Assets/YardCraneSimulator`收集资产依赖并明确排除`Assets/Settings/Build Profiles/**`；接收工程仍须预先具备工程使用的Unity Package Manager/第三方代码依赖。静态Harness `331/331`及Unity `6000.0.42f1` Editor程序集聚焦编译通过。接收工程当前仍有`Temp/UnityLockfile`，为保护现场未外部删除其`Library/BuildProfileContext.asset`或`Library/BuildProfiles`；关闭编辑器、统一升级到`6000.0.42f1`并清理这两个生成缓存后的真实重导入仍待执行。
- 故障目录数据旧基线（2026-09-19，已由2026-10-10条目取代）：当时`StudentFaultCatalog.json`为190条固定8类结果；其中按相邻回路暂归类的元器件记录及旧源表错位内容不再代表当前岸桥64条最新表格结果。
- `YardCrane`：作为正式驾驶场景，场景内UIDocument与双相机由场景持有；HUD、训练上下文与返回路径已有实现。
- `YardCraneS7Integration`：独立S7联调场景已建立；只承载game4automation控制器、唯一S7接口、点位和安全适配器，不承载场桥模型或训练UI。接口默认禁用，尚未连接真实PLC，也未加入正式五场景Build Settings。
- 第三方脚本导入兼容（2026-09-19）：补充`SerializedCollections`缺失asmdef情况下对`Assembly-CSharp-Editor-firstpass`的内部访问授权，并把Crest的`EditorHelpers.cs`移出Unity特殊`Editor`目录以恢复`OceanRenderer`引用；Unity实际刷新后四个预定义程序集编译成功，原9条CS0122/CS0117/CS0103已清零。大字典资产导入后又暴露Drawer对空/未就绪数组越界，已补`ListProperty`有效性和`arraySize`保护并用原Bee响应文件聚焦编译通过。Crest仍有Unity 6弃用警告，SerializedCollections导入包仍缺原版`Editor/Assets`资源。

## 当前活动任务

### HikvisionFourDisplay四路摄像机与四屏显示

- 2026-10-07方案删除：配置面板中每个方案右侧增加“删除”按钮；按ID重新读取并原子保存方案列表，可删到空列表，缺失ID不写文件，损坏文件拒绝删除。删除后刷新方案按钮，若删除当前方案则清除UI及feeds.json的activeSchemeId；保留四路连接/方向/Canvas配置和安全凭据，不调用重连。Runtime与现有Editor验证入口真实引用编译0错误（仅既有MockPatternDecoder事件警告），新增删除持久化/其他方案保留/损坏拒绝/活动方案不重连的现有Harness用例。已同步`Builds/2026-10-06`Windows包的运行程序集并校验复制，未改用户方案/IP文件；当前Unity Editor打开，未额外启动Unity或执行Harness/Play，实际删除、重启后列表持久化和画面连续性待验收。
- 2026-10-07方案按钮显示范围：删除Display1左上角的快捷方案区，方案按钮仅在“视频源配置”面板内显示；关闭面板后只保留配置入口。仅修改`FeedConfigurationPanelUI`显示入口及产品说明；Unity6000.0.42f1 Player真实引用编译0错误（既有MockPatternDecoder事件警告）。已校验程序集身份与复制结果，同步到`Builds/2026-10-06`现有Windows包，保留其方案/IP/方向配置；未全量重建包。Unity Editor正在打开，未启动额外Editor或Play，实际关闭面板后的画面待复验。
- 2026-10-06新增目录、方案及旋转：新增12路现场摄像头选择项（`.200/.202/.203/.204/.201/.211～.216/.100`）；共同登录凭据已写入当前Windows用户凭据管理器，目录/方案/发布包仅保存引用。配置面板可按名称保存四路草稿为方案并生成按钮，同名更新、点击应用，最后配置冷启动恢复；切换只重连连接参数变化的画面。每路左/右90°即时旋转并持久化，不重连；横竖画幅保持比例，默认/故障图保持正向，唯一Canvas目标屏一同入方案。
- 2026-10-06验证与包：Unity6000.0.42f1正常UPM加载后的聚焦验证274项通过（含真实UI保存/应用/左右旋转入口，假解码器，无网络），完整Windows构建成功，产物为`HikvisionFourDisplay/Builds/2026-10-06/Hikvision Four Display.exe`；原`HKV`未覆盖，`Main.unity`和用户`UiDemo.cs`未修改。新版Player实际显示配置方案和旋转控件，单路保存日志仅`displayIndex=0`重载；检测到用户操作后停止自动点击并保留其保存设置。用户Canvas依赖UGUI，完整构建不能沿用早期`-noUpm`。
- 2026-10-06现场网络限制：提升权限只读检查12个新IP的TCP80/554共24项全部超时；本机有效网络为`10.146.6.214/24`，有线为169.254自分配地址，未接入192.168.199现场网段。没有发送登录认证或取流请求；新增设备实流、四块实体屏、长时运行仍待现场验收。

- 状态：`第一路Unity Editor画面与方向通过 / 打包程序四路同源冷启动通过 / 长时间运行与四块实体屏待验收`。
- 2026-09-21现场证据：`.71`的`101/102`均完成RTSP Digest并返回H.264 SDP；Standalone子码流进入`Playing`并创建纹理。修正Editor原生VLC目录后，用户重启并Play，日志出现`Connecting -> Playing -> HFD_FEED_TEXTURE_READY width=2560 height=1442`，Display 1实际显示连续摄像机画面，Display 2按配置显示`NOT CONFIGURED`。
- 当前配置：工程`Config/feeds.json`和包内模板均为四路启用、全部指向`.71`、逻辑通道`1`主码流，四路共享`feed1`凭据键；最新测试包的根配置、模板和运行程序集已同步。构建后处理会把工程当前非敏感配置净化后复制到包根`Config/feeds.json`，解决旧构建目录默认IP与工程不一致的问题。
- 四路同源验证：最新测试包实际读取包根配置，feed1～feed4均进入`Playing`并各自产生纹理；0个Feed故障、0个转换器错误、0个RTP缓冲警告。10秒样本为整机逻辑CPU约`11.8%`、工作集约`1.32GB`、私有内存约`1.96GB`；测试后已关闭Player。该结果只覆盖短时冷启动。
- 运行时修改IP不重连Bug（2026-09-21）：根因是`FeedConfigurationPanelUI.bootstrap`仅由场景搭建阶段的普通内存字段赋值，未序列化到场景；重新加载或打包后引用为空，按钮能写JSON，但`bootstrap?.ReloadConfiguration()`被静默跳过并仍提示“正在重新连接”。现已在`Awake`和保存前运行时解析唯一`FourDisplayBootstrap`，找不到时阻止保存并明确报错，保存成功后直接调用重载并记录`HFD_CONFIG_UI_RELOAD`。Editor/Player程序集离线Roslyn编译均退出码0；修复后的完整Player需重新Build并实点按钮验收。用户本次写入视频2的`.70`当前TCP `80/554/8000`均不可达，因此重载后应显示连接/重连状态，不能期待出现新视频。
- 当前边界：第一路Y轴UV方向修复已由用户截图验收；摄像机OSD仍显示`1970-01-01`，设备时区/NTP待修正。当前机器只检测到一块物理显示器，长时间内存/帧率稳定性、断线恢复、四台独立摄像机和四屏映射仍待验证。密码只存Windows凭据管理器，不进入源码、普通JSON、日志或RTSP URL。
- 下一步：修正设备时间，在四块实体屏上做映射、长时间运行与断流恢复验收。详细证据见`PortTraining/Docs/讨论记录/2026-09-21.md`及`HikvisionFourDisplay/Docs/解码方案PoC.md`。

### 堆场-箱位-集装箱-吊具最小完整闭环

- 状态：`搬运碰撞+落箱静止确认+占用更新实现完成/离线Roslyn编译通过 / 场景已摆3容器4箱位 / 20尺预制体补齐 / 40尺预制体待处理 / 全部未做Unity Play验证`
- 目标：学生能在`YardCrane`场景把堆场里一个真实集装箱（20尺/40尺均要支持）从源箱位抓起、搬运、放到目标箱位，占用状态正确更新。
- 已实现（Codex 2026-09-18继续推进，额度中途用完未留交付说明，以下是Claude事后读代码核实）：射线锁定机制（之前已完成）+ 搬运途中真实碰撞 + 落箱静止确认（速度/角速度阈值+持续时间）+ 占用状态更新（`UpdateReleasedContainerSettlement()`，源箱位锁上即清空、稳定后按容差匹配最近箱位、匹配不到如实记录不编造）；`YardCrane.unity`已摆3个`YardCraneContainer`实例+4个`YardCraneYardSlot`；`YardCraneContainer`锁孔机制从"运行时自动生成"改为"必须预先在场景/预制体里摆好+校验"（未记录的设计变更，已补记）。离线Roslyn重新编译`ObiRopeTest`程序集`EXIT=0`。
- 已实现（Claude 2026-09-18）：补齐`20尺集装箱.prefab`的`LockHoles`四角碰撞体层级+`lockHolesRoot`引用（照抄场景里已验证的Ft20样例）；`40尺.prefab`发现问题比预想更大（没有`YardCraneContainer`组件、没有真实碰撞体、根节点带着未清理的270°旋转和偏移位置）——用户确认后按20尺预制体的结构从头重搭（保留原.prefab/.meta路径和GUID，内容全换），Visual嵌套PrefabInstance逐条比对场景里`YC40-001`同一FBX的真实工作实例写法。两个独立预制体场景当前都不直接引用，不是活跃bug，是为以后复用做准备。
- 未验证：全程没有Unity Play验证（编译已用离线Roslyn确认通过，不再是纯静态检查）；两个预制体改动都没有在Unity里实际加载过；堆叠物理假设、20/40尺实际锁箱、吊具尺寸切换拒绝等第4.3/9节要求的全部真实Play验收都还没做；按分级属于L3，Play验证前不得视为完成。用户下一步计划自己去实测。
- 详见[YC-YARD-01-堆场箱位集装箱吊具最小闭环.md](PortTraining/Docs/Claude任务/2026-09-17/YC-YARD-01-堆场箱位集装箱吊具最小闭环.md)（进度状态已更正）、2026-09-17及2026-09-18讨论记录。

### 教师"任务考核"发布 → 学生"任务考核"接收

- 状态：`教师多题组卷与勾选发布、学生题目列表与单题投影已实现 / 静态Harness及C#编译通过 / Unity Play待验收`
- 目标：教师从多个实训项目和故障条目组成一套当前试卷；学生点击“任务考核”后先选择一道题，再把该题投影到`任务State.asset`并进入对应机型训练场景。
- 当前实现：题库与当前试卷按场桥/岸桥分别持久化为本机JSON；教师端实训项目每次只选择一个并形成一条独立题目，故障目录可跨类别多选并把本次选择整组形成一条故障题。任务列表行首勾选框决定整条任务是否纳入当前试卷。学生题目列表只读取当前机型的paper；选择实训题时写入完整训练快照，选择故障题时把该题内全部故障ID写入`任务State.asset`，故障时长固定为600秒。该新链路不再读写`试卷类型.asset`。训练完成返回学生首页后可重新进入列表选择下一题。
- 教师端添加题目与分组故障修复（2026-09-20）：修复`JsonUtility`把空的另一题型字段还原成默认对象后，题库校验误拒绝已有JSON，导致“添加项目/故障到任务列表”后续点击无效的问题；现有岸桥本地旧单故障题仍可兼容读取，不需删库。训练项目保持单选；故障目录恢复跨类别多选，一批选择只生成一条任务管理记录，类型显示“故障排除”、项目显示“故障 N 条”，并可通过“查看故障”弹窗浏览本组明细。每条任务行首现使用独立绘制的方形勾选框，勾选状态明确显示“已加入”，只有勾选任务才进入本次考核paper；筛选和统计文案同步使用“已加入/未加入”。训练项目多选方框继续移除；故障发布页不显示“发布范围”和“训练入口”，固定语义为“全体学生 + 任务考核”。题库模型保留旧`fault`并新增分组`faults`，旧JSON按单元素组兼容，学生paper对组内每条故障逐项清除答案字段。教师端静态Harness `328/328`通过；学生端分组故障断言通过，两种Build Profile宏下真实Unity Roslyn编译均通过。学生Harness仅保留既有`StudentHome.unity`岸桥场景名序列化失败；Unity Play交互与最终画面仍待人工验收。
- 数据安全与兼容：学生可读paper已升级为v2，发布时剔除`faultPoint`和`lineTag`，避免把故障答案暴露给学生；读取旧v1 paper时会就地清空答案字段、重新验证并原子重写为v2。场桥和岸桥题库、当前试卷彼此独立。
- 已知边界：所有题目进入训练场景后仍统一使用`PickupWorkflow`，本轮没有扩展正式题型动作、故障注入或专项评分规则；因此当前完成的是组卷、发布、选题及任务状态交接，不代表题型语义和正式评分全部闭环。
- 已验证：教师端和学生端静态Harness均完成C#编译检查；学生端当前仅保留1项已知场景断言失败：`StudentHome.unity`把`QuayCraneSceneName`序列化为`YardCrane`。Unity存在锁文件/未保存现场，本轮未从外部改写场景；需在Inspector改为实际岸桥场景名并保存后复跑Harness。
- 未验证：为保护当前Unity现场未自动启动Play；需人工验证教师跨类别多选后只新增一条“故障 N 条”记录、“查看故障”明细与行首勾选、学生题目列表与分组故障启动、故障题600秒、返回首页后再次进入，以及训练场景对全部所选故障ID的实际消费。
- **闭环第二阶段已实现**（任务列表+成绩关联+考核统计页接真数据）：`YardCraneAssignedAssessmentTaskStore`从单文件覆盖升级成可追加任务列表；`YardCraneTrainingSessionIdentity`新增`AssignedTaskId`（启动时捕获，不在保存时重读，避免误关联教师中途发布的新任务）；`YardCraneTrainingScoreRecorder`新增`ListAssessmentRuns()`（直接扫描`TrainingScores/`原始文件——发现`PickupWorkflow`考核是`Unscored`，从不进入按学生索引，必须扫原始文件）；教师端任务管理页/考核统计页从静态模板改成读真实数据（各自最多5/4条，超出只显示最新，无分页；无真实记录时保留原演示数据不强制清空）。真实考核记录统一显示"待复核"档位（考核本身不计分，不能诚实标优秀/合格），班级/专业列现有系统无对应数据源、如实显示"—"。
- **顶部统计磁贴已接真实数字**：任务管理页"总数/考核任务/实训任务"、考核统计页"记录数/已评分均分/优秀记录"从写死数字改成读真实数据（考核均分/优秀记录如实显示"—"/"00"，因为考核本身不计分，不编造数字）；按学生聚合的历史视图明确不做（用户已确认范围）。
- **修正一次误诊+补齐首页卡片**：用户报告"发布"按钮点了没反应，最初错误怀疑是按钮布局问题并做了无谓的CSS改动（用户实测反馈布局本身没问题，已改回）；真正的现象是教师端首页（登录后两张卡片的入口页，不是任务管理页表格）从来没显示过任何任务/成绩数据——这是一直存在的设计空白，不是这次改坏的。新增`RefreshHomeCards()`让首页两张卡片显示真实的已发布任务数/已记录考核数。
- **找到真正原因并修复**：任务管理页的筛选按钮和创建弹窗内部的模式按钮碰巧都叫"任务考核"，是两个互不相关的UI元素；弹窗每次打开都会把模式重置为默认"实训练习"，若发布前没在弹窗内部单独点一次模式按钮就不会写数据——这个设计陷阱连续绊倒同一用户两次。已把弹窗默认模式从"实训练习"改成"任务考核"（唯一真正有效果的模式），UXML静态默认值同步改过。
- **链路首次真实跑通**：用户实测确认`requireS7Link`异常消失、考核会话正确启动、真实操作后触发`[YC_TRAINING_RUN_SAVED]`落盘——但暴露存盘路径挂在`Application.persistentDataPath`（受Player Settings的Product Name影响）不可靠，这次实测落到了一个意外的产品名文件夹下。
- **JSON存放位置迁移到工程目录**：新建`YardCraneLocalDataPaths`，用`Application.dataPath/..`推导根目录（Editor下是工程根目录，打包后是exe所在目录），不再依赖Player Settings的Company/Product Name；`.gitignore`新增`LocalTrainingData/`。**注意**：旧路径（`AppData/LocalLow/.../TrainingScores/`）下的历史测试数据不会自动迁移，需要手动搬运。
- **整体重新规划为发布状态机 + 真实编辑/删除**：用户要求教师端任务管理只保留任务考核一个模块（弹窗去掉"实训练习/任务考核"模式选择）、任务可发布/取消发布/重新发布、列表的编辑删除做成真的。`YardCraneAssignedAssessmentTask`新增`isPublished`字段，`YardCraneAssignedAssessmentTaskStore`新增`Publish`/`Unpublish`/`Update`/`Delete`（`Publish`保证同一时刻全设备至多一个任务已发布，自动取消其它任务的发布状态）；`TryLoadCurrent`语义从"列表最后一条"改成"`isPublished==true`的那一条"。教师端任务列表每行从编辑/删除两个按钮扩展为编辑/发布(或取消发布)/删除三个按钮；创建弹窗支持编辑模式（回填既有任务字段，保存时更新而非新增）；任务筛选与统计磁贴从"全部/实训练习/任务考核"改成"全部/未发布/已发布"。已同步修正`YardCraneStudentShellVerification.cs`里因这次改动而失效的断言，并新增该验证脚本对`YardCraneAssignedAssessmentTaskStore.EditorOutputDirectoryOverride`的设置/清理（此前该按钮是纯预览、不需要重定向；现在会真的写盘，需避免验证脚本污染工程真实数据）。
- **考核结果判定+存储（数据层，2026-09-18）**：考核（`PickupWorkflow`）此前只写过程记录、从无合格/不合格结论、也不进任何按学生索引。新增自动判定规则（`YardCraneTrainingScoreRecorder.AssessmentGradeName`）：未完整走完流程或出现`Safety`类违规→`fail`；完整走完零违规→`excellent`；完整走完只有`Skill`类违规→`pass`。`RunRecord`/`YardCraneAssessmentRunSummary`新增`assessmentGrade`字段，`YardCraneAssessmentRunViolationSummary`补上`category`字段；新增独立的按学生考核索引`TrainingScores/assessments/<学号>.json`（跟现有官方计分训练的`students/`索引并列、不复用）+ 读取方法`ListAssessmentRunsForStudent`。
- **教师端考核统计页接上真实判定字段（2026-09-18）**：考核统计页对真实记录不再一律显示"待复核"——`BuildAssessmentDetail`按`assessmentGrade`映射出优秀/合格/不合格/待复核四态；违规明细条目现在会标注是"安全"还是"技能"类（用上了`category`字段）；新增"不合格"对应的行/成绩条/详情弹窗红色样式；统计磁贴"已评分均分"改成"合格率"（真实计算），筛选按钮数字改成真实计数。已披露的边界：只有"全部/优秀/待复核"三个筛选按钮，"合格"/"不合格"暂无独立筛选入口，只能在"全部"里看到。
- **真实编译验证已补上（2026-09-18）**：因当前Unity编辑器正开着本工程（`Temp/UnityLockfile`存在），不能安全另开实例跑`-batchmode`；改用离线复用Unity自带Roslyn（`Editor/Data/NetCoreRuntime/dotnet.exe` + `DotNetSdkRoslyn/csc.dll`）+ 工程里现成的Bee响应文件（`Library/Bee/artifacts/.../*.rsp`，产物路径重定向到会话暂存目录、不触碰工程内`Library/`，避免跟当前开着的Editor抢文件）编译`Runtime`/`Shell`/`ObiRopeTest`/`Editor`四个程序集，按依赖顺序全部`EXIT=0`、零错误零警告。
- **用户两轮实测`Verify Student Shell`，发现并修复三处真实回归**（均为既有缺口，均不是本轮任务发布/考核判定改动本身引入的——是这两个更早的既有功能自加入后从未被真正Play验证过，这次才第一次暴露）：
  1. `teacher-home-does-not-fake-platform-service`——"首页卡片接真实数据"那轮把静态占位文字换成真实文字后，断言要找的"待接入"字样已不存在。已改成检查更稳健的不变量。
  2. `teacher-task-management-entry-opens-dedicated-page`/`filter-updates-local-preview`——"闭环第二阶段"那轮任务列表接真实数据后，两句断言文字早已过期。已按当前真实文案更新。
  3. 考核统计页一串断言假设"本站从未有真实考核记录"，但`YardCraneTrainingScoreRecorder.EditorOutputDirectoryOverride`从未在这个验证脚本里设置过，工程里真实存在的历史考核记录会让这些断言全部按错误分支失败。已比照任务store已有的隔离做法，新增`ScoreVerificationOutputDirectory`并在`step==14`统一设置/清空——考核页那串断言本身不用改文字，隔离真实数据后会自动重新成立。
  三次修复均已用离线Roslyn重新编译`Editor`程序集确认`EXIT=0`，另外，本轮真正新增的发布/取消发布/编辑/删除按钮，这个验证脚本从未有测试代码覆盖其点击行为（刻意不扩建验证框架），需要用户手动实操验证。
- **真实功能缺陷已发现并修复：教师端操作反馈文字被自己覆盖**——`SaveTaskAsDraft`/`PublishTaskToStudents`/行内发布(取消发布)/行内删除，四处都是"先设置具体反馈文字（如'已创建为草稿：...'），再调用`RefreshTaskRowsFromStore()`"，而后者末尾自己也会写`taskPageStatus.text`（一句通用计数文字），导致教师实际上从来看不到那些具体反馈——瞬间被覆盖。这不是测试断言过期，是`Verify Student Shell`第三次推进后抓到的真实UI缺陷。已修复：四处都改成先刷新列表、再设置具体反馈文字。已用离线Roslyn重新编译`Shell`/`Editor`确认`EXIT=0`，**修复本身尚未被真实Play验证过**，需要用户继续重新跑`Verify Student Shell`并在手动操作时留意状态提示是否正常显示。
- **顺带确认（用户手动Play中查证）**：教师发布任务→学生端读取→考核判定→双重存储（`TrainingScores/run-*.json` + 新增的`TrainingScores/assessments/<学号>.json`）这整条链路已有一次真实端到端证据，`assessmentGrade`/`assignedTaskId`等新字段都正确落盘，且正确处理了一条本轮改动前创建的旧格式（v2）任务记录。
- **2026-09-19闭环审计**：本机现有`assessment-tasks.json`、2份`run-*.json`和1份`assessments/123.json`均可解析，任务ID引用与新索引内容一致，未发现损坏JSON、孤立任务引用或判定字段冲突；Unity当前日志中也没有任务读取、成绩保存、考核记录读取或故障目录加载失败。现有两次真实考核均为`interrupted`，其中v7记录按规则为`fail`，旧v6记录没有`assessmentGrade`并在教师端降级为`待复核`；因此“中途退出也能落盘并回读”已有证据，“完整完成后得到优秀/合格并由教师查看”的成功工况还没有真实证据。
- **审计发现的实质缺口**：教师选择的6类/24项任务仍全部启动同一个`PickupWorkflow`，目标内容只显示在说明里，故障排除也未连接故障注入与判定；教师统计在任务仍存在时会按`assignedTaskId`读取任务的**当前**类别/项目，教师后续编辑任务可能使历史考核显示新名称，而不是学生启动时写入`taskTitle`的快照；任务表和考核表分别只展示最近5/4条且无分页。当前只能称为单机数据闭环，不能称为任务语义或正式平台闭环。
- **数据兼容提醒**：现有任务记录的`formatVersion`仍为v2，而当前Store常量为v3；运行时能正常读取且`isPublished=true`有效，但`Publish/Update/Unpublish`不会刷新记录版本号。旧v6考核也未回填到后来新增的按学生`assessments/`索引。两项都不阻塞当前教师全局统计，但属于待迁移的历史数据债务。
- 详见2026-09-17、2026-09-18讨论记录。

### 场桥game4automation S7独立直连

- 状态：`独立场景首次Play验证通过 / 真实PLC联调待现场条件`
- 架构：`PortGatewayService`退出当前场桥交付路径；独立`YardCraneS7Integration`场景成为工程内唯一允许持有S7连接的位置，现有`YardCrane`、学生端和教师端保持零S7组件。
- 已落地：场景序列化持有`Game4AutomationController`、一个默认禁用的`S7Interface`、大小车两个`PLCOutputInt`占位点位和`YardCraneS7IntegrationBridge`；不在运行时动态创建接口或点位。
- 指令语义：大小车为PLC下发的有符号`INT`，原始范围`-3000..3000`，适配器限幅并归一化为`-1..1`；断连、信号无效或通信超时时强制归零。game4automation S7生命周期已补上双周期计数复位、完整读写成功门禁和”先发布首帧新值、再标记连接”，桥接器还会等待严格新增周期，避免重连重放旧速度。
- 2026-09-15独立Play验证（用户实测）：单独打开`YardCraneS7Integration`场景Play，`[YC_S7_INTEGRATION_READY] connectOnPlay=False, interfaceEnabled=False`，无Missing Script、无空引用、默认不发起连接。过程中发现并修复一个与S7逻辑无关的环境问题：`game4automation`自带的全局快捷键（`Game4AutomationController.Update()`里的ESC/F12）调用旧版`UnityEngine.Input`，与项目`Player Settings`原先的`Active Input Handling=Input System Package`不兼容，抛`InvalidOperationException`；已由用户在Editor里改为`Both`（`ProjectSettings.asset`的`activeInputHandler`现为`2`）并重启编辑器解决，不影响项目自身已使用新Input System的场桥输入代码。
- 待现场填写：PLC IP、Rack、Slot，以及大小车真实绝对地址；占位值和非`INT`配置会被拒绝连接。
- 2026-09-15 Additive组合可行性排查（不连PLC，用户实测确认）：Additive加载`YardCraneS7Integration`后，只要它不是Active Scene，`Game4AutomationController`所在GameObject会被vendor代码自动`SetActive(false)`（`Game4AutomationController.cs:932`），已用真实Play证据复现（Console输出`Deactivating game4automation Controller in Additive Scene ...`，Inspector确认GameObject被停用）。**结论：不加额外处理，直接Additive组合不可行**——`S7Interface`/`PLCOutputInt`等组件初始化时是否会因此触发vendor的`Debug.Break()`尚未验证；`SetActiveScene`等规避手段是否可行也未测试。详见2026-09-15讨论记录。
- 2026-09-16 `YardCraneS7InputSource`新增虚拟Input System设备`YardCraneDevice`（参照岸桥`AnqiaoDevice`模式，仅作Input Debugger可见性镜像，`Sample()`仍读自己的锁保护字段，不依赖设备读回）。**重要发现**：只要构造`YardCraneS7InputSource`就会触发`Unity.InputSystem`程序集加载，脱离真实Unity进程会在CLR类型加载阶段失败且`try/catch`捕获不到（已用`Tools/S7LinkHarness`实测确认）——该类自身行为（消费一次按钮语义、急停失效安全）**自今日起离线不可测，只能靠Unity Play**；`Tools/S7LinkHarness`已收窄为只测传输层+协议层（9项PASS），`Tools/StandaloneHostHarness`因其十四项测试从未触发`requireS7Link=true`路径而不受影响（仍14项PASS）。详见2026-09-16讨论记录。
- 2026-09-16 架构改为"S7独立进程常驻 + 主程序WebSocket客户端"：新增零依赖`YardCraneSimulator.SharedNet`程序集（手写最小WebSocket服务端/客户端，不引入第三方DLL）、S7Integration侧`YardCraneS7CommandWebSocketServer`（转发Bridge已算好的安全值）、主程序侧`YardCraneS7InputSource`（新`IYardCraneInputSource`实现）、`YardCraneStandaloneOperationHost.Configure(requireS7Link:...)`新增可选参数（默认`false`，唯一现有调用点未启用，现有键盘/Xbox行为零改动）。**已验证**：`Tools/S7LinkHarness`真实回环Socket端到端测试5项通过；`Tools/StandaloneHostHarness`原有14项回归全部保持PASS；SharedNet代码独立`dotnet build`通过；**用户已把`YardCraneS7Integration`打包成独立Player运行，并用新增Editor诊断菜单`Tools/Yard Crane/S7/Test WebSocket Link Connectivity`确认两个真实Unity进程间联通**（`framesReceived=41`≈20Hz，`gantry=0/trolley=0/valid=False`，与未连真实PLC时的安全归零语义一致）。**未验证**：`requireS7Link:true`未在任何调用点启用、真实PLC全链路未测、长时间稳定性未测。详见2026-09-16讨论记录。
- 2026-09-21点表更正：用户提供TIA Portal的`PLC-To-Twin [DB1]`截图，确认实际表为7路`REAL`速度（起升/大车/小车/导板/前后倾/平面回转/左右倾，`DB1.DBD0/4/8/12/16/20/24`）、开锁/闭锁两路`INT`（`DB1.DBW28/30`）和鸣笛`BOOL`（`DB1.DBX32.0`）。这与当前场景“两路INT速度+12路BOOL瞬动+急停”的占位契约冲突，不能直接填地址；真实表已写入S7Integration README。当前只先修复离散动作安全窗口：失联/断开立即丢弃未发送动作，单点或链路恢复后的首个健康样本只建立电平基线，不把保持高电平误判为新按键，无效WebSocket帧不得携带或累积动作。S7Integration与Runtime程序集在Unity `6000.0.42f1`响应文件下聚焦编译通过；Unity Play/真实PLC仍待验证。七路单位/方向/满量程、开闭锁INT有效码与复位语义、鸣笛电平语义以及急停/控制权/PLC看门狗点位未确认前，`connectOnPlay=false`、`CommandFrameValid=false`语义和`requireS7Link=false`继续保持。
- 未验证：未连接真实PLC，未验证地址读写、急停/控制权/心跳、断连重连、PLC看门狗与Windows Player；在这些证据补齐前该场景不得用于正式考核或发布。
- 现场保护：没有保存或覆盖用户当前未保存的`YardCrane`场景；正式五场景Build Settings保持原顺序。

### v32八根独立直绳、小车防摇与起升解耦

- 状态：`实现与编译验证通过 / Unity Play、视觉和物理待验证`
- 当前基线：运行时采用`8根独立直绳`；每根绳仅连接1个上锚点与1个吊具下锚点，并只生成1根Obi绳。当前实现版本为`2026-09-14-eight-straight-rope-v32-nominal-hoist-anti-sway`。
- 修正前证据：最新训练结果中空吊具小车移动的摆幅为`0.308–0.401 m`，多次超过训练阈值`0.300 m`；当时日志明确为`loaded=False`。静态追踪确认旧防摇控制仅在锁箱后运行，空吊具只有`0.04`的刚体线性阻尼。
- v31 Play回传：确认运行时加载了8绳拓扑和`empty+loaded`防摇；复位前四次停车日志为`0.184–0.216 m`，累计峰值`0.218 m`。但反复点动及起升混合工况出现`0.544–1.158 m`；训练结果仍记录3次`0.306–0.308 m`超限，因此v31不能放行。
- v32二次修正：训练摆幅统一为防摇控制使用的`质心相对小车偏移 - 当前平衡偏置`，原始绝对偏移只作诊断；起升时八根目标绳长改由“已重置的名义平衡姿态 + 绝对目标吊高”重新计算，不再把吊具瞬时摆动/倾斜逐步固化到绳长；修正静止状态向负方向启动时误用制动加速度的正负不对称。
- 新诊断：READY日志输出`instance/activeRigCount/scene/resetEpoch`；每次停车输出单次行程峰值、原始偏移、真实防摇误差、平衡偏置和八绳目标长度离散量，可区分重复实例、累计旧峰值与真实摆动。
- 已验证：v32的Runtime、ObiRopeTest、Editor三个程序集Roslyn编译均通过，定向`git diff --check`通过；Unity编辑器日志出现`Tundra build success (3.23 seconds), 18 items updated, 838 evaluated`。以上不替代v32 Play。
- 未验证：需要清空Console并关闭Collapse后重新进入Play，先固定吊高做纯小车，再做小车+起升同路程A/B；目标为`activeRigCount=1`且`segmentPeakSway < 0.300 m`。若固定吊高短点动仍超限，再进入小车S曲线/加加速度限制阶段。
- 现场保护：用户当前`YardCrane`场景存在未保存修改，Codex未保存、未覆盖该场景，也未为补充证据强行启动自动Play验证。
- 显示约束：导入模型自带的静态绳索与运行时8根Obi绳不能同时显示，否则视觉上会再次出现重复绳索；运行验收时须只保留其中一套可见。

### 驾驶HUD岸桥几何统一、着箱灯与制动状态

- 状态：`代码与轻量验证完成 / Unity着箱动作及视觉待验收`
- 目标：以岸桥实训场景实际引用的`OperatorHud.uxml`最终生效几何为基准统一场桥HUD；第一盏灯读取吊具着箱状态，右侧原锁销栏显示大车与小车各自的制动状态。
- 已修改：HUD位置与缩放、Panel Settings、HUD版本标识；新增独立`SpreaderContact`读回；大车制动复用`gantryDrive.IsBrakeApplied`，小车制动复用既有减速/保持判定并输出独立状态。两种制动灯互不排斥，断流或未知时熄灭。
- 已验证：13项几何/资源静态检查、着箱链路7项静态检查及制动栏12项静态检查通过；`HudPresentationHarness` 46项通过。
- 未验证：未启动Unity，真实着箱、大小车启动/制动灯态，以及1920×1080 Display1和1920×2160兼容画面等待用户验收。

### 教师端任务管理与考核统计页面

- 状态：`任务管理、创建任务弹窗、考核统计及考核详情弹窗实现完成 / Unity画面与点击待用户验收`
- 已实现：教师首页“任务管理”按钮进入同场景独立页面；页面沿用蓝色毛玻璃主题，包含5项场桥任务模板、全部/实训/考核筛选、任务类型/操作模式/项目/目标/时长/操作列、返回首页、编辑/删除入口，以及居中的三栏“创建训练任务”弹窗。
- 创建任务弹窗：左栏提供单项操作、联动操作、作业操作、能力提升、自由训练、故障排除6类任务；中栏随类别切换4项训练项目；右栏可编辑目标位置，并可互斥选择5/10/15/20分钟及本地/远程操作。关闭与“创建本机草稿”按钮均已接线。**2026-09-17更正**：本节最初描述的“实训练习/任务考核”模式选择已在同日晚些时候的整体重规划中移除（管理页只保留任务考核一种任务），以本文件上方"教师『任务考核』发布 → 学生『任务考核』接收"一节的最新状态为准。
- 考核统计：教师首页第二个按钮进入独立统计页；页面包含日期入口、4条学生演示记录、姓名/专业/班级/学号/项目/用时/成绩/详情列、全部/优秀/待复核筛选、姓名/学号/项目关键词筛选、统计摘要、详情入口和返回首页。
- 考核详情弹窗：4个“查看”按钮按所在行载入对应学生信息、记录编号、项目、用时、演示分数、成绩状态、过程摘要及3行扣分明细；优秀、合格、待复核采用独立状态样式，待复核记录保持无分数；关闭后返回原统计页。
- 真实性边界：任务均明确标记为本机预览草稿，成绩均明确标记为本机演示记录；平台任务与正式成绩服务尚未接入，创建、编辑、删除、日期查询和详情入口均不伪造持久化、发布、学生档案或正式考核结论。
- 已验证：Display1 UXML XML解析、控件数量与UI Toolkit样式兼容检查通过；`YardCraneSimulator.Shell`编译0错误，`YardCraneSimulator.Editor`在关闭无关项目依赖重建时编译0错误；`TeacherHomeUiHarness`为`[YC_TEACHER_HOME_UI_STATIC_PASS] checks=207 failures=0`；Play验证器已覆盖页面路由、成绩筛选、关键词筛选、详情逐行数据映射、扣分明细、待复核无分数、弹窗关闭及返回首页。
- 当前编译阻塞：完整Editor依赖链构建被未在本任务修改的`YardCraneEightRopeMaxPrototypeRig.cs`阻塞，其引用的`YardCraneContainer`当前不存在；教师端Shell和本次Editor验证代码本身已聚焦编译通过。
- 未验证：没有启动Unity，不把静态检查或聚焦编译描述为画面通过；1920×1080实际布局、筛选、弹窗、考核统计交互和最终视觉效果等待用户在TeacherHome中验收。

### 集装箱数据脚本与四角独立锁箱对位

- 状态：`实现完成 / 未编译、未做Unity Play验证`
- 已实现：新增`YardCraneContainer`数据组件（箱号/箱型Ft20-Ft40/皮重/毛重，ISO 668尺寸查表），可选挂载在`ActiveContainer`上，未挂载时行为与改动前完全一致；`YardCraneEightRopeMaxPrototypeRig`的锁箱对位判断从"箱体中心+整体yaw"的单一聚合检查，改成四角（FrontLeft/FrontRight/RearLeft/RearRight）各自独立测量，`IsContainerAlignedForLock()`要求四角都在容差内才放行，按用户要求比之前更严。
- 已提供（未接入代码）：`Twistlock_FL/FR/RL/RR`四个场景节点（局部坐标现为`{0,0,0}`占位）在20/40尺箱下的建议Local Position，供用户在Editor里按需摆放；当前对位判断完全不读取这四个节点，只是几何现算。
- 已知风险：现有对位容差（水平0.20m、yaw 3°）未随四角判断重新标定，按40尺箱真实半长换算，单yaw 3°就可能让角点偏移超出容差——之前卡着旧聚合阈值边缘能锁上的工况，现在可能锁不上，需要用户Play后确认是否要调大容差。
- 已验证：仅大括号配平和`git diff --check`空白检查。
- 未验证：未编译、未进Unity Play；直接影响`TryLockContainer`/`IsContainerAlignedForLock`安全联锁，按分级属于L3，Play验证前不得视为完成；此前验证过的30吨完整锁箱回归是否仍能通过尚未确认。
- 详见2026-09-16讨论记录。

### StudentSide Display1参考几何迁移与故障排除页面

- 状态：`岸桥100项、场桥90项故障均已归入8类并支持按机型切换；导包缺失目录的硬阻塞已修复 / 接收工程重新导包Play待验收`
- 修改范围：学生端Display1 UXML/USS、`YardCraneStudentSideShell.cs`、现有两份`Assets/Data/*故障信息.asset`的只读适配、现有Play断言及`StudentHomeUiHarness`；没有修改`Assets/Data`中的脚本或资产内容。
- 参考：外部工程`StudentSide/Display1UI`的整体几何、用户2026-09-19提供的两张故障排除页面截图，以及同日提供的《岸桥故障表修订1》《场桥故障表 修订1》工作簿。
- 已实现：学习训练模式的“故障排除”从占位入口改为独立页面；保留左侧五类导航及本地/远程入口，提供电源、俯仰小车、起升/大车、夹轮器、左右倾、平面回转、吊具泵、前后倾8类故障卡片，两行四列显示。点击卡片展示对应故障明细，纵向滚动条固定可见，并可返回分类页；故障现象、线号/标号和故障点均来自用户提供的工作簿。
- 数据边界：岸桥100项与场桥90项均已按描述归入8类，目录内不再有“待归类”；学生端根据`YardCraneStudentShell.SelectedCraneKind`动态读取`quayCrane`或`yardCrane`。工作簿没有原理图、故障位置图或操作指导附件，三个按钮继续给出真实的缺失资料提示；故障注入和训练判定仍待接入。场桥目录以活动工作表`Sheet3`为来源，不静默合并内容存在差异的`Sheet1`；原表疑似录入问题仍忠实保留。
- 2026-09-20导包与数据源修复：`StudentHome`场景分别序列化引用`岸桥故障信息.asset`与`场桥故障信息.asset`；学生端根据Build Profile宏解析出的`SelectedCraneKind`只读取对应资产，通过反射适配既有`_故障`字典结构。运行逻辑不再读取`StudentFaultCatalog.json`，两个Data资产也因此成为场景可追踪的GUID导出依赖。
- 保留：三模式入口、原10项可启动训练、场桥图片、数据绑定和既有路由逻辑。
- 2026-09-21学习训练闭环：教师发布的试卷只属于学生端“任务考核”，不再筛选学习训练故障目录。进入“故障排除”类别明细后点击“模拟故障练习”，学生端始终收集该类别本地完整故障列表的全部原始ID，写入`Assets/Data/任务State.asset`的`故障内容`，把任务标记为“故障训练”，再按`Mode_Anqiao`/`Mode_Changqiao`选定的机型直接加载岸桥或场桥实训场景。统一场景切换入口现强制要求本次操作恰好提供一种任务状态内容，因此学习训练、自由练习和任务考核均须先成功写入`任务State`才能进入实训场景。故障练习使用开放式学习会话，HUD标记“故障训练”，返回时回到学习训练选择页，不再借用自由练习语义。没有修改任何场景或`Assets/Data`脚本/资产。
- 已验证：数据检查直接解析两份Data资产，覆盖8类顺序、岸桥100项、场桥90项、两端零待归类及ID连续唯一；Unity `6000.0.42f1` Roslyn分别以`Mode_Changqiao`和`Mode_Anqiao`编译Shell及相关Editor代码，均退出码0；Shell工程连同Runtime项目经VS 2022 MSBuild编译成功。“学习故障不读取教师发布筛选”“所选类别完整ID写入”“开放式学习会话及返回页”“每次场景切换恰好一种任务状态内容”“忽略非学生学习任务类型”和按机型加载场景的静态契约均通过。当前`StudentHomeUiHarness`为`checks=313 failures=1 warnings=2`，唯一失败仍是既有`StudentHome.unity`岸桥场景名序列化覆盖问题，本任务遵照约束未修改场景；两条警告均为未执行Unity渲染、点击和跳转的验证边界说明。
- 2026-09-21接收工程兼容：学生端读取岸桥/场桥任务目录时只接收`单项操作、联动操作、作业操作、能力提升、自由练习`五种本地学习类型；`故障排除`及接收工程自行增加的其他任务类型直接忽略，不再导致整个`StudentHome`报`MISSING_ASSET`。支持类型自身若缺少必需字段仍会明确报错，避免把损坏的学习任务静默吞掉。没有修改任何`Assets/Data`脚本或资产。
- 未验证：当前Unity编辑器正在运行，为保护用户现场未自动切换或保存场景；尚未在一份全新的接收工程中重新导入修复后的`.unitypackage`并执行登录到故障页的Play闭环。
- 下一步：从最新工程重新导出包含`StudentHome`及依赖的包，在接收工程覆盖导入后重新登录；成功标志为出现`YC_STUDENT_SIDE_SHELL_READY`，且不再出现`YC_STUDENT_SIDE_SHELL_MISSING_ASSET`。

### Shell UI直接使用PanelSettings资源与分辨率自适应

- 状态：`实现及聚焦编译完成 / Unity Play与多分辨率画面待验收`。
- `StudentEntry`、`StudentHome`、`TeacherHome`的上下屏`UIDocument`继续使用各自场景已序列化的6份PanelSettings资源；`YardCraneShellPanelUtility`不再运行时`Instantiate`或销毁PanelSettings，也不再把临时克隆回填给`UIDocument`。
- 学生首页和教师首页已删除每帧写`panel.scale`及强制根画布为`1920×1080`、`left/top`居中的代码；登录页仅保留横竖屏样式类判断，不再写PanelSettings缩放值。
- 6份PanelSettings资源统一为`Scale With Screen Size + Shrink`，参考分辨率`1920×1080`；Display1仍指向显示器0，Display2仍指向显示器1，未合并或共用上下屏资源。
- 已验证：`YardCraneSimulator.Shell.csproj`经VS 2022 MSBuild编译成功；`TeacherHomeUiHarness`为`286/286`；`StudentHomeUiHarness`的`Mode_Changqiao`和`Mode_Anqiao`真实Roslyn编译均通过，资源、双屏和场景引用检查通过。学生端总结果仍为`278/279`，唯一失败是既有`StudentHome.unity`把岸桥场景字段保存为`YardCrane`，与本次面板改动无关。
- 未修改三个场景文件。Unity编辑器当前开启，为保护现场未自动Play；需在目标分辨率与双屏设备上检查留白、完整缩放和点击命中。
- 同批教师端小修：`YardCraneTeacherSideShell`新增Inspector字段“登录场景”，默认`StudentEntry`；返回按钮按该字段校验并加载，场景无效时保留当前教师会话并输出错误。

### HikvisionFourDisplay 单路保存与重连

- 状态：`实现与聚焦编译通过 / 2026-10-06完整新包及单路保存点击日志通过 / 现场四路画面连续性待验收`。
- 2026-09-21修复运行时配置面板的全路刷新问题：原“保存并连接”会调用`ReloadConfiguration()`并重建四路解码器；现改为每个摄像头配置块各自提供“保存并连接摄像头 N”，只替换对应`displayIndex`的JSON条目并调用`ReloadFeed(displayIndex)`，其余三路继续使用当前解码器和画面。
- 凭据隔离：密码留空时继续使用该路原凭据；输入新密码或修改用户名时改用该路独立凭据键，避免修改共享凭据后影响其他画面。日志新增`HFD_CONFIG_UI_FEED_SAVED`和`HFD_CONFIG_FEED_RELOAD`，均包含目标`displayIndex`。
- 已验证：Unity `6000.0.42f1`现有Editor与Player响应文件下Roslyn编译均为0错误；仅保留既有`MockPatternDecoder.FaultReported`未使用警告。
- 测试包：已把同一Player响应文件生成且程序集身份校验为`Assembly-CSharp`的新程序集更新到`D:\wuhanxiangmu\HKV`。无界面启动冒烟检查读取4路配置成功，视频1/3/4产生`HFD_FEED_TEXTURE_READY`，没有`TypeLoadException`、`MissingMethodException`或`NullReferenceException`；视频2仍按测试包当前保存的`.70`地址连接失败。
- 2026-09-21界面美化：运行时配置面板已按岸桥驾驶HUD框体基线改为深海军蓝半透明底、青蓝细描边、圆角卡片、顶部高光语义、白色主文字、蓝色主按钮和胶囊状态/码流控件；4路仍在同一滚动面板中，每张卡片保留独立保存连接按钮。圆角九宫格纹理由运行时生成，不新增外部图片资源。测试包已更新到`D:\wuhanxiangmu\HKV`，Editor/Player编译0错误，隐藏模式运行10秒未发现IMGUI、纹理生成或程序集加载异常。
- 默认/故障画面：四路启动时统一从`StreamingAssets/默认图片.png`加载同一张共享纹理；未配置、连接中、认证失败、断网、重连及其他取流失败状态均继续显示该图，取得有效视频纹理后才切换到实时画面。静态图使用正常UV，实时VLC纹理继续垂直翻转，二者都按16:9区域等比适配；占位图可用时不再显示居中的英文故障大字。测试包已同步该图片，实际启动日志确认只加载1次、尺寸`1672×941`，无缺失/解码/运行时异常。
- Canvas单屏路由：运行时配置新增根字段`canvasDisplayIndex`（`-1`关闭，`0..3`对应Display 1..4），每张摄像头卡片提供一个互斥Toggle；保存任一路时持久化当前唯一选择并设置场景中名为`Canvas`的对象为启用/禁用及对应`targetDisplay`，不会重连其他三路。运行时会包含inactive对象查找并缓存该Canvas；日志为`HFD_CANVAS_ROUTE`。`Main.unity`已确认含一个Screen Space Overlay Canvas，Editor/Player响应文件编译均为0错误。当前Unity编辑器占用工程，且该场景时间晚于现有`HKV`构建，旧包不含新Canvas，必须在保存现场后完整Build才能更新包；当前可先在Editor Play验收。
- 2026-10-06更正：当前已保存Main已通过完整Build进入新包，原“包内不含Canvas”的阻塞只适用于旧HKV。新版配置面板单路保存已有`HFD_CONFIG_UI_FEED_SAVED`/`HFD_CONFIG_FEED_RELOAD displayIndex=0`实际点击日志；假解码器验证确认另三路实例不释放。现场网络当前不可达，真实视频2修改后的连续播放仍待现场验证。

## Git状态

- 已存在仓库基线：`b0e27f5`；标签基线：`baseline-2026-09-03-v1`（`2da8297`）。
- 当前工作树包含大量跨模块已修改和未跟踪内容，不适合全量暂存或一次性“重新建基线”。
- 后续只按任务路径建立小提交；先清除/忽略临时验证目录，再逐模块收口。

## 默认快速工作流

1. 读取本页，按下方高频路径直接打开目标文件。
2. 开始前声明验收结果、预计文件和完整交付时间；微任务上限为5个实现/验证文件、2次精确搜索。
3. 不顺手重命名、重构或扩建验证器；只做一次实现和一次现有最小验证。
4. 任一预算超限或预计时间超过承诺上限1.5倍时，先暂停并重新说明，不静默扩大任务。
5. 报告实际修改、实际验证和待用户验收项；同批微调结束后再统一更新讨论记录与Git提交。

### 高频路径

- 学生首页：`StudentSide/Display1UI/StudentSideShell.uxml`及同目录USS。
- 教师首页：`TeacherSide/Display1UI`、`Display2UI`及`YardCraneTeacherSideShell.cs`。
- 驾驶HUD：`YardCraneUI/OperatorHud.uxml`、USS、Panel；数据链固定为`YardCraneOperatorHud → YardCraneHudSceneAdapter → YardCraneEightRopeMaxPrototypeRig`。

## 流程优化状态（2026-09-14）

- 已完成：一页式状态入口、最小阅读规则、L1/L2/L3门禁、Claude/Codex统一约束、任务路径轻量验证入口。
- 快速通道：纯样式和文案不再触发C#编译、Unity Play、全仓扫描或工程复制。
- 保护策略：默认验证范围只包含显式`-Paths`；不传路径时仅查看Git已跟踪差异并明确提示不含新文件。
- 待后续：在用户保存当前Unity现场后，为高频UI页面补充更快的固定分辨率预览入口；这不阻塞日常样式修改。
- 第二阶段已完成：增加微任务文件/搜索/时间硬预算、估时升级门槛、高频路径直达和禁止附带重构规则；小任务不再同时维护多份过程文档。
