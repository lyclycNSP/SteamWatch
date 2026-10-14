# 原生 UI 验收宿主

仅为 ACCEPT-201 编译同一套生产界面源码，允许固定的窗口激活、浅/深主题、同一个 MainPage 的卸载/重挂载、旧通知回调及合成提醒合并检查。管道只允许当前 Windows 用户，限定固定命令，不执行任意代码。宿主有 `-uiacceptance` 产品版本，需要显式数据目录标记，不能用于日常使用或交付。

在 PowerShell 7、交互桌面、没有 SteamWatch 自启项的隔离用户下执行。输出必须是新目录，不与其他桌面自动化同时运行：

```powershell
dotnet publish src/SteamWatch.App/SteamWatch.App.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:UiAcceptanceHost=true -o artifacts/ui-host-publish
dotnet publish tools/SteamWatch.RuntimeProbe/SteamWatch.RuntimeProbe.csproj -c Release -r win-x64 --self-contained false -o artifacts/ui-exit-probe
pwsh -NoProfile -File scripts/test-ui-acceptance.ps1 -HostPublishDirectory artifacts/ui-host-publish -ProbeExecutable artifacts/ui-exit-probe/SteamWatch.RuntimeProbe.exe -OutputRoot artifacts/ui-host-run
```

宿主保持生产程序集名以使用同一资源身份，但中间目录、输出和产品版本独立。普通构建不包含钩子；交付清单拒绝宿主标记及宿主产品版本，包括标记被移除的情况。

脚本复制到新绿色布局，工作目录与生产根启动器一致；校验实际 MainWindow 句柄后输入与截图，空白截图使本批失败。通过生产升级退出协议正常结束，不强杀进程。输出保存实际 DPI、主题、焦点、订阅/任务身份和失败现场。

应用内 RequestedTheme 不等于系统主题；原生标题栏仍可能跟随系统。Tab 焦点抽样不等于完整键盘旅程，无障碍树不等于真人读屏。实际缩放/高对比度、长文本、读屏及完整故障矩阵仍按 [手工验收](../../docs/manual-acceptance.md) 执行，当前证据只见 本地 `STATUS.md`。
