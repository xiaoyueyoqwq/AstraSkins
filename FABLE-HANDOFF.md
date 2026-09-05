# AstraSkins 交接：入场预览已完成，其余四条交给 Fable

给 Claude Fable。只动 AstraSkins。不要开这四个未修好问题的 PR。入场展示已单独成支。

仓库 fork：`https://github.com/xiaoyueyoqwq/AstraSkins.git`  
上游：`https://github.com/Ayrton09/AstraSkins.git`（`origin/main` = `740a514` / v1.0.10）

## 职责边界

- 只改 AstraSkins。不要改 Bot-Improver、不要写第三个插件。
- 真人用 `IsLiveHuman`。Bot intro 归 BotRandomizer。局内 pawn / 手上武器维持现状。
- `FollowCS2ServerGuidelines` 已经是 false，不要改。
- 管理手册：`/home/xiaoyueyoqwq/文档/CS2_服务器连接与维护手册.md`。有人在线不重启。不要改线上 CVar、SQLite、`AstraSkins.json`。
- CSS API 1.0.369，net10.0。

## 完成 / 未完成

| 面 | 状态 | fork 分支 |
|---|---|---|
| 入场展示探员（同一套写入也覆盖 intro 枪/手套） | 已完成，可 PR | `feat/team-preview-intro` |
| 购买展示皮肤 | 未修好，v1–v3 失败 | `wip/buy-menu-getiteminloadout` |
| 新一轮首次入场等待音乐 | 未修好，尝试修复时误伤，稳定复现 | `wip/wait-music-connect-prestart` |
| 选边展示探员 | 未修好（首次连接不齐） | `wip/team-select-preview` |
| 首次进服选边音乐 | 未修好 | 与等待音乐同一支 |

混装失败现场（线上 intro-music 那包）：`wip/mixed-intro-music-buymenu`。要对齐当时代码看这个，不要从混装里继续叠。

已在 fork、本轮不要重开 PR：

- `fix/music-kit-cue-timing` = `c11e83e`（回合切换 / C4 MVP cue）。父提交是 `bda5da5` EnableAllWeaponsStatTrak。
- `feat/stattrak-default` 是更旧实现（默认 true），不要覆盖。

## 硬约束

1. `OnPlayerDeath` **不得**改写 music netprop（`m_iMusicKitID` 等）。给局内尸体写会重触发 DeathCam，把 MVP / 回合 cue 切断。
2. 不要加 0.05s / 0.2s `round_mvp` 定时器去「补」音乐。
3. 在搞清等待音乐挤占之前，**不要重新武装** `SendInventoryUpdateEvent`。Path 2 的 Send 会在 connect-full / spawn / set-return 把客户端库存（含 `MusicID`）拉回原皮 SO。
4. 购买菜单不要再用同一套 `GetItemInLoadout` Post + SetReturn 重试。停规则已满足。
5. variant2 是同一 designer 名上的 `m_nVariant`，不是第二个 designer 名。不要为此加新签名，不要钩 `UpdateSelectTeamPreview`。
6. 不要写手套/探员到 GetItemInLoadout，不要写 `m_vecNetworkableLoadout`。

## Path 1（入场 / 选边预览实体）

Team intro 和 team select 读的是 `CCSGO_TeamPreviewCharacterPosition`，不是活 pawn。

写入字段：

- `m_agentItem`（探员 `ItemDefinitionIndex` + `SetStateChanged`）
- `m_glovesItem`
- `m_weaponItem`

只写 Xuid 对得上、且插件拥有的真人槽。

Designer 名：

```
team_intro_counterterrorist
team_intro_terrorist
team_select_counterterrorist
team_select_terrorist
wingman_intro_counterterrorist
wingman_intro_terrorist
```

`feat/team-preview-intro` 触发：

- `round_prestart`、`team_intro_start`：`ScheduleTeamPreviewApply()`（全场）
- `player_team` 对真人再 schedule 一次
- 定时：NextFrame / 0.10 / 0.25
- 菜单改皮肤/探员后立刻 `ApplyTeamPreviewCosmetics(player)`
- `ApplyToPlayer` 在 pawn 守卫前也会写预览

选边首次连接不齐，后来在 `wip/team-select-preview` 加了 `player_connect_full` 和 0.50 / 1.00。这不是新写入路径，只是补触发。入场 PR **不含**这两处。

## Path 2（购买菜单，失败）

思路：hook `CCSPlayerInventory::GetItemInLoadout` Post，造持久 `CEconItemView`，`SetReturn`，再 `SendInventoryUpdateEvent`。

`wip/buy-menu-getiteminloadout` 里代码还在，**Load 不调用 `HookGetItemInLoadout()`**，和现网 intro-music 一致。

v3 实测（`1.0.10-mkfix8-intro-buymenu3`，日志 `log-all20260906.txt`，真人 `76561199344426281`）：

| 项 | 值 |
|---|---|
| GetItemInLoadout 行 | 1572 |
| `match=cache` / `set-return` | 269 / 153 |
| `match=unmatched` | 1417 |
| `match=soid` | 0 |
| `after-set-return` Send | 5 |

悬停时 `this` 换成另一批指针，**`soid=0`**，对不上真人 embedded `CCSPlayerInventory`。客户端买菜单悬停不问人的库存。匹配到人的 SetReturn+Send 也没把购买页打成皮肤。

买完停在灰按钮上那一瞬带皮，是手上 GiveNamedItem 实体，不是这条 hook。

GiveNamedItem 保持 Post。购买页阶段 2（portrait / pawn 借用）没写。

## 等待音乐 / 进服选边音乐

采样窗口是 `team_intro_start` / `round_prestart` 的控制器 `MusicKitID` / 库存 `MusicID`。`team_intro_end` / `round_start` 已经晚了。

误伤来源：

1. Path 2 在 connect-full、spawn、第一次 set-return 后 `SendInventoryUpdateEvent`，挤占开局采样。
2. 停 hook 之后，又在 connect-full + 0.15/0.5/1.0、`round_prestart`、`team_intro_start` 加写。部署为 `1.0.10-mkfix8-intro-music`。**等待音乐仍未修好，开始对局丢等待音乐可稳定复现。**

`wip/wait-music-connect-prestart` 基于 `c11e83e`，只含那批加写和 `EnsureMusicKitWhenProfileReady` 的尸体跳过（有有效 pawn 且已死才跳过）。父分支带 StatTrak。不要把这支当修复合进去。

`c11e83e` 本身是死亡路径 / C4 MVP cue，和等待音乐不是同一件事。死亡路径：已有 MVP cue 则 replay 给听众，不改 netprop。

## 现网（本轮不要动）

版本：`1.0.10-mkfix8-intro-music`（混装树，等于 `wip/mixed-intro-music-buymenu`）。

- 备份：`/home/steam/recovery/pre-astraskins-mkfix8-intro-music-20260906-004817/`
- stage：`/home/steam/stage/astraskins-intro-music-20260906-004817/`
- zip `7cfd71b98534d44cd412f9f9abc6e99745b5e11d713e7ee9142f7ee36aa2c172`
- DLL `4247377a0f0de836a6c802663b1208c44351cede6c2ef21b9fd2c66285dbcb34`
- gamedata `f1e823f3f604d834f70083942463cf456a3257b97a0c0e852ed7384b4225954d`（8 条签名仍在磁盘上，hook 未武装）

SSH 用户 `admin`，进程用户 `steam`。CSS file-watch 会在进程重启前热加载 DLL：要更新就先 stop 再 rsync 再 start。不要用 root `cp -a` 覆盖 steam 文件。

## 建议开工顺序

1. 读 `feat/team-preview-intro` 了解 Path 1 写入。入场不要再改。
2. 等待音乐先回归：对比 `c11e83e` 与 `wip/wait-music-connect-prestart` 与混装树，确认开局采样窗口不再被 Send / 额外写冲掉。先恢复「不比 mkfix8 更差」，再谈进服选边音乐。
3. 选边探员：在 Path 1 写入已存在的前提下只补触发（Xuid 何时填上）。参考 `wip/team-select-preview`，不要重写 agent 字段逻辑。
4. 购买菜单：不要重做 GetItemInLoadout SetReturn。悬停走 soid=0 的别的 inventory。需要新路径。

本地不要从 `feat/team-preview-cosmetics` 继续混装开发。

## Fable 第二轮（未上线、未线上验证，只有 `dotnet build -c Release` 通过）

两支独立分支，都没有开 PR，都没有碰线上、CVar、SQLite、`AstraSkins.json`。

### 1. `fix/music-kit-inventory-netprop` = `d46e9f8`（基于 `c11e83e`）

诊断：所有分支的 `ApplyMusicKitState` 都写了 `InventoryServices.MusicID`（`CCSPlayerController_InventoryServices.m_unMusicID`，网络字段），但从未
`SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices")`。`m_iMusicKitID` 有打标，`m_unMusicID` 没有。客户端只在引擎自己刷
InventoryServices 时才拿到新值，所以在 prestart / intro_start 写多少次都不进采样窗口；这也解释 Path 2 的 `SendInventoryUpdateEvent` 为什么会「拉回原皮」——那是唯一真正把 `m_unMusicID` 下发出去的时刻。参考 WeaponPaints `GivePlayerMusicKit`：两者都打标。

改动：

- `ApplyMusicKitState`：写 `MusicID` 后打标 `m_pInventoryServices`；四个字段全部先比较再写 + 打标（状态已一致就什么都不发）。
- `EnsureMusicKitWhenProfileReady`：不再自己比较，直接走上面的幂等写；带尸体跳过（有有效 pawn 且 `!PawnIsAlive` 才跳过，选边 / 连接阶段无 pawn 照常写）。
- 按硬约束 1/2 去掉 `c11e83e` 的：`OnPlayerDeath` 里 `TryApplySelectedMusicKit` + 0.15s 定时器；`OnRoundMvp` 里 0.05/0.2 定时器。死亡路径改成回合级 `_pendingMvpCue`，任何真人死亡（不只 C4）若本回合 MVP cue 已开始就 `FireEventToClient(listener)` 重播，不改 netprop。
- 新增 `round_prestart`、`team_intro_start`、`player_connect_full` 各一次 `ApplyMusicKitWhenProfileReady`（单次写，无补偿定时器）。
- **没有**重新武装 `SendInventoryUpdateEvent`。

线上验收点：开局 / 新一轮首次入场等待音乐；首次进服选边音乐；C4 炸死 MVP 的 anthem 是否还被切；`css_wsdebug` 里 `MusicID`（库存）与 `MusicKitID`（控制器）是否一致。

如果打标后等待音乐还是丢：下一步该查客户端是不是在 `player_connect_full` 之前就已经采样了一次（这时只能靠 auth 期 preload 让 connect_full 那次写赶上），以及 Valve 的库存同步是否在我们之后又把 `m_unMusicID` 覆盖回去（1s 巡检现在会看到差值并重写，但会晚 ≤1s）。

### 2. `fix/team-select-preview-ensure` = `30b0c0c`（基于 `feat/team-preview-intro`，agent/手套/枪写入未改）

`SkinManager` 按 preview 实体 index 记住上次写入后的签名（Xuid、agent/gloves/weapon 的 `ItemDefinitionIndex`、gloves/weapon 的 `ItemIDLow`）。现有 1s 健康巡检加 `EnsureTeamPreviewCosmetics()`：只重写「Xuid 非 0、能对上真人、当前签名 ≠ 上次写入」的槽；一致的槽只有几次 schema 读，不打标、不下发。Xuid 归 0 时清该槽签名；map start 清全部。

触发补充：`player_connect_full` 先 `PreloadProfile` 再走原有 NextFrame/0.10/0.25 apply；`OnClientAuthorized` 提前开 profile 读。没有加 0.50/1.00 定时器（`wip/team-select-preview` 那两条不需要了）。

线上验收点：首次进服选边界面自己的探员 / 手套 / 枪；切队后再看选边（Valve 重填 Xuid 后 ≤1s 应被巡检补上）；`team_intro_*` 行为应与 `feat/team-preview-intro` 一致。

### 3. 购买菜单

未动。仍按硬约束 4：不要重做 `GetItemInLoadout` Post + `SetReturn`。待 1/2 线上验完再研究悬停 soid=0 属于哪批 inventory。
