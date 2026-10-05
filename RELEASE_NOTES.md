# 桌面课笺 1.6 更新说明

> 1.4、1.5、1.5.1 没有单独发布，以下是相对 1.3 的全部变化。English below.

下载 `DeskStudy-1.6-Windows.zip`（约 630 KB），解压后双击 `DeskStudy.exe`。压缩包里有图文版 `使用指南.html` 和 `User Guide.html`。

## 新功能

- **桌面组件模式**：课表、Todo、DDL 变成无边框的小组件，平时待在其他窗口下面。拖动时自动贴齐、互不重叠，可以折叠到屏幕底部，四角可选圆角或直角。升级后默认开启，可在设置里切回标准窗口。
- **任务栏按钮**：任务栏上有一个「桌面课笺」按钮，单击把组件叫到前面，再单击收回去。
- **DDL 自动排序**：按截止时间排好，最急的在最上面，做完的沉到底。截止时间可以只填日期。
- **DDL 显示在日历上**：有时间的 DDL 显示为 ⚑ 小标签，有 DDL 的日子在日期旁有角标；单击跳到对应任务并高亮。
- **Outlook 日历只读同步**：粘贴 Outlook 发布的日历链接，日程显示在日历上并定时更新，可以设置提醒、按日程名称配色，也可以一键复制成本地日程并停止同步。
- **全天日程**：可以跨多天、可以每周重复，显示在星期行下面的「全天」行。在周视图里双击日期直接新建。
- **双击编辑任务**：双击任务文字原地修改，回车或点别处都会保存。
- **英文界面**：设置中心「外观」里切换中文 / English，第一次打开时跟随 Windows 语言。
- **工作周视图**：只显示周一到周五；日历外观也会跟随便签布局。
- **日程颜色**：颜色选择带色块，可以直接选已经在用的颜色，也可以自定义。

## 改进

- 翻页、打勾、编辑任务时不再闪烁。
- 周视图显示完整的日程名称，放不下会换行，不再显示省略号。
- 便签标题区更紧凑。

## 修复

- 在锁定的组件上向下滚动滚轮可能导致程序报错退出。
- 纸页本、原始外观偶尔残留一条多余的横向滚动条。

## 升级前请注意

- 建议先在旧版设置中心导出一份全部数据作为备份。
- 在旧版托盘图标上右键选「保存并退出」，再启动 1.6。数据会自动读取，无需导入。
- 升级后不要再用旧版打开同一份数据；需要回退时，用升级前导出的备份。
- 程序默认不联网。只有订阅了 Outlook 日历，才会下载你填写的那一个链接，而且只下载、不上传。
- 程序没有数字签名，首次运行如果被 Windows 拦截，点「更多信息」→「仍要运行」。
- 运行环境：Windows 10 / 11，.NET Framework 4.8（Windows 10 1903 及以后版本和 Windows 11 自带）。

---

# DeskStudy 1.6 release notes

> 1.4, 1.5 and 1.5.1 were never released on their own; this lists everything new since 1.3.

Download `DeskStudy-1.6-Windows.zip` (about 630 KB), unzip it and double-click `DeskStudy.exe`. The zip includes an illustrated `User Guide.html` (and `使用指南.html` in Chinese).

## New

- **Desktop widgets**: the calendar, To-do and Deadlines become borderless widgets that stay behind your other windows. They snap into place without overlapping, fold down to the bottom of the screen, and have rounded or square corners. On by default after upgrading; Settings can switch back to ordinary windows.
- **Taskbar button**: one DeskStudy button on the taskbar. Click it to bring the widgets forward, click again to send them back.
- **Deadlines sort themselves**: by due time, most urgent on top, done ones at the bottom. A deadline can be a date without a time.
- **Deadlines on the calendar**: timed deadlines show as small ⚑ tags, and days with deadlines get a badge. Click one to jump to the task, highlighted.
- **Read-only Outlook calendar**: paste the link of a published Outlook calendar and its events appear on the calendar, refreshed on a schedule, with optional reminders and colors per event name. You can also copy them into your own events once and stop syncing.
- **All-day events**: across several days and repeating weekly, shown in an All day row under the dates. Double-click a date in the week view to create one.
- **Double-click to edit a task** in place; Enter or a click anywhere else saves.
- **English interface**: switch between English and 中文 under Settings → Appearance. The first start follows your Windows language.
- **Work week view** (Monday to Friday); the calendar's look now follows the notebook layout.
- **Event colors**: swatches, the colors you already use, or a custom color.

## Improved

- No flicker when turning pages, ticking or editing tasks.
- The week view shows full event names, wrapping instead of cutting them off with "…".
- A more compact notebook title area.

## Fixed

- Scrolling down over a locked widget could make the program crash.
- Paper and Original layouts sometimes kept a stray horizontal scroll bar.

## Before you upgrade

- We suggest exporting all data from the old version's Settings first, as a backup.
- Right-click the old version's tray icon and choose Save and exit, then start 1.6. Your data is read automatically.
- Don't open the same data with an older version afterwards; to go back, use the backup you exported.
- DeskStudy is offline by default. Only if you subscribe to an Outlook calendar does it download from the link you entered, and it never uploads anything.
- The program is not code-signed. If Windows stops it the first time, choose More info → Run anyway.
- Requires Windows 10 / 11 with .NET Framework 4.8 (built into Windows 10 version 1903 and later, and Windows 11).
