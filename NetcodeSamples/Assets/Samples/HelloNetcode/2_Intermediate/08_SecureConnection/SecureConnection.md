# HelloNetcode 安全连接示例

NetCode package 提供了一种创建 client 和 server worlds 并将它们安全地连接在一起的方法（client 连接到 server）。引导程序。

看

* [入门](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/getting-started.html) 指南中的 _ 建立连接 _ 部分
* [Client Server Worlds](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/client-server-worlds.html)
* [网络连接](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/network-connection.html)
* [安全 Client 和 Server](https://docs-multiplayer.unity3d.com/transport/current/secure-connection)
* [生成所需密钥和证书](https://docs-multiplayer.unity3d.com/transport/current/secure-connection#generating-the-required-keys-and-certificates-with-openssl)

## 要求

只需要引导程序设置 client 和 server world。

* BootstrapAndFrontend

## 示例描述

此示例显示了为启用安全连接功能而需要对引导程序功能进行的修改。

此示例不包含 scene，因为无需向 scene 添加任何内容即可启用此功能。

要在网络驱动程序上建立安全连接，需要设置自定义/手动驱动程序，以便可以将不同的网络参数传递给它。
需要在引导程序中设置自定义驱动程序构造函数，以便尽早替换默认驱动程序。这是在 _SecureBootStrapExtension.cs_ 中完成的，但它被文件顶部的 _ENABLE_NETCODE_SAMPLE_SECURE_ 定义禁用，因为启用它意味着它在整个项目中全局强制执行。要启用它，只需取消注释那里以及 _NetworkParams.cs_ 和 _SecureDriverConstructor.cs_ 中的定义。

### 生成安全参数

在 _NetworkParams.cs_ 内，您将看到包含此​​示例生成的证书的静态变量。对于您自己的游戏，您可以自己生成这些。请点击上面的链接“[生成所需的密钥和证书]”，了解如何执行此操作。

**NOTE：** 确保您在发布游戏时不会发送生成的密钥和证书。
即使源代码被混淆，恶意用户也很容易反编译并看到密钥。

### 传递安全参数

生成的证书将传递到 _SecureDriverConstructor.cs_ 中的网络驱动程序。这是使用辅助函数来设置网络设置的默认值。
您可以更改此设置以手动构建网络驱动程序实例，或使用适当的覆盖传递您自己的网络设置。
