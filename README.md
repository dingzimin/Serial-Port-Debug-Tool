# 串口调试工具 Serial Port Debug Tool
基于 C# 和 WPF 的串口调试工具，可用于计算机和微控制器的串口通信。
需要 .NET Framework 4.x 运行环境

![Screenshot](https://raw.githubusercontent.com/dingzimin/Serial-Port-Debug-Tool/master/images/Screenshot0.png)

## 特性
* 支持基本的串口接收和发送
* 自动列出所有的串口
* 串口发生变化时，自动更新串口列表 (2018.10)
* 支持自定义波特率、校验位、数据位、停止位
* 支持文本、16进制、带转义字符的文本
* 一键清空接收区数据
* 16进制可自由输入，支持包括但不限于以下格式 (2018.Q2)
    * 024CB016EA
    * 02 4c b0 16 ea
    * 02-4C-B0-16-EA
    * 2 4CB0 16EA
    * z2y>.jt4cnm^#gB0']pok16rg-(=eA
* 支持ASCII、UTF-8编码，其他编码尚未实现
* 窗口置顶 (2018.10)
* //窗口半透明 (2018.11?)
* 可自由调整各区域大小 (2018.10.31)
