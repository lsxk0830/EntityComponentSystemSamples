# HelloNetcode 主机迁移示例

有关该功能的详细信息，请参阅手册中的[主机迁移](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/host-migration/host-migration.html) 部分。

请参阅[Asteroids 中的主机迁移](https://docs.unity3d.com/Packages/com.unity.netcode@latest?subfolder=/manual/host-migration/host-migration-sample.html) 部分中有关此示例中的功能实现的详细信息。

> [!NOTE]
> 不包含 ghosts 的示例（例如 hello netcode 示例 `RPC`、`ConnectionMonitor`、`GoInGame` 和 `ConnectionApproval`）将没有任何要迁移的数据，并且无法用于测试主机迁移。主机迁移 system 仅当 server world 中至少有 1 个 ghost 时才会开始收集主机迁移数据。因此，通常情况下，在至少生成一个 ghost 之前，不会上传主机迁移数据，但在这些示例中从未发生过这种情况。然后，主机迁移控制器将中止并报告错误，因为在主机迁移事件期间未找到主机迁移数据。